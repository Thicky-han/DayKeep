using System.Text.Json;
using DayKeep.Core;
using Microsoft.Data.Sqlite;

var directory = Path.GetFullPath(args[0]);
Directory.CreateDirectory(directory);
TaskService Open(string name = "tasks.db", bool readOnly = false)
{
    var service = new TaskService(new TaskRepository(Path.Combine(directory, name), readOnly));
    service.Initialize();
    return service;
}
void Sql(string path, string sql)
{
    using var connection = new SqliteConnection($"Data Source={path};Pooling=False;Foreign Keys=True");
    connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
}
long Count(string path, string table)
{
    using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
    connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM " + table; return (long)command.ExecuteScalar()!;
}
static void Assert(bool result, string message = "Unexpected result") { if (!result) throw new InvalidOperationException(message); }
static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}"); }

if (args.Length > 1 && args[1] == "--write-restart")
{
    var service = Open("restart.db");
    var item = service.Create(new("跨进程验证事项", TaskKind.WaitingForCondition, "https://example.org/patent", "仅为合成测试", "14:30, 09:00", 31, "2026-12-01"));
    item = service.TogglePause(item);
    service.Complete(item);
    var daily=new TaskService(new TaskRepository(Path.Combine(directory,"daily-restart.db")),()=>new DateTimeOffset(2026,10,22,8,59,0,TimeSpan.Zero));
    daily.Initialize();
    daily.RecordWaitingCheck(daily.Create(new("重启后已检查",TaskKind.WaitingForCondition)),new(2026,10,22),"still waiting");
    daily.MarkReady(daily.Create(new("重启后待办理",TaskKind.WaitingForCondition)),new(2026,10,22));
    var reminderTime=new DateTimeOffset(2026,10,22,8,59,0,TimeSpan.Zero);
    var delayed=new TaskService(new TaskRepository(Path.Combine(directory,"snooze-restart.db")),()=>reminderTime); delayed.Initialize();
    var delayedTask=delayed.Create(new("跨进程延期",TaskKind.WaitingForCondition,RepeatMinutes:30,MaxExtraReminders:3));
    foreach(var time in new[]{new DateTimeOffset(2026,10,22,9,0,0,TimeSpan.Zero),new(2026,10,22,9,30,0,TimeSpan.Zero)})
    {
        reminderTime=time; var snapshot=delayed.ReminderSnapshot(new(2026,10,22));
        delayed.RecordDelivery(ReminderPlanner.Due(snapshot,time,time.AddHours(-1),TimeZoneInfo.Utc).Single(),snapshot.Progress.SingleOrDefault(),time,TimeZoneInfo.Utc);
    }
    reminderTime=reminderTime.AddMinutes(1); delayed.Snooze(delayedTask,new(2026,10,22),60);
    var durable=new TaskService(new TaskRepository(Path.Combine(directory,"outbox-restart.db")),()=>new DateTimeOffset(2026,10,22,8,59,0,TimeSpan.Zero));durable.Initialize();
    durable.Create(new("提交后尚未显示",TaskKind.Todo));durable.Create(new("显示后尚未收起",TaskKind.Todo));
    var queueAt=new DateTimeOffset(2026,10,22,9,0,0,TimeSpan.Zero);
    foreach(var occurrence in ReminderPlanner.Due(durable.ReminderSnapshot(new(2026,10,22)),queueAt,queueAt,TimeZoneInfo.Utc,true))
    { durable.QueueDelivery(occurrence,null,queueAt,TimeZoneInfo.Utc); if(occurrence.Task.Title=="显示后尚未收起") durable.SetDeliveryState([occurrence.Key],DeliveryState.Shown); }
    Console.WriteLine("Restart fixture written by process " + Environment.ProcessId);
    return;
}
if (args.Length > 1 && args[1] == "--read-restart")
{
    var service = Open("restart.db");
    var item = service.LoadAll().Single();
    Assert(item.Title == "跨进程验证事项" && item.State == TaskState.Completed && item.Reminder.Times == "09:00, 14:30" && item.Reminder.Weekdays == 31 && item.DueDate == new DateOnly(2026,12,1) && item.Url == "https://example.org/patent" && item.Notes == "仅为合成测试");
    item = service.UndoComplete(item);
    Assert(item.State == TaskState.Waiting && item.Paused && item.CompletedAt is null);
    var daily=Open("daily-restart.db");
    var snapshot=daily.ReminderSnapshot(new(2026,10,22));
    var due=ReminderPlanner.Due(snapshot,new DateTimeOffset(2026,10,22,9,0,0,TimeSpan.Zero),new DateTimeOffset(2026,10,22,8,59,0,TimeSpan.Zero),TimeZoneInfo.Utc);
    Assert(snapshot.Checks.Count==2 && due.Single().Task.Title=="重启后待办理" && due.Single().Task.State==TaskState.Ready);
    var delayed=Open("snooze-restart.db"); var saved=delayed.ReminderSnapshot(new(2026,10,22));
    var restored=saved.Progress.Single(); var postponed=new DateTimeOffset(2026,10,22,10,31,0,TimeSpan.Zero);
    Assert(restored.ExtraCount==1 && restored.SnoozedUntil==postponed && restored.NextRepeatAt is null);
    Assert(ReminderPlanner.Due(saved,postponed.AddMinutes(-1),postponed.AddMinutes(-1),TimeZoneInfo.Utc).Count==0);
    var postponedDue=ReminderPlanner.Due(saved,postponed.AddMinutes(9),postponed.AddMinutes(9),TimeZoneInfo.Utc).Single();
    Assert(postponedDue.Key.Kind==ReminderKind.Snooze && postponedDue.Task.State==TaskState.Waiting);
    delayed.RecordDelivery(postponedDue,restored,postponed.AddMinutes(9),TimeZoneInfo.Utc);
    Assert(delayed.ReminderSnapshot(new(2026,10,22)).Progress.Single() is { ExtraCount:1,SnoozedUntil:null } after && after.NextRepeatAt==postponed.AddMinutes(30));
    var durable=Open("outbox-restart.db");var durableSnapshot=durable.ReminderSnapshot(new(2026,10,22));
    Assert(durableSnapshot.Deliveries.Count==2 && durableSnapshot.Deliveries.Count(d=>d.State==DeliveryState.Pending)==1 && durableSnapshot.Deliveries.Count(d=>d.State==DeliveryState.Shown)==1);
    var deliveryKeys=durableSnapshot.Deliveries.Select(d=>d.Key).ToArray();
    durable.SetDeliveryState(deliveryKeys,DeliveryState.Dismissed);
    Assert(Open("outbox-restart.db").ReminderSnapshot(new(2026,10,22)).Deliveries.All(d=>d.State==DeliveryState.Dismissed));
    File.WriteAllText(Path.Combine(directory, "restart-report.json"), JsonSerializer.Serialize(new { passed = true, processId = Environment.ProcessId, scope = "Fresh process restored all task fields and undid completion with original pause state" }));
    Console.WriteLine("Cross-process tasks, snooze, budgets and L4 pending/shown/dismissed delivery restore passed in process " + Environment.ProcessId);
    return;
}

var results = new List<object>();
var failures = 0;
void Test(string name, Action test)
{
    try { test(); results.Add(new { name, passed = true }); Console.WriteLine("PASS " + name); }
    catch (Exception error) { failures++; results.Add(new { name, passed = false, error = error.ToString() }); Console.WriteLine("FAIL " + name + ": " + error.Message); }
}
var draft = new TaskDraft("查看缴费入口（合成）", TaskKind.WaitingForCondition);
Test("Fresh database is empty and schema initialization is idempotent", () => { var service = Open(); Assert(service.LoadAll().Count == 0); service.Initialize(); Assert(service.LoadAll().Count == 0); });
Test("Both task types start in their correct business states", () => { var service = Open(); Assert(service.Create(draft).State == TaskState.Waiting); Assert(service.Create(draft with { Kind = TaskKind.Todo }).State == TaskState.Ready); });
Test("Unknown due date and no URL roundtrip without invented data", () => { var service = Open(); var item = service.Create(draft); var stored = Open().LoadAll().Single(t => t.Id == item.Id); Assert(stored == item && stored.DueDate is null && stored.Url == ""); });
Test("Edit persists notes, URL, multiple sorted unique times, weekdays and date", () => { var service = Open(); var item = service.Create(draft); item = service.Edit(item, draft with { Title = "  修改后的事项  ", Url = "https://example.org/check?q=1", Notes = "第一行\n第二行", Times = "14:30，09:00,09:00", Weekdays = 31, DueDate = "2026-12-01" }); var stored = Open().LoadAll().Single(t => t.Id == item.Id); Assert(stored == item && stored.Title == "修改后的事项" && stored.Reminder.Times == "09:00, 14:30" && stored.Revision == 2); });
Test("Changing kind updates state without completing the task", () => { var service = Open(); var item = service.Create(draft); item = service.Edit(item, draft with { Kind = TaskKind.Todo }); Assert(item.State == TaskState.Ready && item.CompletedAt is null); item = service.Edit(item, draft); Assert(item.State == TaskState.Waiting); });
Test("Pause and resume preserve business state", () => { var service = Open(); var item = service.Create(draft); item = service.TogglePause(item); Assert(item.Paused && item.State == TaskState.Waiting); item = service.TogglePause(item); Assert(!item.Paused && item.State == TaskState.Waiting); });
Test("Completion and persisted undo restore waiting and previous pause", () => { var service = Open(); var item = service.TogglePause(service.Create(draft)); item = service.Complete(item); Assert(item.State == TaskState.Completed && !item.Paused && item.CompletedAt is not null); item = Open().LoadAll().Single(t => t.Id == item.Id); item = service.UndoComplete(item); Assert(item.State == TaskState.Waiting && item.Paused && item.CompletedAt is null && item.BeforeCompletion is null); });
Test("Ordinary task completion undo restores ready state", () => { var service = Open(); var item = service.Create(draft with { Kind = TaskKind.Todo }); item = service.UndoComplete(service.Complete(item)); Assert(item.State == TaskState.Ready && !item.Paused); });
Test("Archive and restore retain state, notes, pause and completion undo", () => { var service = Open(); var item = service.TogglePause(service.Create(draft)); item = service.Archive(item); Assert(item.ArchivedAt is not null && item.Paused && item.State == TaskState.Waiting); item = service.Restore(item); Assert(item.ArchivedAt is null && item.Paused); item = service.Archive(service.Complete(item)); Throws<TaskValidationException>(() => service.UndoComplete(item)); item = service.UndoComplete(service.Restore(item)); Assert(item.Paused && item.State == TaskState.Waiting); });
Test("Archived and completed tasks reject active-only transitions", () => { var service = Open(); var item = service.Complete(service.Create(draft)); Throws<TaskValidationException>(() => service.Edit(item, draft)); Throws<TaskValidationException>(() => service.TogglePause(item)); Throws<TaskValidationException>(() => service.Complete(item)); var archived = service.Archive(item); Throws<TaskValidationException>(() => service.Archive(archived)); });
Test("Waiting check writes a separate record and never completes task", () => { var service = Open(); var item = service.Create(draft); item = service.RecordWaitingCheck(item, new(2026,10,22), "尚未开放（合成）"); var restored = Open().LoadAll().Single(t => t.Id == item.Id); Assert(restored.State == TaskState.Waiting && restored.CompletedAt is null); var check = Open().Repository.LoadChecks(item.Id).Single(); Assert(check.LocalDate == new DateOnly(2026,10,22) && check.Notes == "尚未开放（合成）"); });
Test("Paused and ordinary tasks cannot receive waiting checks", () => { var service = Open(); var ordinary = service.Create(draft with { Kind = TaskKind.Todo }); Throws<TaskValidationException>(() => service.RecordWaitingCheck(ordinary, new(2026,10,22), "")); var paused = service.TogglePause(service.Create(draft)); Throws<TaskValidationException>(() => service.RecordWaitingCheck(paused, new(2026,10,22), "")); });
Test("Stale edit cannot overwrite newer task or append an event", () => { var service = Open(); var stale = service.Create(draft); var changed = service.Edit(stale, draft with { Title = "较新内容" }); var count = Count(service.Repository.DatabasePath, "task_events"); Throws<TaskConflictException>(() => service.Edit(stale, draft with { Title = "过时内容" })); Assert(service.LoadAll().Single(t => t.Id == changed.Id) == changed && Count(service.Repository.DatabasePath, "task_events") == count); });
Test("SQL-like title is stored as literal text", () => { var service = Open(); const string title = "'); DROP TABLE tasks; --"; Assert(service.Create(draft with { Title = title }).Title == title); Assert(service.LoadAll().Count > 0); });
var invalidDrafts = new Dictionary<string,TaskDraft>
{
    ["empty title"] = draft with { Title = "  " }, ["long title"] = draft with { Title = new string('字',121) },
    ["invalid kind"] = draft with { Kind = (TaskKind)7 }, ["javascript URL"] = draft with { Url = "javascript:alert(1)" },
    ["file URL"] = draft with { Url = "file:///tmp/a" }, ["URL credentials"] = draft with { Url = "https://user:password@example.org" },
    ["incomplete URL"] = draft with { Url = "example.org" }, ["empty time"] = draft with { Times = "" },
    ["out of range time"] = draft with { Times = "24:00" }, ["malformed time"] = draft with { Times = "9:00" },
    ["empty time token"] = draft with { Times = "09:00," }, ["too many times"] = draft with { Times = string.Join(',', Enumerable.Repeat("09:00",9)) },
    ["no weekday"] = draft with { Weekdays = 0 }, ["invalid weekday"] = draft with { Weekdays = 128 },
    ["invalid date"] = draft with { DueDate = "2026-02-30" }, ["wrong date format"] = draft with { DueDate = "2026/12/01" },
    ["long notes"] = draft with { Notes = new string('字',4001) }
};
foreach (var (name, input) in invalidDrafts) Test("Reject " + name + " before writing", () => { var service = Open(); var count = service.LoadAll().Count; Throws<TaskValidationException>(() => service.Create(input)); Assert(service.LoadAll().Count == count); });
Test("Past deadline is accepted without completing or hiding a task", () => { var item = Open().Create(draft with { DueDate = "2000-01-01" }); Assert(item.DueDate == new DateOnly(2000,1,1) && item.State == TaskState.Waiting && item.ArchivedAt is null); });
Test("Task write is rolled back if event write fails", () => { var service = Open("rollback.db"); Sql(service.Repository.DatabasePath, "CREATE TRIGGER fail_event BEFORE INSERT ON task_events BEGIN SELECT RAISE(ABORT,'controlled failure'); END;"); Throws<SqliteException>(() => service.Create(draft)); Assert(service.LoadAll().Count == 0 && Count(service.Repository.DatabasePath,"task_events") == 0); });
Test("Task revision, check and event roll back together on check failure", () => { var service = Open("check-rollback.db"); var item = service.Create(draft); Sql(service.Repository.DatabasePath, "CREATE TRIGGER fail_check BEFORE INSERT ON task_checks BEGIN SELECT RAISE(ABORT,'controlled failure'); END;"); Throws<SqliteException>(() => service.RecordWaitingCheck(item,new(2026,10,22),"")); Assert(service.LoadAll().Single() == item && Count(service.Repository.DatabasePath,"task_events") == 1 && Count(service.Repository.DatabasePath,"task_checks") == 0); });
Test("Read-only database rejects saves without losing stored tasks", () => { var writable = Open("readonly.db"); var item = writable.Create(draft); var readOnly = Open("readonly.db",true); Throws<SqliteException>(() => readOnly.TogglePause(item)); Assert(writable.LoadAll().Single() == item); });
Test("Locked database reports failure and keeps original row", () => { var service = Open("locked.db"); var item = service.Create(draft); using var connection = new SqliteConnection($"Data Source={service.Repository.DatabasePath};Pooling=False"); connection.Open(); using var transaction = connection.BeginTransaction(); using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "UPDATE tasks SET title=title;"; command.ExecuteNonQuery(); Throws<SqliteException>(() => service.TogglePause(item)); transaction.Rollback(); Assert(service.LoadAll().Single() == item); });
Test("Future schema is rejected and byte content remains unchanged", () => { var service = Open("future.db"); service.Create(draft); Sql(service.Repository.DatabasePath,"PRAGMA user_version=999;"); var before = File.ReadAllBytes(service.Repository.DatabasePath); Throws<StoreVersionException>(() => Open("future.db")); Assert(before.SequenceEqual(File.ReadAllBytes(service.Repository.DatabasePath))); });
Test("Unrelated SQLite schema is not adopted or overwritten", () => { var path = Path.Combine(directory,"unrelated.db"); Sql(path,"CREATE TABLE unrelated(value TEXT);"); var before = File.ReadAllBytes(path); Throws<StoreVersionException>(() => Open("unrelated.db")); Assert(before.SequenceEqual(File.ReadAllBytes(path))); });
Test("Corrupt database is rejected without destructive reset", () => { var path = Path.Combine(directory,"corrupt.db"); File.WriteAllText(path,"not-a-database-preserve-me"); var before = File.ReadAllBytes(path); Throws<SqliteException>(() => Open("corrupt.db")); Assert(before.SequenceEqual(File.ReadAllBytes(path))); });
Test("Missing directory is created only for new owned database", () => { var service = Open("nested/new/tasks.db"); Assert(service.LoadAll().Count == 0); });
ReminderChecks.Run(Test,directory);
RepeatChecks.Run(Test,directory);
RecoveryChecks.Run(Test,directory);
using (var connection = new SqliteConnection($"Data Source={Path.Combine(directory,"tasks.db")};Pooling=False"))
{
    connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "SELECT sqlite_version();";
    Console.WriteLine("Bundled SQLite version: " + command.ExecuteScalar());
}
File.WriteAllText(Path.Combine(directory,"core-report.json"), JsonSerializer.Serialize(new { stage = "L4", failures, count = results.Count, checks = results }, new JsonSerializerOptions { WriteIndented = true }));
if (failures > 0) Environment.Exit(1);
