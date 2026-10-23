using DayKeep.Core;
using Microsoft.Data.Sqlite;

internal static class RecoveryChecks
{
    public static void Run(Action<string,Action> test,string directory)
    {
        var number=0;
        void Case(string name,Action<Fixture> action)=>test("L4 "+name,()=>action(new(Path.Combine(directory,$"l4-{++number}.db"))));
        Case("startup recovers the latest missed fixed time",f=> { f.At(11,12); var due=f.Due().Single(); Assert(due.ScheduledAt==f.Start.AddMinutes(1) && due.Key.Kind==ReminderKind.Fixed); });
        Case("multiple missed times and days aggregate to one current task",f=> { f.Task=f.Service.Edit(f.Task,new("weekly absence",TaskKind.Todo,Times:"09:00, 14:00")); f.Now=f.Start.AddDays(40).AddHours(8); var due=f.Due().Single(); Assert(due.ScheduledAt.Hour==14 && due.ScheduledAt.Date==f.Now.Date); });
        Case("does not create a historical reminder before creation or edit",f=> { f.Task=f.Service.Edit(f.Task,new("future",TaskKind.Todo,Times:"08:00")); f.At(11,0); Assert(f.Due().Count==0); });
        Case("selected weekday is recovered even when waking on a weekend",f=> { f.Task=f.Service.Edit(f.Task,new("workdays",TaskKind.Todo,Weekdays:31)); f.Now=f.Start.AddDays(6).AddHours(5); Assert(f.Due().Single().ScheduledAt.DayOfWeek==DayOfWeek.Friday); });
        Case("prepared but never shown delivery survives restart without double spending",f=> { f.At(9,0); f.Queue(); var before=f.Snapshot.Progress.Single(); var reopened=f.Reopen(); Assert(reopened.Deliveries.Single().State==DeliveryState.Pending && reopened.Progress.Single()==before && f.Due().Count==0); });
        Case("shown delivery remains recoverable until explicitly dismissed",f=> { f.At(9,0); var key=f.Queue().Key; f.Service.SetDeliveryState([key],DeliveryState.Shown); Assert(f.Reopen().Deliveries.Single().State==DeliveryState.Shown); f.Service.SetDeliveryState([key],DeliveryState.Dismissed); Assert(f.Reopen().Deliveries.Single().State==DeliveryState.Dismissed && f.Due().Count==0); });
        Case("queue and progress roll back together when progress write fails",f=> { f.Sql("CREATE TRIGGER reject_progress BEFORE INSERT ON reminder_progress BEGIN SELECT RAISE(ABORT,'controlled'); END;"); f.At(9,0); Throws<SqliteException>(()=>f.Queue()); Assert(f.Snapshot.Progress.Count==0 && f.Snapshot.Deliveries.Count==0); });
        Case("ledger failure never consumes a future repeat",f=> { f.At(9,0); f.Queue(); var before=f.Snapshot.Progress.Single(); f.Sql("CREATE TRIGGER reject_delivery BEFORE INSERT ON reminder_deliveries BEGIN SELECT RAISE(ABORT,'controlled'); END;"); f.At(9,30); Throws<SqliteException>(()=>f.Queue()); Assert(f.Snapshot.Progress.Single()==before && f.Snapshot.Deliveries.Count==1); });
        Case("duplicate queue is idempotent even with a stale progress snapshot",f=> { f.At(9,0); var snapshot=f.Snapshot; var due=f.Due().Single(); Assert(f.Service.QueueDelivery(due,null,f.Now,TimeZoneInfo.Utc)); Assert(!f.Service.QueueDelivery(due,null,f.Now,TimeZoneInfo.Utc)); Assert(f.Snapshot.Deliveries.Count==1 && f.Snapshot.Progress.Single().Version==1); });
        Case("newer task revision rejects an obsolete prepared reminder",f=> { f.At(9,0); var due=f.Due().Single(); f.Task=f.Service.TogglePause(f.Task); Throws<TaskConflictException>(()=>f.Service.QueueDelivery(due,null,f.Now,TimeZoneInfo.Utc)); Assert(f.Snapshot.Deliveries.Count==0); });
        Case("snooze check completion and pause cancel durable pending cards",f=> { f.At(9,0); f.Queue(); f.Task=f.Service.Snooze(f.Task,f.Date,10); Assert(f.Snapshot.Deliveries.All(d=>d.State==DeliveryState.Canceled)); f.At(9,10); f.Queue(); f.Task=f.Service.RecordWaitingCheck(f.Task,f.Date,""); Assert(f.Snapshot.Deliveries.All(d=>d.State==DeliveryState.Canceled)); f.Task=f.Service.MarkReady(f.Task,f.Date); f.At(9,40); f.Queue(); f.Task=f.Service.TogglePause(f.Task); Assert(f.Snapshot.Deliveries.All(d=>d.State==DeliveryState.Canceled)); });
        Case("ready transition updates the pending card revision atomically",f=> { f.At(9,0); f.Queue(); f.Task=f.Service.MarkReady(f.Task,f.Date); Assert(f.Snapshot.Deliveries.Single() is { State:DeliveryState.Pending } d && d.TaskRevision==f.Task.Revision); });
        Case("clock forward then backward never replays older fixed slots",f=> { f.Task=f.Service.Edit(f.Task,new("clock changes",TaskKind.Todo,Times:"09:00, 14:00, 16:00")); f.At(14,10); var key=f.Queue().Key; f.Service.SetDeliveryState([key],DeliveryState.Dismissed); f.At(9,10); Assert(f.Due().Count==0 && ReminderPlanner.Next(f.Task,new HashSet<Guid>(),f.Now,TimeZoneInfo.Utc,f.Snapshot.Progress.Single())==new DateTimeOffset(2026,10,26,14,30,0,TimeSpan.Zero)); f.At(16,0); Assert(f.Due().Single().Key.Time==new TimeOnly(16,0)); });
        Case("daily budgets survive date forward and backward movement",f=> { f.At(9,0); f.Queue(); f.At(9,30); f.Queue(); var monday=f.Date; f.Now=f.Now.AddDays(1).AddMinutes(-30); f.Queue(); Assert(f.Snapshot.Progress.Single().ExtraCount==0); f.Now=f.Now.AddDays(-1).AddMinutes(40); Assert(f.Date==monday && f.Snapshot.Progress.Single().ExtraCount==1 && f.Due().Count==0); });
        Case("returning from a future date resumes unseen slots and remaining repeats",f=>
        {
            f.Task=f.Service.Edit(f.Task,new("date correction",TaskKind.Todo,Times:"09:00, 14:00"));
            f.At(9,0);f.Queue();f.Now=f.Now.AddDays(1);f.Queue();f.At(10,0);
            Assert(f.Due().Single().Key.Kind==ReminderKind.Repeat);f.Queue();Assert(f.Snapshot.Progress.Single().ExtraCount==1);
            f.At(14,0);Assert(f.Due().Single().Key.Kind==ReminderKind.Fixed);f.Queue();Assert(f.Snapshot.Progress.Single().ExtraCount==1);
        });
        Case("explicit new snooze can reuse a wall time after a clock correction",f=> { f.At(9,0); f.Task=f.Service.Snooze(f.Task,f.Date,10); f.At(9,10); var first=f.Queue().Key; f.Service.SetDeliveryState([first],DeliveryState.Dismissed); f.At(9,0); f.Task=f.Service.Snooze(f.Task,f.Date,10); f.At(9,10); var second=f.Queue().Key; Assert(first!=second && f.Snapshot.Deliveries.Count==2); });
        Case("time-zone change does not duplicate the same fixed wall-clock identity",f=> { var east=TimeZoneInfo.CreateCustomTimeZone("L4 UTC8",TimeSpan.FromHours(8),"UTC8","UTC8"); var task=f.Task with { UpdatedAt=f.Start.AddDays(-1),CreatedAt=f.Start.AddDays(-1) }; var first=ReminderPlanner.Due(new([task],[]),new(2026,10,26,1,0,0,TimeSpan.Zero),f.Start.AddDays(-1),east,true).Single(); var delivery=new ReminderDelivery(first.Key,task.Revision,first.ScheduledAt,first.ScheduledAt,DeliveryState.Dismissed); Assert(ReminderPlanner.Due(new([task],[],SavedDeliveries:[delivery]),new(2026,10,26,9,0,0,TimeSpan.Zero),f.Start.AddDays(-1),TimeZoneInfo.Utc,true).Count==0); });
        Case("schema-3 migration retains snooze repeat budget and timestamps",f=> { f.At(9,0); f.Queue(); f.At(9,30); f.Queue(); f.Task=f.Service.Snooze(f.Task,f.Date,60); var before=f.Snapshot.Progress.Single(); f.Sql("DROP TABLE app_metadata; DROP TABLE reminder_deliveries; DROP TABLE reminder_days; PRAGMA user_version=3;"); f.Service.Initialize(); Assert(f.Snapshot.Progress.Single()==before && f.Snapshot.Deliveries.Count==0); });
        Case("failed schema-3 migration leaves byte content unchanged",f=> { f.At(9,0); f.Queue(); f.Sql("PRAGMA user_version=3;"); var bytes=File.ReadAllBytes(f.Path); Throws<SqliteException>(f.Service.Initialize); Assert(bytes.SequenceEqual(File.ReadAllBytes(f.Path))); });
        Case("today check suppresses recovery from any missed day",f=> { f.Now=f.Start.AddDays(3).AddHours(5); f.Task=f.Service.RecordWaitingCheck(f.Task,f.Date,""); Assert(f.Due().Count==0); f.Now=f.Now.AddDays(1); Assert(f.Due().Count==1); });
        Case("DST fallback keeps elapsed repeat intervals without duplicate instants",f=>
        {
            var eastern=TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            var start=new DateTimeOffset(2026,11,1,3,59,0,TimeSpan.Zero);
            var task=f.Task with { CreatedAt=start,UpdatedAt=start,Reminder=new("00:00",127,30,6) };
            ReminderProgress? progress=null; var ledger=new List<ReminderDelivery>();
            for(var i=0;i<5;i++)
            {
                var at=start.AddMinutes(1+30*i);var snapshot=new ReminderSnapshot([task],[],progress is null ? [] : [progress],ledger);
                var due=ReminderPlanner.Due(snapshot,at,start,eastern,true).Single();progress=ReminderPlanner.AfterDelivery(due,progress,at,eastern);
                ledger.Add(new(due.Key,task.Revision,due.ScheduledAt,at,DeliveryState.Shown));
            }
            Assert(ledger.Select(d=>d.Key).Distinct().Count()==5 && progress?.ExtraCount==4);
        });
    }
    private static void Assert(bool value) { if(!value) throw new InvalidOperationException("L4 recovery expectation failed"); }
    private static void Throws<T>(Action action) where T:Exception { try {action();} catch(T) {return;} throw new InvalidOperationException($"Expected {typeof(T).Name}"); }
    private sealed class Fixture
    {
        public string Path {get;}
        public DateTimeOffset Start {get;}=new(2026,10,26,8,59,0,TimeSpan.Zero);
        public DateTimeOffset Now {get;set;}
        public DateOnly Date=>DateOnly.FromDateTime(Now.DateTime);
        public TaskService Service {get;}
        public TaskItem Task {get;set;}
        public ReminderSnapshot Snapshot=>Service.ReminderSnapshot(Date);
        public Fixture(string path) {Path=path; Now=Start; Service=new(new TaskRepository(path),()=>Now); Service.Initialize(); Task=Service.Create(new("恢复测试",TaskKind.WaitingForCondition));}
        public void At(int h,int m)=>Now=new(2026,10,26,h,m,0,TimeSpan.Zero);
        public IReadOnlyList<ReminderOccurrence> Due()=>ReminderPlanner.Due(Snapshot,Now,Now,TimeZoneInfo.Utc,true);
        public ReminderOccurrence Queue() {var snapshot=Snapshot; var due=Due().Single(); Service.QueueDelivery(due,snapshot.Progress.SingleOrDefault(),Now,TimeZoneInfo.Utc); return due;}
        public ReminderSnapshot Reopen() {var service=new TaskService(new TaskRepository(Path));service.Initialize();return service.ReminderSnapshot(Date);}
        public void Sql(string sql) {using var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path,Pooling=false}.ToString());c.Open();using var command=c.CreateCommand();command.CommandText=sql;command.ExecuteNonQuery();}
    }
}
