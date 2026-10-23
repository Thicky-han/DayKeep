using System.Security.Cryptography;
using System.Text.Json;
using DayKeep.Core;
using Microsoft.Data.Sqlite;

internal static class DataChecks
{
    public static void Run(Action<string,Action> test,string directory)
    {
        var number=0;
        void Case(string name,Action<Fixture> action)=>test("L5 "+name,()=>action(new(Path.Combine(directory,$"data-{++number}"))));
        Case("export includes full logical tables and validates checksum",f=> {f.Populate();var before=f.Source.Capture();f.Export();var file=BackupFile.Read(f.File);Assert(file is {Version:1,SchemaVersion:5} && BackupFile.Hash(file.Data)==BackupFile.Hash(before) && BackupFile.Hash(f.Source.Capture())==BackupFile.Hash(before));});
        Case("preview is read-only and reports merge and replacement counts",f=> {f.Populate();f.Export();f.Target.Create(new("local",TaskKind.Todo));var before=File.ReadAllBytes(f.Target.Repository.DatabasePath);var p=f.Target.PreviewImport(f.File);Assert(p.IncomingTasks==3 && p.NewTasks==3 && p.RemovedByReplace==1 && before.SequenceEqual(File.ReadAllBytes(f.Target.Repository.DatabasePath)));});
        Case("replacement restores states undo snapshots schedules budgets and outbox",f=>
        {
            f.Populate();f.Export();var local=f.Target.Create(new("local only",TaskKind.Todo));var before=f.Target.Capture();var original=f.Source.Capture();
            var result=f.Target.ImportBackup(f.Target.PreviewImport(f.File),ImportMode.Replace);var restored=f.Target.Capture();
            Assert(result.ImportedTasks==3 && restored.Tasks.All(t=>t.Id!=local.Id) && BackupFile.Hash(BackupFile.Read(result.RecoveryPath).Data)==BackupFile.Hash(before));
            foreach(var task in original.Tasks) Assert(restored.Tasks.Single(t=>t.Id==task.Id) with {Revision=task.Revision}==task);
            Assert(restored.Checks.SequenceEqual(original.Checks) && restored.Days.SequenceEqual(original.Days));
            foreach(var progress in original.Progress) Assert(restored.Progress.Single(p=>p.TaskId==progress.TaskId) with {Version=progress.Version}==progress);
            foreach(var delivery in original.Deliveries) Assert(restored.Deliveries.Single(d=>d.Key==delivery.Key) with {TaskRevision=delivery.TaskRevision}==delivery);
            var completed=restored.Tasks.Single(t=>t.State==TaskState.Completed);Assert(f.Target.UndoComplete(completed).Paused);
        });
        Case("merge keeps same-ID local tasks and histories while adding new groups",f=>
        {
            f.Populate();f.Export();f.Target.ImportBackup(f.Target.PreviewImport(f.File),ImportMode.Replace);
            var shared=f.Target.LoadAll().First(t=>ReminderPlanner.Active(t));f.Target.Edit(shared,new("local edit kept",shared.Kind));var before=f.Target.Capture();
            var added=f.Source.Create(new("new source task",TaskKind.Todo));f.Export("second.json");var preview=f.Target.PreviewImport(f.File);
            Assert(preview.NewTasks==1 && preview.KeptIds==3);f.Target.ImportBackup(preview,ImportMode.Merge);var after=f.Target.Capture();
            Assert(before.Tasks.All(t=>after.Tasks.Contains(t)) && before.Events.All(e=>after.Events.Contains(e)) && after.Tasks.Any(t=>t.Id==added.Id));
        });
        Case("repeated merge is idempotent for all logical records",f=> {f.Populate();f.Export();f.Target.ImportBackup(f.Target.PreviewImport(f.File),ImportMode.Merge);var before=BackupFile.Hash(f.Target.Capture());var result=f.Target.ImportBackup(f.Target.PreviewImport(f.File),ImportMode.Merge);Assert(result.ImportedTasks==0 && BackupFile.Hash(f.Target.Capture())==before);});
        Case("an old form cannot overwrite restored or removed-and-readded IDs",f=>
        {
            var old=f.Target.Create(new("old reference",TaskKind.Todo));var backup=Path.Combine(f.Root,"old.json");f.Target.ExportBackup(backup);
            f.Target.ImportBackup(f.Target.PreviewImport(backup),ImportMode.Replace);Throws<TaskConflictException>(()=>f.Target.Edit(old,new("stale",TaskKind.Todo)));
            f.Export();f.Target.ImportBackup(f.Target.PreviewImport(f.File),ImportMode.Replace);Assert(f.Target.LoadAll().Count==0);
            f.Target.ImportBackup(f.Target.PreviewImport(backup),ImportMode.Replace);Throws<TaskConflictException>(()=>f.Target.Complete(old));Assert(f.Target.LoadAll().Single().Title==old.Title);
        });
        Case("preview rejects edits saved before confirmation",f=> {f.Populate();f.Export();var t=f.Target.Create(new("before",TaskKind.Todo));var p=f.Target.PreviewImport(f.File);f.Target.Edit(t,new("after",TaskKind.Todo));var hash=BackupFile.Hash(f.Target.Capture());Throws<TaskValidationException>(()=>f.Target.ImportBackup(p,ImportMode.Replace));Assert(BackupFile.Hash(f.Target.Capture())==hash && !Directory.Exists(Path.Combine(f.Root,"target","Recovery")));});
        Case("preview rejects timer delivery or dismissal changes",f=>
        {
            f.Export();var t=f.Target.Create(new("timer",TaskKind.Todo));var p=f.Target.PreviewImport(f.File);f.Now=f.Now.AddMinutes(1);
            var snapshot=f.Target.ReminderSnapshot(f.Date);var due=ReminderPlanner.Due(snapshot,f.Now,f.Now,TimeZoneInfo.Utc,true).Single();f.Target.QueueDelivery(due,null,f.Now,TimeZoneInfo.Utc);
            Throws<TaskValidationException>(()=>f.Target.ImportBackup(p,ImportMode.Replace));p=f.Target.PreviewImport(f.File);f.Target.SetDeliveryState([due.Key],DeliveryState.Dismissed);Throws<TaskValidationException>(()=>f.Target.ImportBackup(p,ImportMode.Replace));Assert(f.Target.LoadAll().Single()==t);
        });
        Case("confirmation uses the exact frozen file that was previewed",f=> {f.Source.Create(new("previewed",TaskKind.Todo));f.Export();var p=f.Target.PreviewImport(f.File);File.WriteAllText(f.File,"changed after preview");f.Target.ImportBackup(p,ImportMode.Replace);Assert(f.Target.LoadAll().Single().Title=="previewed");});
        Case("failed SQL replacement rolls back every table and leaves verified recovery",f=> {f.Populate();f.Export();f.Target.Create(new("preserve",TaskKind.Todo));f.Sql(f.Target,"CREATE TRIGGER reject_import BEFORE INSERT ON tasks BEGIN SELECT RAISE(ABORT,'controlled'); END;");var before=f.Target.Capture();Throws<SqliteException>(()=>f.Target.ImportBackup(f.Target.PreviewImport(f.File),ImportMode.Replace));Assert(BackupFile.Hash(f.Target.Capture())==BackupFile.Hash(before));var recovery=Directory.GetFiles(Path.Combine(f.Root,"target","Recovery")).Single();Assert(BackupFile.Hash(BackupFile.Read(recovery).Data)==BackupFile.Hash(before));});
        Case("failed recovery-file write prevents any import mutation",f=> {f.Populate();f.Export();f.Target.Create(new("preserve",TaskKind.Todo));File.WriteAllText(Path.Combine(f.Root,"target","Recovery"),"blocked folder");var hash=BackupFile.Hash(f.Target.Capture());Throws<IOException>(()=>f.Target.ImportBackup(f.Target.PreviewImport(f.File),ImportMode.Replace));Assert(BackupFile.Hash(f.Target.Capture())==hash);});
        Case("read-only and locked stores reject import without data loss",f=>
        {
            f.Populate();f.Export();f.Target.Create(new("preserve",TaskKind.Todo));var before=BackupFile.Hash(f.Target.Capture());
            var readOnly=new TaskService(new TaskRepository(f.Target.Repository.DatabasePath,true));readOnly.Initialize();Throws<SqliteException>(()=>readOnly.ImportBackup(readOnly.PreviewImport(f.File),ImportMode.Replace));
            var p=f.Target.PreviewImport(f.File);using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=f.Target.Repository.DatabasePath,Pooling=false}.ToString()))
            {c.Open();using var tx=c.BeginTransaction();Throws<SqliteException>(()=>f.Target.ImportBackup(p,ImportMode.Replace));tx.Rollback();}
            Assert(BackupFile.Hash(f.Target.Capture())==before);
        });
        Case("merge identity collisions are visible before applying and do not write",f=> {var target=f.Target.Create(new("local",TaskKind.Todo));f.Source.Create(new("incoming",TaskKind.Todo));var data=f.Source.Capture();data=data with {Events=[data.Events.Single() with {Id=f.Target.Capture().Events.Single().Id}]};BackupFile.Write(f.File,data,f.Now);var p=f.Target.PreviewImport(f.File);Assert(p.MergeError is not null);var hash=BackupFile.Hash(f.Target.Capture());Throws<TaskValidationException>(()=>f.Target.ImportBackup(p,ImportMode.Merge));Assert(BackupFile.Hash(f.Target.Capture())==hash && f.Target.LoadAll().Single()==target);});
        Case("existing export files and the live database are never overwritten",f=> {var t=f.Source.Create(new("preserve",TaskKind.Todo));File.WriteAllText(f.File,"original");Throws<IOException>(()=>f.Source.ExportBackup(f.File));Throws<IOException>(()=>f.Source.ExportBackup(f.Source.Repository.DatabasePath));Assert(File.ReadAllText(f.File)=="original" && f.Source.LoadAll().Single()==t);});
        Case("unsupported version truncation and changed checksum are rejected",f=>
        {
            f.Source.Create(new("source",TaskKind.Todo));f.Export();var envelope=BackupFile.Read(f.File);var hash=BackupFile.Hash(f.Target.Capture());
            foreach(var content in new[]{"{",JsonSerializer.Serialize(envelope with {Version=99},Json),JsonSerializer.Serialize(envelope with {Sha256=new string('0',64)},Json)}) {File.WriteAllText(f.File,content);Throws<TaskValidationException>(()=>f.Target.PreviewImport(f.File));}
            Assert(BackupFile.Hash(f.Target.Capture())==hash);
        });
        Case("duplicate JSON properties null lists and unknown fields are rejected",f=>
        {
            f.Export();var text=File.ReadAllText(f.File);foreach(var content in new[]{text.Replace("\"version\": 1","\"version\": 1, \"version\": 1"),text.Replace("\"tasks\": []","\"tasks\": null"),text.Replace("\"version\": 1","\"version\": 1, \"unknown\": true")}) {File.WriteAllText(f.File,content);Throws<TaskValidationException>(()=>f.Target.PreviewImport(f.File));}Assert(f.Target.LoadAll().Count==0);
        });
        Case("oversized file is rejected before deserialization",f=> {using(var file=new FileStream(f.File,FileMode.CreateNew))file.SetLength(BackupFile.MaxBytes+1);Throws<TaskValidationException>(()=>f.Target.PreviewImport(f.File));Assert(f.Target.LoadAll().Count==0);});
        Case("invalid states URLs and relational references are rejected despite matching hashes",f=>
        {
            var t=f.Source.Create(new("valid",TaskKind.Todo));var data=f.Source.Capture();
            foreach(var invalid in new[]{data with {Tasks=[t with {State=TaskState.Completed}]},data with {Tasks=[t with {Url="javascript:alert(1)"}]},data with {Tasks=[t,t]},data with {Checks=[new(Guid.NewGuid(),Guid.NewGuid(),f.Date,f.Now,"")]},data with {Days=[new(Guid.NewGuid(),f.Date,0,null)]}})
            {var checksum=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(invalid,Json)));File.WriteAllText(f.File,JsonSerializer.Serialize(new BackupEnvelope("daykeep-backup",1,5,f.Now,invalid,checksum),Json));Throws<TaskValidationException>(()=>f.Target.PreviewImport(f.File));}
            Assert(f.Target.LoadAll().Count==0);
        });
        Case("history filters by task date text and keeps check distinct from completion",f=>
        {
            var t=f.Source.Create(new("专利缴费",TaskKind.WaitingForCondition));f.Now=f.Now.AddMinutes(1);t=f.Source.RecordWaitingCheck(t,f.Date,"入口尚未开放");f.Now=f.Now.AddDays(1);t=f.Source.MarkReady(t,f.Date);f.Now=f.Now.AddMinutes(1);f.Source.Complete(t);
            var entries=f.Source.History(new(TaskId:t.Id),TimeZoneInfo.Utc).Entries;Assert(entries.Count(e=>e.Kind=="check-waiting")==1 && entries.Count(e=>e.Kind=="check-ready")==1 && entries.Count(e=>e.Kind=="completed")==1);
            var first=new DateOnly(2026,10,26);Assert(f.Source.History(new("尚未开放",t.Id,first,first),TimeZoneInfo.Utc).Entries.Single().Notes=="入口尚未开放");Assert(f.Source.History(new(TaskId:Guid.NewGuid()),TimeZoneInfo.Utc).Entries.Count==0);Throws<TaskValidationException>(()=>f.Source.History(new(From:first.AddDays(1),To:first),TimeZoneInfo.Utc));
        });
        Case("history reports truncation and can narrow to archived task",f=> {var t=f.Source.Create(new("history",TaskKind.Todo));for(var i=0;i<201;i++)t=f.Source.Edit(t,new("history",TaskKind.Todo));var result=f.Source.History(new("编辑事项"),TimeZoneInfo.Utc);Assert(result.HasMore && result.Entries.Count==200);t=f.Source.Archive(t);Assert(f.Source.History(new("归档",t.Id),TimeZoneInfo.Utc).Entries.Count==1);});
        Case("localized operation search filters before truncation and keeps canonical queries",f=>
        {
            var t=f.Source.Create(new("Original task title",TaskKind.Todo));
            for(var i=0;i<205;i++) {f.Now=f.Now.AddMinutes(1);t=f.Source.Edit(t,new("Original task title",TaskKind.Todo));}
            var labels=new Dictionary<string,string> {{"创建事项","Task created"},{"编辑事项","Task edited"}};var before=BackupFile.Hash(f.Source.Capture());
            var created=f.Source.History(new("CREAT",ActionLabels:labels),TimeZoneInfo.Utc);
            Assert(created.Entries.Single().Kind=="created" && !created.HasMore);
            var edited=f.Source.History(new("EDIT",ActionLabels:labels),TimeZoneInfo.Utc);
            Assert(edited.Entries.Count==200 && edited.HasMore && edited.Entries.All(e=>e.Kind=="edited"));
            Assert(f.Source.History(new("创建事项",ActionLabels:labels),TimeZoneInfo.Utc).Entries.Single().Id==created.Entries.Single().Id);
            var french=new Dictionary<string,string> {{"创建事项","Tâche créée"},{"编辑事项","Tâche modifiée"}};
            Assert(f.Source.History(new("cré",ActionLabels:french),TimeZoneInfo.Utc).Entries.Single().Id==created.Entries.Single().Id);
            Assert(f.Source.History(new("Original task title",ActionLabels:french),TimeZoneInfo.Utc) is {HasMore:true,Entries.Count:200} && BackupFile.Hash(f.Source.Capture())==before);
        });
        Case("localized history search combines task dates and unchanged title or note text",f=>
        {
            var day=f.Date;var t=f.Source.Create(new("创建事项",TaskKind.WaitingForCondition));
            f.Now=f.Now.AddMinutes(1);t=f.Source.RecordWaitingCheck(t,day,"Created item 原文 à garder");
            var other=f.Source.Create(new("other task",TaskKind.WaitingForCondition));f.Source.RecordWaitingCheck(other,day,"Created item in another task");
            f.Now=f.Now.AddDays(1);f.Source.RecordWaitingCheck(t,f.Date,"第二日原文");
            var labels=new Dictionary<string,string> {{"创建事项","Tâche créée"},{"今日已检查，尚未开放","Vérification faite, toujours indisponible"}};
            var before=BackupFile.Hash(f.Source.Capture());var query=new HistoryQuery("VÉRIF",t.Id,day,day,labels);
            var checkedEntry=f.Source.History(query,TimeZoneInfo.Utc);
            Assert(!checkedEntry.HasMore && checkedEntry.Entries.Single() is {Kind:"check-waiting",Title:"创建事项",Notes:"Created item 原文 à garder"});
            Assert(f.Source.History(query with {Text="Created item"},TimeZoneInfo.Utc).Entries.Single().Id==checkedEntry.Entries.Single().Id);
            Assert(f.Source.History(query with {Text="创建事项"},TimeZoneInfo.Utc).Entries.Count==2);
            Assert(f.Source.History(query with {From=day.AddDays(2),To=day.AddDays(2)},TimeZoneInfo.Utc).Entries.Count==0 && BackupFile.Hash(f.Source.Capture())==before);
        });
        Case("schema-4 upgrade retains full prior data and protects stale restores",f=> {f.Populate();var before=f.Source.Capture();f.Sql(f.Source,"DROP TABLE app_metadata; PRAGMA user_version=4;");f.Source.Initialize();var after=f.Source.Capture();Assert(after.Tasks.SequenceEqual(before.Tasks) && after.Checks.SequenceEqual(before.Checks) && after.Deliveries.SequenceEqual(before.Deliveries));});
        Case("failed schema-4 upgrade preserves byte content",f=> {f.Source.Create(new("preserve",TaskKind.Todo));f.Sql(f.Source,"PRAGMA user_version=4;");var bytes=File.ReadAllBytes(f.Source.Repository.DatabasePath);Throws<SqliteException>(f.Source.Initialize);Assert(bytes.SequenceEqual(File.ReadAllBytes(f.Source.Repository.DatabasePath)));});
        Case("patent scenario checks daily then handles completes undoes and restores backup",f=>
        {
            var task=f.Source.Create(new("专利缴费入口",TaskKind.WaitingForCondition,"https://example.org/patent"));
            void Queue() {var snapshot=f.Source.ReminderSnapshot(f.Date);var due=ReminderPlanner.Due(snapshot,f.Now,f.Now,TimeZoneInfo.Utc,true).Single();f.Source.QueueDelivery(due,snapshot.Progress.SingleOrDefault(),f.Now,TimeZoneInfo.Utc);}
            f.Now=f.Now.AddMinutes(1);Queue();var key=f.Source.ReminderSnapshot(f.Date).Deliveries.Single().Key;f.Source.SetDeliveryState([key],DeliveryState.Dismissed);f.Now=f.Now.AddMinutes(30);Queue();
            task=f.Source.RecordWaitingCheck(task,f.Date,"尚未开放");Assert(ReminderPlanner.Due(f.Source.ReminderSnapshot(f.Date),f.Now.AddHours(1),f.Now,TimeZoneInfo.Utc,true).Count==0);
            f.Now=new(2026,10,27,9,0,0,TimeSpan.Zero);Queue();task=f.Source.MarkReady(task,f.Date);Assert(task.CompletedAt is null);task=f.Source.Snooze(task,f.Date,10);f.Now=f.Now.AddMinutes(10);Queue();task=f.Source.Complete(task);task=f.Source.UndoComplete(task);task=f.Source.TogglePause(task);task=f.Source.Archive(task);task=f.Source.Restore(task);
            f.Export();f.Target.ImportBackup(f.Target.PreviewImport(f.File),ImportMode.Replace);var restored=f.Target.LoadAll().Single();Assert(restored.State==TaskState.Ready && restored.Paused && f.Target.History(new(),TimeZoneInfo.Utc).Entries.Any(e=>e.Kind=="check-waiting"));
        });
    }
    private static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    private static void Assert(bool value) {if(!value)throw new InvalidOperationException("L5 data expectation failed");}
    private static void Throws<T>(Action action) where T:Exception {try {action();}catch(T){return;}throw new InvalidOperationException($"Expected {typeof(T).Name}");}
    private sealed class Fixture
    {
        public string Root {get;} public string File {get;private set;} public TaskService Source {get;} public TaskService Target {get;}
        public DateTimeOffset Now {get;set;}=new(2026,10,26,8,59,0,TimeSpan.Zero);
        public DateOnly Date=>DateOnly.FromDateTime(Now.DateTime);
        public Fixture(string root) {Root=root;Directory.CreateDirectory(root);File=Path.Combine(root,"backup.json");Source=new(new TaskRepository(Path.Combine(root,"source","tasks.db")),()=>Now);Target=new(new TaskRepository(Path.Combine(root,"target","tasks.db")),()=>Now);Source.Initialize();Target.Initialize();}
        public void Export(string? name=null) {if(name is not null)File=Path.Combine(Root,name);Source.ExportBackup(File);}
        public void Populate()
        {
            var t=Source.Create(new("缴费入口",TaskKind.WaitingForCondition,"https://example.org/patent","测试备注\n第二行",DueDate:"2026-10-30"));Now=Now.AddMinutes(1);
            var due=ReminderPlanner.Due(Source.ReminderSnapshot(Date),Now,Now,TimeZoneInfo.Utc,true).Single();Source.QueueDelivery(due,null,Now,TimeZoneInfo.Utc);
            t=Source.RecordWaitingCheck(t,Date,"尚未开放");Now=Now.AddMinutes(5);t=Source.MarkReady(t,Date);t=Source.Snooze(t,Date,30);Now=Now.AddMinutes(30);
            var snapshot=Source.ReminderSnapshot(Date);Source.QueueDelivery(ReminderPlanner.Due(snapshot,Now,Now,TimeZoneInfo.Utc,true).Single(),snapshot.Progress.Single(),Now,TimeZoneInfo.Utc);
            Source.Complete(Source.TogglePause(Source.Create(new("可撤销完成",TaskKind.Todo))));Source.Archive(Source.Create(new("归档事项",TaskKind.WaitingForCondition)));
        }
        public void Sql(TaskService service,string sql) {using var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=service.Repository.DatabasePath,Pooling=false}.ToString());c.Open();using var command=c.CreateCommand();command.CommandText=sql;command.ExecuteNonQuery();}
    }
}
