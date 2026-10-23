using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DayKeep.Core;

public sealed record TaskEvent(Guid Id,Guid TaskId,string Kind,DateTimeOffset RecordedAt);
public sealed record DayBudget(Guid TaskId,DateOnly LocalDate,int ExtraCount,DateTimeOffset? CoveredAt);
public sealed record DataSnapshot(IReadOnlyList<TaskItem> Tasks,IReadOnlyList<TaskEvent> Events,
    IReadOnlyList<TaskCheck> Checks,IReadOnlyList<ReminderProgress> Progress,IReadOnlyList<DayBudget> Days,
    IReadOnlyList<ReminderDelivery> Deliveries,int RevisionFloor);
public sealed record BackupEnvelope(string Format,int Version,int SchemaVersion,DateTimeOffset ExportedAt,DataSnapshot Data,string Sha256);
public enum ImportMode { Merge, Replace }
public sealed record ImportResult(int ImportedTasks,string RecoveryPath);
public sealed class ImportPreview
{
    internal ImportPreview(BackupEnvelope source,string targetHash,DataSnapshot current)
    { Source=source;TargetHash=targetHash;Current=current;var ids=current.Tasks.Select(t=>t.Id).ToHashSet();NewTasks=source.Data.Tasks.Count(t=>!ids.Contains(t.Id));var incoming=source.Data.Tasks.Select(t=>t.Id).ToHashSet();RemovedByReplace=current.Tasks.Count(t=>!incoming.Contains(t.Id)); }
    internal BackupEnvelope Source { get; }
    internal DataSnapshot Current { get; }
    internal string TargetHash { get; }
    public DateTimeOffset ExportedAt=>Source.ExportedAt;
    public int IncomingTasks=>Source.Data.Tasks.Count;
    public int IncomingChecks=>Source.Data.Checks.Count;
    public int CurrentTasks=>Current.Tasks.Count;
    public int NewTasks { get; }
    public int KeptIds=>IncomingTasks-NewTasks;
    public int RemovedByReplace { get; }
    public string? MergeError { get; internal set; }
    public string Describe(ImportMode mode)=>mode==ImportMode.Merge
        ? $"合并将新增 {NewTasks} 件事项；同 ID 的 {KeptIds} 件保留本机任务和记录。现有 {CurrentTasks} 件均保留。"
        : $"替换后为 {IncomingTasks} 件事项。将移除本机备份中没有的 {RemovedByReplace} 件，并恢复备份内的状态和记录。";
}

public static class BackupFile
{
    public const long MaxBytes=64*1024*1024;
    public const int MaxRecords=200000;
    private static readonly JsonSerializerOptions Options=new()
    {
        PropertyNamingPolicy=JsonNamingPolicy.CamelCase,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations=true,RespectRequiredConstructorParameters=true,MaxDepth=32
        ,AllowDuplicateProperties=false
    };
    public static string Hash(DataSnapshot data)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(data,Options)));
    public static BackupEnvelope Read(string path)
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(stream.Length>MaxBytes) throw new TaskValidationException("备份超过 64 MB，未载入。请保留原文件。");
        BackupEnvelope envelope;
        try { envelope=JsonSerializer.Deserialize<BackupEnvelope>(stream,Options) ?? throw new JsonException(); }
        catch(JsonException) { throw new TaskValidationException("备份格式不完整或无法识别，原数据未修改。"); }
        Validate(envelope); return envelope;
    }
    public static BackupEnvelope Seal(DataSnapshot data,DateTimeOffset now)
    { ValidateData(data);return new("daykeep-backup",1,5,now,data,Hash(data)); }
    public static void Write(string path,DataSnapshot data,DateTimeOffset now)
    {
        var envelope=Seal(data,now);
        var bytes=JsonSerializer.SerializeToUtf8Bytes(envelope,new JsonSerializerOptions(Options){WriteIndented=true});
        if(bytes.LongLength>MaxBytes) throw new TaskValidationException("完整备份超过 64 MB，未写入；请保留任务库。");
        var full=Path.GetFullPath(path);
        if(File.Exists(full)) throw new IOException("目标文件已存在，请选择新的文件名。");
        var temporary=full+".tmp-"+Guid.NewGuid().ToString("N");
        try
        {
            using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {file.Write(bytes);file.Flush(true);}
            File.Move(temporary,full,false);
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException)
        {
            try {File.Delete(temporary);}
            catch(Exception cleanup) when(cleanup is IOException or UnauthorizedAccessException)
            {throw new IOException("备份写入失败，临时文件未能清理；目标原文件未覆盖。",new AggregateException(error,cleanup));}
            throw;
        }
    }
    public static void Validate(BackupEnvelope envelope)
    {
        if(envelope.Format!="daykeep-backup" || envelope.Version!=1 || envelope.SchemaVersion!=5)
            throw new TaskValidationException("备份版本不受支持，请使用兼容的日守版本；原数据未修改。");
        if(envelope.Data is null || envelope.Sha256 is null) throw Invalid();
        ValidateData(envelope.Data);
        if(!string.Equals(envelope.Sha256,Hash(envelope.Data),StringComparison.OrdinalIgnoreCase))
            throw new TaskValidationException("备份校验不匹配，文件可能不完整或已被改动；未导入。");
    }
    public static void ValidateData(DataSnapshot data)
    {
        if(data.Tasks is null || data.Events is null || data.Checks is null || data.Progress is null || data.Days is null || data.Deliveries is null) throw Invalid();
        if(data.Tasks.Count>20000 || (long)data.Tasks.Count+data.Events.Count+data.Checks.Count+data.Progress.Count+data.Days.Count+data.Deliveries.Count>MaxRecords)
            throw new TaskValidationException("备份记录数量超过本版限制，未导入。");
        if(data.Tasks.Any(t=>t is null) || data.Events.Any(t=>t is null) || data.Checks.Any(t=>t is null) || data.Progress.Any(t=>t is null) || data.Days.Any(t=>t is null) || data.Deliveries.Any(t=>t is null)) throw Invalid();
        Unique(data.Tasks.Select(t=>t.Id));Unique(data.Events.Select(t=>t.Id));Unique(data.Checks.Select(t=>t.Id));Unique(data.Progress.Select(t=>t.TaskId));
        Unique(data.Days.Select(t=>(t.TaskId,t.LocalDate)));Unique(data.Deliveries.Select(t=>t.Key));
        var tasks=data.Tasks.ToDictionary(t=>t.Id);
        void Ref(Guid id) {if(id==Guid.Empty || !tasks.ContainsKey(id)) throw Invalid();}
        if(data.RevisionFloor<0 || data.RevisionFloor>=int.MaxValue-1) throw Invalid();
        foreach(var t in data.Tasks)
        {
            if(t.Id==Guid.Empty || t.Revision<1 || t.Revision>=int.MaxValue-1 || t.Title is null || t.Url is null || t.Notes is null || t.Reminder is null || t.Reminder.Times is null) throw Invalid();
            var draft=TaskRules.Validate(new(t.Title,t.Kind,t.Url,t.Notes,t.Reminder.Times,t.Reminder.Weekdays,t.DueDate?.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture) ?? "",t.Reminder.RepeatMinutes,t.Reminder.MaxExtraReminders));
            if(draft.Title!=t.Title || draft.Url!=t.Url || draft.Notes!=t.Notes || draft.Times!=t.Reminder.Times || !Enum.IsDefined(t.State) || (t.Kind==TaskKind.Todo && (t.State==TaskState.Waiting || t.BeforeCompletion==TaskState.Waiting))) throw Invalid();
            if(t.State==TaskState.Completed ? t.CompletedAt is null || t.BeforeCompletion is not (TaskState.Waiting or TaskState.Ready) || t.Paused : t.CompletedAt is not null || t.BeforeCompletion is not null) throw Invalid();
        }
        foreach(var e in data.Events) {Ref(e.TaskId);if(e.Id==Guid.Empty || string.IsNullOrWhiteSpace(e.Kind) || e.Kind.Length>80) throw Invalid();}
        foreach(var c in data.Checks) {Ref(c.TaskId);if(c.Id==Guid.Empty || c.Notes is null || c.Notes.Length>4000 || !Enum.IsDefined(c.Outcome)) throw Invalid();}
        foreach(var p in data.Progress) {Ref(p.TaskId);if(p.Version<1 || p.Version>=int.MaxValue-1 || p.ExtraCount is <0 or >24 || (p.NextRepeatAt is not null && p.SnoozedUntil is not null)) throw Invalid();}
        foreach(var d in data.Days) {Ref(d.TaskId);if(d.ExtraCount is <0 or >24) throw Invalid();}
        foreach(var d in data.Deliveries)
        {
            Ref(d.Key.TaskId);
            if(!Enum.IsDefined(d.State) || !Enum.IsDefined(d.Key.Kind) || d.TaskRevision<1 || d.TaskRevision>=int.MaxValue-1) throw Invalid();
            if(d.Key.Kind==ReminderKind.Fixed ? d.Key.Generation!=0 || d.Key.Instant!=0 : d.Key.Generation<1 || d.Key.Instant!=d.ScheduledAt.UtcTicks) throw Invalid();
            if(d.State is DeliveryState.Pending or DeliveryState.Shown && (d.TaskRevision!=tasks[d.Key.TaskId].Revision || !ReminderPlanner.Active(tasks[d.Key.TaskId]))) throw Invalid();
            var difference=d.Key.Date.ToDateTime(d.Key.Time).Ticks-d.ScheduledAt.UtcTicks;
            if(Math.Abs(difference)>TimeSpan.FromHours(14).Ticks) throw Invalid();
        }
        if(data.Deliveries.Where(d=>d.State is DeliveryState.Pending or DeliveryState.Shown).GroupBy(d=>d.Key.TaskId).Any(g=>g.Count()>1)) throw Invalid();
    }
    private static void Unique<T>(IEnumerable<T> values) where T:notnull {var seen=new HashSet<T>();foreach(var value in values) if(!seen.Add(value)) throw Invalid();}
    private static TaskValidationException Invalid()=>new("备份包含无效状态、重复标识或缺失关联，未导入；原数据已保留。");
}
