using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DayKeep.Core;

public sealed class TaskRepository(string databasePath, bool readOnly = false)
{
    public string DatabasePath { get; } = Path.GetFullPath(databasePath);
    private const long ApplicationId = 1145786417;
    public void Initialize()
    {
        if (!readOnly) Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        using var connection = Open();
        var version = Convert.ToInt64(Scalar(connection, "PRAGMA user_version;"), CultureInfo.InvariantCulture);
        var appId = Convert.ToInt64(Scalar(connection, "PRAGMA application_id;"), CultureInfo.InvariantCulture);
        if (version > 2) throw new StoreVersionException("任务库来自更新版本，请升级日守后再打开；原文件已保留。");
        if ((version == 1 || version == 2) && appId == ApplicationId)
        {
            if (!Equals(Scalar(connection, "PRAGMA quick_check;"), "ok")) throw new StoreVersionException("任务库检查失败，原文件已保留，请从可靠备份恢复。");
            if (version == 1) UpgradeFrom1(connection);
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
        UpgradeFrom1(connection);
    }

    private static void UpgradeFrom1(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "ALTER TABLE task_checks ADD COLUMN outcome INTEGER NOT NULL DEFAULT 0 CHECK(outcome IN(0,1)); PRAGMA user_version=2;";
        command.ExecuteNonQuery();
        transaction.Commit();
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

    public void Save(TaskItem item, int? expectedRevision, string eventKind, TaskCheck? check = null)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = expectedRevision is null
            ? "INSERT INTO tasks (id,revision,title,kind,state,paused,url,notes,times,weekdays,repeat_minutes,max_extra,due_date,created_at,updated_at,completed_at,before_completion,paused_before_completion,archived_at) VALUES ($id,$revision,$title,$kind,$state,$paused,$url,$notes,$times,$weekdays,$repeat,$max,$due,$created,$updated,$completed,$before,$wasPaused,$archived);"
            : "UPDATE tasks SET revision=$revision,title=$title,kind=$kind,state=$state,paused=$paused,url=$url,notes=$notes,times=$times,weekdays=$weekdays,repeat_minutes=$repeat,max_extra=$max,due_date=$due,created_at=$created,updated_at=$updated,completed_at=$completed,before_completion=$before,paused_before_completion=$wasPaused,archived_at=$archived WHERE id=$id AND revision=$expected;";
        Add(command, "$id", item.Id.ToString()); Add(command, "$revision", item.Revision); Add(command, "$title", item.Title);
        Add(command, "$kind", (int)item.Kind); Add(command, "$state", (int)item.State); Add(command, "$paused", item.Paused);
        Add(command, "$url", item.Url); Add(command, "$notes", item.Notes); Add(command, "$times", item.Reminder.Times);
        Add(command, "$weekdays", item.Reminder.Weekdays); Add(command, "$repeat", item.Reminder.RepeatMinutes); Add(command, "$max", item.Reminder.MaxExtraReminders);
        Add(command, "$due", item.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Add(command, "$created", Stamp(item.CreatedAt)); Add(command, "$updated", Stamp(item.UpdatedAt));
        Add(command, "$completed", item.CompletedAt is { } completed ? Stamp(completed) : null);
        Add(command, "$before", item.BeforeCompletion is { } before ? (int)before : null); Add(command, "$wasPaused", item.PausedBeforeCompletion);
        Add(command, "$archived", item.ArchivedAt is { } archived ? Stamp(archived) : null);
        if (expectedRevision is { } revision) Add(command, "$expected", revision);
        if (command.ExecuteNonQuery() != 1) throw new TaskConflictException();
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
        transaction.Commit();
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
        transaction.Commit();
        return new(items, checks);
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
