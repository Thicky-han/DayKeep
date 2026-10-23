using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DayKeep.Core;

public sealed partial class TaskRepository
{
    public DataSnapshot Capture()
    {
        using var connection=Open();using var transaction=connection.BeginTransaction(deferred:true);
        var data=Capture(connection,transaction);transaction.Commit();return data;
    }
    private static DataSnapshot Capture(SqliteConnection connection,SqliteTransaction transaction)
    {
        var tasks=new List<TaskItem>();var events=new List<TaskEvent>();var checks=new List<TaskCheck>();
        var progress=new List<ReminderProgress>();var days=new List<DayBudget>();var deliveries=new List<ReminderDelivery>();
        void Rows(string sql,Action<SqliteDataReader> read)
        {using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText=sql;using var reader=command.ExecuteReader();while(reader.Read()) {read(reader);if((long)tasks.Count+events.Count+checks.Count+progress.Count+days.Count+deliveries.Count>BackupFile.MaxRecords) throw new TaskValidationException("完整数据超过备份记录上限，请保留原任务库。");}}
        Rows("SELECT * FROM tasks;",r=>tasks.Add(Read(r)));
        Rows("SELECT id,task_id,kind,recorded_at FROM task_events;",r=>events.Add(new(Guid.Parse(r.GetString(0)),Guid.Parse(r.GetString(1)),r.GetString(2),DateTimeOffset.Parse(r.GetString(3),CultureInfo.InvariantCulture))));
        Rows("SELECT * FROM task_checks;",r=>checks.Add(ReadCheck(r)));
        Rows("SELECT * FROM reminder_progress;",r=>progress.Add(ReadProgress(r)));
        Rows("SELECT task_id,local_date,extra_count,covered_at FROM reminder_days;",r=>days.Add(new(Guid.Parse(r.GetString(0)),DateOnly.ParseExact(r.GetString(1),"yyyy-MM-dd",CultureInfo.InvariantCulture),r.GetInt32(2),r.IsDBNull(3) ? null : DateTimeOffset.Parse(r.GetString(3),CultureInfo.InvariantCulture))));
        Rows("SELECT task_id,task_revision,local_date,local_time,kind,scheduled_at,queued_at,status,generation FROM reminder_deliveries;",r=>
        {
            var at=DateTimeOffset.Parse(r.GetString(5),CultureInfo.InvariantCulture);var kind=(ReminderKind)r.GetInt32(4);
            deliveries.Add(new(new(Guid.Parse(r.GetString(0)),DateOnly.ParseExact(r.GetString(2),"yyyy-MM-dd",CultureInfo.InvariantCulture),TimeOnly.ParseExact(r.GetString(3),"HH:mm:ss.fffffff",CultureInfo.InvariantCulture),kind,r.GetInt32(8),kind==ReminderKind.Fixed ? 0 : at.UtcTicks),r.GetInt32(1),at,DateTimeOffset.Parse(r.GetString(6),CultureInfo.InvariantCulture),(DeliveryState)r.GetInt32(7)));
        });
        using var meta=connection.CreateCommand();meta.Transaction=transaction;meta.CommandText="SELECT revision_floor FROM app_metadata WHERE id=1;";
        if(meta.ExecuteScalar() is not long floor || floor<0 || floor>=int.MaxValue-1) throw new StoreVersionException("任务库修订信息无法识别，原数据已保留。");
        return Canonical(new(tasks,events,checks,progress,days,deliveries,(int)floor));
    }
    internal static DataSnapshot Canonical(DataSnapshot data)=>data with
    {
        Tasks=data.Tasks.OrderBy(t=>t.Id).ToArray(),Events=data.Events.OrderBy(t=>t.Id).ToArray(),Checks=data.Checks.OrderBy(t=>t.Id).ToArray(),
        Progress=data.Progress.OrderBy(t=>t.TaskId).ToArray(),Days=data.Days.OrderBy(t=>t.TaskId).ThenBy(t=>t.LocalDate).ToArray(),Deliveries=data.Deliveries.OrderBy(d=>KeyId(d.Key),StringComparer.Ordinal).ToArray()
    };
    internal static DataSnapshot PrepareImport(DataSnapshot current,DataSnapshot source,ImportMode mode,DateTimeOffset now)
    {
        if(!Enum.IsDefined(mode)) throw new TaskValidationException("请选择合并或替换。");
        var currentIds=current.Tasks.Select(t=>t.Id).ToHashSet();
        var importedIds=source.Tasks.Where(t=>mode==ImportMode.Replace || !currentIds.Contains(t.Id)).Select(t=>t.Id).ToHashSet();
        if(mode==ImportMode.Merge && importedIds.Count==0) return current;
        var maximum=new[]{current.RevisionFloor,source.RevisionFloor}
            .Concat(current.Tasks.Select(t=>t.Revision)).Concat(source.Tasks.Select(t=>t.Revision))
            .Concat(current.Progress.Select(t=>t.Version)).Concat(source.Progress.Select(t=>t.Version))
            .Concat(current.Deliveries.Select(d=>d.TaskRevision)).Concat(source.Deliveries.Select(d=>d.TaskRevision)).Max();
        if(maximum>=int.MaxValue-2) throw new TaskValidationException("修订号过大，无法安全导入。");
        var revision=maximum+1;
        var tasks=source.Tasks.Where(t=>importedIds.Contains(t.Id)).Select(t=>t with {Revision=revision}).ToList();
        var events=source.Events.Where(t=>importedIds.Contains(t.TaskId)).ToList();
        events.AddRange(tasks.Select(t=>new TaskEvent(Guid.NewGuid(),t.Id,"backup-imported",now)));
        var checks=source.Checks.Where(t=>importedIds.Contains(t.TaskId)).ToList();
        var progress=source.Progress.Where(t=>importedIds.Contains(t.TaskId)).Select(p=>p with {Version=revision}).ToList();
        var days=source.Days.Where(t=>importedIds.Contains(t.TaskId)).ToList();
        var deliveries=source.Deliveries.Where(t=>importedIds.Contains(t.Key.TaskId)).Select(d=>d with {TaskRevision=revision}).ToList();
        if(mode==ImportMode.Merge) {tasks.AddRange(current.Tasks);events.AddRange(current.Events);checks.AddRange(current.Checks);progress.AddRange(current.Progress);days.AddRange(current.Days);deliveries.AddRange(current.Deliveries);}
        var data=Canonical(new(tasks,events,checks,progress,days,deliveries,revision));BackupFile.ValidateData(data);return data;
    }
    public ImportResult ApplyImport(ImportPreview preview,ImportMode mode,DateTimeOffset now,Func<DataSnapshot,string> saveRecovery)
    {
        using var connection=Open();using var transaction=connection.BeginTransaction();
        var current=Capture(connection,transaction);
        if(BackupFile.Hash(current)!=preview.TargetHash) throw new TaskValidationException("预览后本机数据发生变化，请重新选择备份并核对预览；本次未导入。");
        BackupFile.Validate(preview.Source);
        var data=PrepareImport(current,preview.Source.Data,mode,now);
        var recovery=saveRecovery(current);
        if(mode==ImportMode.Merge && preview.NewTasks==0) {transaction.Commit();return new(0,recovery);}
        using var clear=connection.CreateCommand();clear.Transaction=transaction;
        clear.CommandText="DELETE FROM reminder_deliveries;DELETE FROM reminder_days;DELETE FROM reminder_progress;DELETE FROM task_checks;DELETE FROM task_events;DELETE FROM tasks;";clear.ExecuteNonQuery();
        void Write(string sql,Action<SqliteCommand> bind)
        {using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText=sql;bind(command);command.ExecuteNonQuery();}
        foreach(var t in data.Tasks) Write("INSERT INTO tasks (id,revision,title,kind,state,paused,url,notes,times,weekdays,repeat_minutes,max_extra,due_date,created_at,updated_at,completed_at,before_completion,paused_before_completion,archived_at) VALUES ($id,$revision,$title,$kind,$state,$paused,$url,$notes,$times,$weekdays,$repeat,$max,$due,$created,$updated,$completed,$before,$wasPaused,$archived);",c=>BindTask(c,t));
        foreach(var e in data.Events) Write("INSERT INTO task_events VALUES ($id,$task,$kind,$at);",c=> {Add(c,"$id",e.Id.ToString());Add(c,"$task",e.TaskId.ToString());Add(c,"$kind",e.Kind);Add(c,"$at",Stamp(e.RecordedAt));});
        foreach(var c in data.Checks) Write("INSERT INTO task_checks(id,task_id,local_date,recorded_at,notes,outcome) VALUES ($id,$task,$date,$at,$notes,$outcome);",s=> {Add(s,"$id",c.Id.ToString());Add(s,"$task",c.TaskId.ToString());Add(s,"$date",Date(c.LocalDate));Add(s,"$at",Stamp(c.RecordedAt));Add(s,"$notes",c.Notes);Add(s,"$outcome",(int)c.Outcome);});
        foreach(var p in data.Progress) Write("INSERT INTO reminder_progress VALUES ($task,$date,$count,$next,$snooze,$version,$last);",c=> {Add(c,"$task",p.TaskId.ToString());Add(c,"$date",Date(p.LocalDate));Add(c,"$count",p.ExtraCount);Add(c,"$next",OptionalStamp(p.NextRepeatAt));Add(c,"$snooze",OptionalStamp(p.SnoozedUntil));Add(c,"$version",p.Version);Add(c,"$last",OptionalStamp(p.LastDeliveredAt));});
        foreach(var d in data.Days) Write("INSERT INTO reminder_days VALUES ($task,$date,$count,$covered);",c=> {Add(c,"$task",d.TaskId.ToString());Add(c,"$date",Date(d.LocalDate));Add(c,"$count",d.ExtraCount);Add(c,"$covered",OptionalStamp(d.CoveredAt));});
        foreach(var d in data.Deliveries) Write("INSERT INTO reminder_deliveries VALUES ($id,$task,$revision,$date,$time,$kind,$generation,$scheduled,$queued,$status);",c=> {Add(c,"$id",KeyId(d.Key));Add(c,"$task",d.Key.TaskId.ToString());Add(c,"$revision",d.TaskRevision);Add(c,"$date",Date(d.Key.Date));Add(c,"$time",d.Key.Time.ToString("HH:mm:ss.fffffff",CultureInfo.InvariantCulture));Add(c,"$kind",(int)d.Key.Kind);Add(c,"$generation",d.Key.Generation);Add(c,"$scheduled",Stamp(d.ScheduledAt));Add(c,"$queued",Stamp(d.QueuedAt));Add(c,"$status",(int)d.State);});
        Write("UPDATE app_metadata SET revision_floor=$floor WHERE id=1;",c=>Add(c,"$floor",data.RevisionFloor));
        transaction.Commit();return new(mode==ImportMode.Merge ? preview.NewTasks : preview.IncomingTasks,recovery);
    }
    private static string Date(DateOnly date)=>date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
    private static string? OptionalStamp(DateTimeOffset? at)=>at is { } value ? Stamp(value) : null;

    public HistoryResult LoadHistory(HistoryQuery query,TimeZoneInfo zone)
    {
        if(query.Text is null || query.Text.Length>120 || (query.From is { } from && query.To is { } to && from>to)) throw new TaskValidationException("请检查查询文字和起止日期。");
        using var connection=Open();using var command=connection.CreateCommand();
        command.CommandText="""
            SELECT * FROM (
                SELECT e.id,e.task_id,t.title,e.kind,e.recorded_at,NULL AS check_date,'' AS notes FROM task_events e JOIN tasks t ON t.id=e.task_id
                    WHERE e.kind NOT IN ('checked-still-waiting','condition-ready')
                UNION ALL
                SELECT c.id,c.task_id,t.title,CASE c.outcome WHEN 0 THEN 'check-waiting' ELSE 'check-ready' END,c.recorded_at,c.local_date,c.notes FROM task_checks c JOIN tasks t ON t.id=c.task_id
            ) WHERE ($task IS NULL OR task_id=$task) ORDER BY julianday(recorded_at) DESC,id;
            """;
        Add(command,"$task",query.TaskId?.ToString());
        using var reader=command.ExecuteReader();var entries=new List<HistoryEntry>();var text=query.Text.Trim();
        while(reader.Read())
        {
            var at=DateTimeOffset.Parse(reader.GetString(4),CultureInfo.InvariantCulture);
            var date=reader.IsDBNull(5) ? ReminderPlanner.LocalDate(at,zone) : DateOnly.ParseExact(reader.GetString(5),"yyyy-MM-dd",CultureInfo.InvariantCulture);
            if(query.From is { } start && date<start || query.To is { } end && date>end) continue;
            var entry=new HistoryEntry(Guid.Parse(reader.GetString(0)),Guid.Parse(reader.GetString(1)),reader.GetString(2),reader.GetString(3),at,date,reader.GetString(6));
            // Filter every matching field before applying the result cap, including caller-provided display labels.
            if(text.Length>0 && !entry.Title.Contains(text,StringComparison.OrdinalIgnoreCase)
                && !entry.Action.Contains(text,StringComparison.OrdinalIgnoreCase)
                && query.ActionLabels?.GetValueOrDefault(entry.Action)?.Contains(text,StringComparison.OrdinalIgnoreCase)!=true
                && !entry.Notes.Contains(text,StringComparison.OrdinalIgnoreCase)) continue;
            entries.Add(entry);if(entries.Count>200) break;
        }
        return new(entries.Take(200).ToArray(),entries.Count>200);
    }
}
