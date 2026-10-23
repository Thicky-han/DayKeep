using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DayKeep.Core;

public sealed partial class TaskRepository(string databasePath, bool readOnly = false)
{
    public string DatabasePath { get; } = Path.GetFullPath(databasePath);
    private const long ApplicationId = 1145786417;
    public void Initialize()
    {
        if (!readOnly) Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        using var connection = Open();
        var version = Convert.ToInt64(Scalar(connection, "PRAGMA user_version;"), CultureInfo.InvariantCulture);
        var appId = Convert.ToInt64(Scalar(connection, "PRAGMA application_id;"), CultureInfo.InvariantCulture);
        if (version > 5) throw new StoreVersionException("任务库来自更新版本，请升级日守后再打开；原文件已保留。");
        if (version is >= 1 and <= 5 && appId == ApplicationId)
        {
            if (!Equals(Scalar(connection, "PRAGMA quick_check;"), "ok")) throw new StoreVersionException("任务库检查失败，原文件已保留，请从可靠备份恢复。");
            if (version < 5) Upgrade(connection,version);
            return;
        }
        if (version != 0 || appId != 0 || Convert.ToInt64(Scalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE name NOT LIKE 'sqlite_%';"), CultureInfo.InvariantCulture) != 0)
            throw new StoreVersionException("任务库结构无法识别，未修改原文件。请检查是否打开了正确的数据库。");
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $$"""
            CREATE TABLE tasks (
                id TEXT PRIMARY KEY, revision INTEGER NOT NULL CHECK(revision>0),
                title TEXT NOT NULL CHECK(length(title) BETWEEN 1 AND 120),
                kind INTEGER NOT NULL CHECK(kind IN (0,1)), state INTEGER NOT NULL CHECK(state IN (0,1,2)),
                paused INTEGER NOT NULL CHECK(paused IN (0,1)), url TEXT NOT NULL, notes TEXT NOT NULL,
                times TEXT NOT NULL, weekdays INTEGER NOT NULL CHECK(weekdays BETWEEN 1 AND 127),
                repeat_minutes INTEGER NOT NULL CHECK(repeat_minutes BETWEEN 5 AND 1440),
                max_extra INTEGER NOT NULL CHECK(max_extra BETWEEN 0 AND 24), due_date TEXT,
                created_at TEXT NOT NULL, updated_at TEXT NOT NULL, completed_at TEXT,
                before_completion INTEGER CHECK(before_completion IN (0,1)), paused_before_completion INTEGER NOT NULL CHECK(paused_before_completion IN (0,1)),
                archived_at TEXT,
                CHECK((state=2 AND completed_at IS NOT NULL AND before_completion IS NOT NULL AND paused=0)
                  OR (state<>2 AND completed_at IS NULL AND before_completion IS NULL)),
                CHECK(kind<>0 OR state<>0)
            );
            CREATE TABLE task_events (id TEXT PRIMARY KEY, task_id TEXT NOT NULL REFERENCES tasks(id), kind TEXT NOT NULL, recorded_at TEXT NOT NULL);
            CREATE TABLE task_checks (id TEXT PRIMARY KEY, task_id TEXT NOT NULL REFERENCES tasks(id), local_date TEXT NOT NULL, recorded_at TEXT NOT NULL, notes TEXT NOT NULL);
            CREATE INDEX task_checks_by_task_date ON task_checks(task_id,local_date);
            PRAGMA application_id={{ApplicationId}};
            PRAGMA user_version=1;
            """;
        command.ExecuteNonQuery();
        transaction.Commit();
        Upgrade(connection,1);
    }

    private static void Upgrade(SqliteConnection connection, long version)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        if (version == 1)
        {
            command.CommandText = "ALTER TABLE task_checks ADD COLUMN outcome INTEGER NOT NULL DEFAULT 0 CHECK(outcome IN(0,1));";
            command.ExecuteNonQuery();
        }
        if (version < 3)
        {
        command.CommandText = """
            CREATE TABLE reminder_progress (
                task_id TEXT PRIMARY KEY REFERENCES tasks(id), local_date TEXT NOT NULL,
                extra_count INTEGER NOT NULL CHECK(extra_count BETWEEN 0 AND 24),
                next_repeat_at TEXT, snoozed_until TEXT, version INTEGER NOT NULL CHECK(version>0), last_delivered_at TEXT
            );
            """;
        command.ExecuteNonQuery();
        }
        if(version < 4)
        {
        command.CommandText = """
            CREATE TABLE reminder_days (
                task_id TEXT NOT NULL REFERENCES tasks(id), local_date TEXT NOT NULL,
                extra_count INTEGER NOT NULL CHECK(extra_count BETWEEN 0 AND 24), covered_at TEXT, PRIMARY KEY(task_id,local_date)
            );
            CREATE INDEX reminder_days_by_date ON reminder_days(local_date);
            INSERT INTO reminder_days SELECT task_id,local_date,extra_count,last_delivered_at FROM reminder_progress;
            CREATE TABLE reminder_deliveries (
                id TEXT PRIMARY KEY, task_id TEXT NOT NULL REFERENCES tasks(id), task_revision INTEGER NOT NULL,
                local_date TEXT NOT NULL, local_time TEXT NOT NULL, kind INTEGER NOT NULL CHECK(kind IN(0,1,2)), generation INTEGER NOT NULL,
                scheduled_at TEXT NOT NULL, queued_at TEXT NOT NULL, status INTEGER NOT NULL CHECK(status IN(0,1,2,3))
            );
            CREATE INDEX reminder_deliveries_by_task_status ON reminder_deliveries(task_id,status);
            CREATE INDEX reminder_deliveries_by_status ON reminder_deliveries(status);
            CREATE INDEX reminder_deliveries_by_date ON reminder_deliveries(local_date);
            """;
        command.ExecuteNonQuery();
        }
        command.CommandText="CREATE TABLE app_metadata(id INTEGER PRIMARY KEY CHECK(id=1),revision_floor INTEGER NOT NULL CHECK(revision_floor>=0)); INSERT INTO app_metadata SELECT 1,COALESCE(MAX(revision),0) FROM tasks; PRAGMA user_version=5;";
        command.ExecuteNonQuery(); transaction.Commit();
    }

    public IReadOnlyList<TaskItem> LoadAll()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM tasks ORDER BY created_at DESC, id;";
        using var reader = command.ExecuteReader();
        var items = new List<TaskItem>();
        while (reader.Read()) items.Add(Read(reader));
        return items;
    }

    public void Save(TaskItem item, int? expectedRevision, string eventKind, TaskCheck? check = null,
        DateOnly? reminderDate = null, DateTimeOffset? nextRepeat = null, DateTimeOffset? snoozedUntil = null)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = expectedRevision is null
            ? "INSERT INTO tasks (id,revision,title,kind,state,paused,url,notes,times,weekdays,repeat_minutes,max_extra,due_date,created_at,updated_at,completed_at,before_completion,paused_before_completion,archived_at) VALUES ($id,$revision,$title,$kind,$state,$paused,$url,$notes,$times,$weekdays,$repeat,$max,$due,$created,$updated,$completed,$before,$wasPaused,$archived);"
            : "UPDATE tasks SET revision=$revision,title=$title,kind=$kind,state=$state,paused=$paused,url=$url,notes=$notes,times=$times,weekdays=$weekdays,repeat_minutes=$repeat,max_extra=$max,due_date=$due,created_at=$created,updated_at=$updated,completed_at=$completed,before_completion=$before,paused_before_completion=$wasPaused,archived_at=$archived WHERE id=$id AND revision=$expected;";
        BindTask(command,item);
        if (expectedRevision is { } revision) Add(command, "$expected", revision);
        if (command.ExecuteNonQuery() != 1) throw new TaskConflictException();
        using var floor = connection.CreateCommand(); floor.Transaction=transaction;
        floor.CommandText="UPDATE app_metadata SET revision_floor=MAX(revision_floor,$revision) WHERE id=1;"; Add(floor,"$revision",item.Revision); floor.ExecuteNonQuery();
        using var eventCommand = connection.CreateCommand();
        eventCommand.Transaction = transaction;
        eventCommand.CommandText = "INSERT INTO task_events VALUES ($id,$task,$kind,$at);";
        Add(eventCommand, "$id", Guid.NewGuid().ToString()); Add(eventCommand, "$task", item.Id.ToString()); Add(eventCommand, "$kind", eventKind); Add(eventCommand, "$at", Stamp(item.UpdatedAt));
        eventCommand.ExecuteNonQuery();
        if (check is not null)
        {
            using var checkCommand = connection.CreateCommand();
            checkCommand.Transaction = transaction;
            checkCommand.CommandText = "INSERT INTO task_checks (id,task_id,local_date,recorded_at,notes,outcome) VALUES ($id,$task,$date,$at,$notes,$outcome);";
            Add(checkCommand, "$id", check.Id.ToString()); Add(checkCommand, "$task", check.TaskId.ToString());
            Add(checkCommand, "$date", check.LocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); Add(checkCommand, "$at", Stamp(check.RecordedAt)); Add(checkCommand, "$notes", check.Notes);
            Add(checkCommand, "$outcome", (int)check.Outcome);
            checkCommand.ExecuteNonQuery();
        }
        var progress = ReadProgress(connection,transaction,item.Id);
        if (progress is not null || reminderDate is not null)
        {
            var date = reminderDate ?? progress!.LocalDate;
            WriteProgress(connection,transaction,new(item.Id,date,progress?.LocalDate == date ? progress.ExtraCount : 0,
                nextRepeat,snoozedUntil,progress?.Version ?? 0,progress?.LastDeliveredAt));
        }
        using var deliveryCommand = connection.CreateCommand(); deliveryCommand.Transaction = transaction;
        deliveryCommand.CommandText = "UPDATE reminder_deliveries SET task_revision=$revision,status=CASE WHEN $cancel THEN 3 ELSE status END WHERE task_id=$id AND status IN(0,1);";
        Add(deliveryCommand,"$revision",item.Revision); Add(deliveryCommand,"$id",item.Id.ToString());
        Add(deliveryCommand,"$cancel",!ReminderPlanner.Active(item) || eventKind is "snoozed" or "checked-still-waiting");
        deliveryCommand.ExecuteNonQuery();
        transaction.Commit();
    }

    private static void BindTask(SqliteCommand command,TaskItem item)
    {
        Add(command, "$id", item.Id.ToString()); Add(command, "$revision", item.Revision); Add(command, "$title", item.Title);
        Add(command, "$kind", (int)item.Kind); Add(command, "$state", (int)item.State); Add(command, "$paused", item.Paused);
        Add(command, "$url", item.Url); Add(command, "$notes", item.Notes); Add(command, "$times", item.Reminder.Times);
        Add(command, "$weekdays", item.Reminder.Weekdays); Add(command, "$repeat", item.Reminder.RepeatMinutes); Add(command, "$max", item.Reminder.MaxExtraReminders);
        Add(command, "$due", item.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Add(command, "$created", Stamp(item.CreatedAt)); Add(command, "$updated", Stamp(item.UpdatedAt));
        Add(command, "$completed", item.CompletedAt is { } completed ? Stamp(completed) : null);
        Add(command, "$before", item.BeforeCompletion is { } before ? (int)before : null); Add(command, "$wasPaused", item.PausedBeforeCompletion);
        Add(command, "$archived", item.ArchivedAt is { } archived ? Stamp(archived) : null);
    }

    public IReadOnlyList<TaskCheck> LoadChecks(Guid taskId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM task_checks WHERE task_id=$task ORDER BY recorded_at;";
        Add(command, "$task", taskId.ToString());
        using var reader = command.ExecuteReader();
        var checks = new List<TaskCheck>();
        while (reader.Read()) checks.Add(ReadCheck(reader));
        return checks;
    }

    public ReminderSnapshot LoadReminderSnapshot(DateOnly date)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        var items = new List<TaskItem>();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT * FROM tasks ORDER BY created_at DESC,id;";
            using var reader = command.ExecuteReader();
            while (reader.Read()) items.Add(Read(reader));
        }
        var checks = new List<TaskCheck>();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT * FROM task_checks WHERE local_date=$date ORDER BY recorded_at;";
            Add(command, "$date", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            using var reader = command.ExecuteReader();
            while (reader.Read()) checks.Add(ReadCheck(reader));
        }
        var progress = new List<ReminderProgress>();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction; command.CommandText = "SELECT * FROM reminder_progress;";
            using var reader = command.ExecuteReader(); while(reader.Read()) progress.Add(ReadProgress(reader));
        }
        var budgets = new Dictionary<Guid,int>();
        var covered = new Dictionary<Guid,DateTimeOffset>();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction; command.CommandText = "SELECT task_id,extra_count,covered_at FROM reminder_days WHERE local_date=$date;"; Add(command,"$date",date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
            using var reader = command.ExecuteReader(); while(reader.Read()) {var id=Guid.Parse(reader.GetString(0));budgets.Add(id,reader.GetInt32(1));if(!reader.IsDBNull(2)) covered.Add(id,DateTimeOffset.Parse(reader.GetString(2),CultureInfo.InvariantCulture));}
        }
        var deliveries = new List<ReminderDelivery>();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction; command.CommandText = "SELECT task_id,task_revision,local_date,local_time,kind,scheduled_at,queued_at,status,generation FROM reminder_deliveries WHERE status IN(0,1) OR local_date BETWEEN $from AND $to ORDER BY queued_at,id;";
            Add(command,"$from",date.AddDays(-8).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)); Add(command,"$to",date.AddDays(8).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
            using var reader = command.ExecuteReader();
            while(reader.Read()) deliveries.Add(new(new(Guid.Parse(reader.GetString(0)),DateOnly.ParseExact(reader.GetString(2),"yyyy-MM-dd",CultureInfo.InvariantCulture),TimeOnly.ParseExact(reader.GetString(3),"HH:mm:ss.fffffff",CultureInfo.InvariantCulture),(ReminderKind)reader.GetInt32(4),reader.GetInt32(8),reader.GetInt32(4)==0 ? 0 : DateTimeOffset.Parse(reader.GetString(5),CultureInfo.InvariantCulture).UtcTicks),
                reader.GetInt32(1),DateTimeOffset.Parse(reader.GetString(5),CultureInfo.InvariantCulture),DateTimeOffset.Parse(reader.GetString(6),CultureInfo.InvariantCulture),(DeliveryState)reader.GetInt32(7)));
        }
        transaction.Commit();
        return new(items, checks, progress.Select(p=>p with
        {
            LocalDate=date, ExtraCount=budgets.GetValueOrDefault(p.TaskId), LastDeliveredAt=covered.TryGetValue(p.TaskId,out var at) ? at : null,
            NextRepeatAt=p.LocalDate!=date && p.NextRepeatAt is not null && covered.TryGetValue(p.TaskId,out var anchor)
                ? anchor.AddMinutes(items.Single(t=>t.Id==p.TaskId).Reminder.RepeatMinutes) : p.NextRepeatAt
        }).ToList(),deliveries);
    }

    public bool QueueDelivery(ReminderOccurrence occurrence, ReminderProgress progress, DateTimeOffset now)
    {
        var task = occurrence.Task;
        using var connection = Open(); using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT revision FROM tasks WHERE id=$id;"; Add(command,"$id",task.Id.ToString());
        if (!Equals(command.ExecuteScalar(),(long)task.Revision)) throw new TaskConflictException();
        command.CommandText = "INSERT OR IGNORE INTO reminder_deliveries VALUES ($key,$id,$revision,$date,$time,$kind,$generation,$scheduled,$queued,0);";
        Add(command,"$key",KeyId(occurrence.Key)); Add(command,"$revision",task.Revision);
        Add(command,"$date",occurrence.Key.Date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)); Add(command,"$time",occurrence.Key.Time.ToString("HH:mm:ss.fffffff",CultureInfo.InvariantCulture));
        Add(command,"$kind",(int)occurrence.Key.Kind); Add(command,"$scheduled",Stamp(occurrence.ScheduledAt)); Add(command,"$queued",Stamp(now));
        Add(command,"$generation",occurrence.Key.Generation);
        if(command.ExecuteNonQuery()==0) return false;
        command.CommandText="UPDATE reminder_deliveries SET status=3 WHERE task_id=$id AND id<>$key AND status IN(0,1);";
        command.ExecuteNonQuery();
        WriteProgress(connection,transaction,progress,now); transaction.Commit(); return true;
    }
    public void SetDeliveryState(IReadOnlyList<ReminderKey> keys, DeliveryState state)
    {
        if(state is not (DeliveryState.Shown or DeliveryState.Dismissed)) throw new ArgumentOutOfRangeException(nameof(state));
        using var connection=Open(); using var transaction=connection.BeginTransaction();
        foreach(var key in keys)
        {
            using var command=connection.CreateCommand(); command.Transaction=transaction;
            command.CommandText="UPDATE reminder_deliveries SET status=$state WHERE id=$id AND status IN(0,1);";
            Add(command,"$state",(int)state); Add(command,"$id",KeyId(key)); command.ExecuteNonQuery();
        }
        transaction.Commit();
    }
    private static string KeyId(ReminderKey key) => $"{key.TaskId:N}/{key.Date.ToString("yyyyMMdd",CultureInfo.InvariantCulture)}/{key.Time.ToString("HHmmssfffffff",CultureInfo.InvariantCulture)}/{(int)key.Kind}/{key.Generation}/{key.Instant}";
    private static ReminderProgress? ReadProgress(SqliteConnection connection, SqliteTransaction transaction, Guid id)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT * FROM reminder_progress WHERE task_id=$id;"; Add(command,"$id",id.ToString());
        using var reader = command.ExecuteReader(); return reader.Read() ? ReadProgress(reader) : null;
    }
    private static ReminderProgress ReadProgress(SqliteDataReader reader)
    {
        DateTimeOffset? At(int ordinal) => reader.IsDBNull(ordinal) ? null : DateTimeOffset.Parse(reader.GetString(ordinal),CultureInfo.InvariantCulture);
        return new(Guid.Parse(reader.GetString(0)),DateOnly.ParseExact(reader.GetString(1),"yyyy-MM-dd",CultureInfo.InvariantCulture),reader.GetInt32(2),At(3),At(4),reader.GetInt32(5),At(6));
    }
    private static void WriteProgress(SqliteConnection connection, SqliteTransaction transaction, ReminderProgress progress, DateTimeOffset? coveredAt = null)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = progress.Version == 0
            ? "INSERT INTO reminder_progress VALUES ($id,$date,$count,$next,$snooze,1,$last);"
            : "UPDATE reminder_progress SET local_date=$date,extra_count=$count,next_repeat_at=$next,snoozed_until=$snooze,version=version+1,last_delivered_at=$last WHERE task_id=$id AND version=$version;";
        Add(command,"$id",progress.TaskId.ToString()); Add(command,"$date",progress.LocalDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
        Add(command,"$count",progress.ExtraCount); Add(command,"$next",progress.NextRepeatAt is { } next ? Stamp(next) : null);
        Add(command,"$snooze",progress.SnoozedUntil is { } snooze ? Stamp(snooze) : null);
        Add(command,"$last",progress.LastDeliveredAt is { } last ? Stamp(last) : null); Add(command,"$version",progress.Version);
        if(command.ExecuteNonQuery()!=1) throw new TaskConflictException();
        command.Parameters.Clear();
        command.CommandText="INSERT INTO reminder_days VALUES ($id,$date,$count,$covered) ON CONFLICT(task_id,local_date) DO UPDATE SET extra_count=MAX(extra_count,excluded.extra_count),covered_at=CASE WHEN $covered IS NULL THEN covered_at WHEN covered_at IS NULL OR julianday($covered)>julianday(covered_at) THEN $covered ELSE covered_at END;";
        Add(command,"$id",progress.TaskId.ToString()); Add(command,"$date",progress.LocalDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)); Add(command,"$count",progress.ExtraCount); Add(command,"$covered",coveredAt is { } covered ? Stamp(covered) : null); command.ExecuteNonQuery();
    }

    private static TaskCheck ReadCheck(SqliteDataReader reader) => new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
        DateOnly.ParseExact(reader.GetString(2), "yyyy-MM-dd", CultureInfo.InvariantCulture), DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture), reader.GetString(4), (CheckOutcome)reader.GetInt32(5));

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate, ForeignKeys = true, Pooling = false, DefaultTimeout = 1 }.ToString());
        try { connection.Open(); return connection; } catch { connection.Dispose(); throw; }
    }
    private static object? Scalar(SqliteConnection connection, string sql) { using var command = connection.CreateCommand(); command.CommandText = sql; return command.ExecuteScalar(); }
    private static void Add(SqliteCommand command, string key, object? value) => command.Parameters.AddWithValue(key, value ?? DBNull.Value);
    private static string Stamp(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);
    private static TaskItem Read(SqliteDataReader reader)
    {
        string Text(string name) => reader.GetString(reader.GetOrdinal(name));
        int Number(string name) => reader.GetInt32(reader.GetOrdinal(name));
        string? Optional(string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? null : Text(name);
        DateTimeOffset? At(string name) => Optional(name) is { } value ? DateTimeOffset.Parse(value, CultureInfo.InvariantCulture) : null;
        return new(Guid.Parse(Text("id")), Text("title"), (TaskKind)Number("kind"), (TaskState)Number("state"), Number("paused") == 1,
            Text("url"), Text("notes"), new(Text("times"), Number("weekdays"), Number("repeat_minutes"), Number("max_extra")),
            Optional("due_date") is { } due ? DateOnly.ParseExact(due, "yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
            At("created_at")!.Value, At("updated_at")!.Value, Number("revision"), At("completed_at"),
            Optional("before_completion") is not null ? (TaskState)Number("before_completion") : null, Number("paused_before_completion") == 1, At("archived_at"));
    }
}
