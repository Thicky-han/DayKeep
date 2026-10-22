using DayKeep.Core;
using Microsoft.Data.Sqlite;

internal static class ReminderChecks
{
    public static void Run(Action<string,Action> test, string directory)
    {
        var now = new DateTimeOffset(2026,10,26,8,59,0,TimeSpan.Zero); // Monday
        var zone = TimeZoneInfo.Utc;
        TaskDraft Draft(string times = "09:00, 14:00", int days = 127) => new("每天检查（合成）",TaskKind.WaitingForCondition,Times:times,Weekdays:days);
        TaskService Open(string name)
        {
            var service = new TaskService(new TaskRepository(Path.Combine(directory,name)),() => now);
            service.Initialize(); return service;
        }
        static void Assert(bool value) { if (!value) throw new InvalidOperationException("Reminder expectation failed"); }
        static void Throws<T>(Action action) where T : Exception { try { action(); } catch(T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}"); }
        static void Sql(string path, string sql)
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=path,Pooling=false }.ToString()); connection.Open();
            using var command = connection.CreateCommand(); command.CommandText=sql; command.ExecuteNonQuery();
        }
        var item = TaskRules.Create(Draft(),now);
        ReminderSnapshot Snapshot(TaskItem? task = null, params TaskCheck[] checks) => new([task ?? item],checks);
        var atNine = now.AddMinutes(1);
        test("L2 fixed time does not fire early and fires during its current minute",() => { Assert(ReminderPlanner.Due(Snapshot(),now,now,zone).Count==0); Assert(ReminderPlanner.Due(Snapshot(),atNine.AddSeconds(2),now,zone).Count==1); });
        test("L2 later fixed time also produces a distinct slot",() => { var first=ReminderPlanner.Due(Snapshot(),atNine,now,zone).Single(); var second=ReminderPlanner.Due(Snapshot(),now.AddHours(5).AddMinutes(1),now,zone).Single(); Assert(first.Key!=second.Key && first.Key.TaskId==second.Key.TaskId); });
        test("L2 does not replay previous minutes or pre-start times",() => { Assert(ReminderPlanner.Due(Snapshot(),atNine.AddMinutes(1),now,zone).Count==0); Assert(ReminderPlanner.Due(Snapshot(),atNine.AddSeconds(20),atNine.AddSeconds(10),zone).Count==0); });
        test("L2 edited plan only applies to future slots",() => Assert(ReminderPlanner.Due(Snapshot(item with { UpdatedAt=atNine.AddSeconds(1) }),atNine.AddSeconds(2),now,zone).Count==0));
        test("L2 weekday bit zero is Monday and bit six is Sunday",() => { Assert(ReminderPlanner.Due(Snapshot(item with { Reminder=new("09:00",1) }),atNine,now,zone).Count==1); Assert(ReminderPlanner.Due(Snapshot(item with { Reminder=new("09:00",64) }),atNine,now,zone).Count==0); Assert(ReminderPlanner.Due(Snapshot(item with { Reminder=new("09:00",64) }),atNine.AddDays(6),now,zone).Count==1); });
        test("L2 workday schedule excludes weekend",() => Assert(ReminderPlanner.Due(Snapshot(item with { Reminder=new("09:00",31) }),atNine.AddDays(5),now,zone).Count==0));
        test("L2 paused, completed and archived tasks do not produce reminders",() => { foreach(var task in new[] { TaskRules.Pause(item,now), TaskRules.Complete(item,now), TaskRules.Archive(item,now) }) Assert(ReminderPlanner.Due(Snapshot(task),atNine,now,zone).Count==0); });
        var check=new TaskCheck(Guid.NewGuid(),item.Id,new(2026,10,26),atNine,"still waiting");
        test("L2 today's waiting check suppresses every remaining fixed time",() => { Assert(ReminderPlanner.Due(Snapshot(null,check),atNine,now,zone).Count==0); Assert(ReminderPlanner.Due(Snapshot(null,check),atNine.AddHours(5),now,zone).Count==0); });
        test("L2 yesterday's check does not suppress today's or next-day reminders",() => { Assert(ReminderPlanner.Due(Snapshot(null,check with { LocalDate=new(2026,10,25) }),atNine,now,zone).Count==1); Assert(ReminderPlanner.Due(Snapshot(null,check),atNine.AddDays(1),now,zone).Count==1); });
        test("L2 condition-ready record cannot suppress ready-to-handle task",() => { var ready=item with { State=TaskState.Ready }; Assert(ReminderPlanner.Due(Snapshot(ready,check),atNine,now,zone).Count==1); Assert(ReminderPlanner.Due(Snapshot(null,check with { Outcome=CheckOutcome.ConditionReady }),atNine,now,zone).Count==1); });
        test("L2 next reminder skips checked day and honors selected weekdays",() => { var friday=new DateTimeOffset(2026,10,30,8,59,0,TimeSpan.Zero); var workday=item with { Reminder=new("09:00",31) }; Assert(ReminderPlanner.Next(workday,new HashSet<Guid>{item.Id},friday,zone)==new DateTimeOffset(2026,11,2,9,0,0,TimeSpan.Zero)); });
        test("L2 uses local day and clock instead of UTC day",() => { var china=TimeZoneInfo.CreateCustomTimeZone("Test-UTC8",TimeSpan.FromHours(8),"UTC8","UTC8"); var task=item with { CreatedAt=now.AddDays(-1),UpdatedAt=now.AddDays(-1),Reminder=new("00:05",2) }; var time=new DateTimeOffset(2026,10,26,16,5,0,TimeSpan.Zero); Assert(ReminderPlanner.LocalDate(time,china)==new DateOnly(2026,10,27) && ReminderPlanner.Due(Snapshot(task),time,time.AddMinutes(-1),china).Count==1); });
        test("L2 invalid DST local time is skipped and next valid day remains",() => { var eastern=TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); var task=item with { UpdatedAt=new DateTimeOffset(2026,3,1,0,0,0,TimeSpan.Zero),Reminder=new("02:30") }; var next=ReminderPlanner.Next(task,new HashSet<Guid>(),new DateTimeOffset(2026,3,8,5,0,0,TimeSpan.Zero),eastern); Assert(next==new DateTimeOffset(2026,3,9,6,30,0,TimeSpan.Zero)); });
        test("L2 ambiguous DST time has one wall-clock reminder identity",() => { var eastern=TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); var task=item with { UpdatedAt=new DateTimeOffset(2026,10,1,0,0,0,TimeSpan.Zero),Reminder=new("01:30") }; var first=new DateTimeOffset(2026,11,1,5,30,0,TimeSpan.Zero); var second=first.AddHours(1); Assert(ReminderPlanner.Due(Snapshot(task),first,first.AddMinutes(-1),eastern).Single().Key==ReminderPlanner.Due(Snapshot(task),second,first.AddMinutes(-1),eastern).Single().Key); });
        test("L2 due planner groups simultaneous tasks in the same batch",() => { var other=TaskRules.Create(Draft(),now); Assert(ReminderPlanner.Due(new([item,other],[]),atNine,now,zone).Count==2); });
        test("L2 check and open transitions preserve task identity and never auto-complete",() => { var service=Open("l2-ready.db"); var task=service.Create(Draft()); task=service.RecordWaitingCheck(task,new(2026,10,26),"not open"); task=service.MarkReady(task,new(2026,10,26)); var saved=service.LoadAll().Single(); Assert(saved.Id==task.Id && saved.State==TaskState.Ready && saved.CompletedAt is null && service.Repository.LoadChecks(task.Id).Select(c=>c.Outcome).SequenceEqual(new[]{CheckOutcome.StillWaiting,CheckOutcome.ConditionReady})); Assert(ReminderPlanner.Due(service.ReminderSnapshot(new(2026,10,26)),atNine,now,zone).Count==1); });
        test("L2 mark-ready rejects ordinary, paused and completed tasks",() => { var service=Open("l2-reject.db"); foreach(var task in new[]{service.Create(Draft() with { Kind=TaskKind.Todo }),service.TogglePause(service.Create(Draft())),service.Complete(service.Create(Draft()))}) Throws<TaskValidationException>(()=>service.MarkReady(task,new(2026,10,26))); });
        test("L2 stale reminder cannot complete or check a newer revision",() => { var service=Open("l2-stale.db"); var task=service.Create(Draft()); var changed=service.Edit(task,Draft() with { Title="newer" }); Throws<TaskConflictException>(()=>service.MarkReady(task,new(2026,10,26))); Throws<TaskConflictException>(()=>service.RecordWaitingCheck(task,new(2026,10,26),"")); Assert(service.LoadAll().Single()==changed && service.Repository.LoadChecks(task.Id).Count==0); });
        test("L2 mark-ready rolls back state when check insert fails",() => { var service=Open("l2-rollback.db"); var task=service.Create(Draft()); Sql(service.Repository.DatabasePath,"CREATE TRIGGER reject_check BEFORE INSERT ON task_checks BEGIN SELECT RAISE(ABORT,'test failure'); END;"); Throws<SqliteException>(()=>service.MarkReady(task,new(2026,10,26))); Assert(service.LoadAll().Single()==task); });
        test("L2 schema-1 upgrade preserves tasks and classifies old checks as waiting",() => { var service=Open("l2-upgrade.db"); var task=service.RecordWaitingCheck(service.Create(Draft()),new(2026,10,26),"legacy note"); Sql(service.Repository.DatabasePath,"DROP TABLE reminder_progress; ALTER TABLE task_checks DROP COLUMN outcome; PRAGMA user_version=1;"); service.Initialize(); Assert(service.LoadAll().Single()==task && service.Repository.LoadChecks(task.Id).Single().Outcome==CheckOutcome.StillWaiting); service.Initialize(); Assert(service.Repository.LoadChecks(task.Id).Count==1); });
        test("L2 failed schema upgrade keeps schema-1 data intact",() => { var service=Open("l2-upgrade-fail.db"); var task=service.Create(Draft()); Sql(service.Repository.DatabasePath,"PRAGMA user_version=1;"); var before=File.ReadAllBytes(service.Repository.DatabasePath); Throws<SqliteException>(service.Initialize); Assert(before.SequenceEqual(File.ReadAllBytes(service.Repository.DatabasePath)) && service.LoadAll().Single()==task); });
        test("L2 browser URL validation cannot open local files or commands",() => { foreach(var text in new[]{"file:///tmp/test","cmd.exe","javascript:alert(1)","https://user:secret@example.org"}) Throws<TaskValidationException>(()=>WebLink.Parse(text)); Assert(WebLink.Parse("https://example.org/patent?q=1").Scheme=="https"); });
        test("L2 failed initialization blocks reminder reads and writes to future schema",()=>
        {
            var service=Open("l2-init-guard.db"); service.Create(Draft()); Sql(service.Repository.DatabasePath,"PRAGMA user_version=999;");
            var rejected=new TaskService(new TaskRepository(service.Repository.DatabasePath)); var before=File.ReadAllBytes(service.Repository.DatabasePath);
            Throws<StoreVersionException>(rejected.Initialize); Throws<StoreVersionException>(()=>rejected.ReminderSnapshot(new(2026,10,26))); Throws<StoreVersionException>(()=>rejected.Create(Draft()));
            Assert(before.SequenceEqual(File.ReadAllBytes(service.Repository.DatabasePath)));
        });
    }
}
