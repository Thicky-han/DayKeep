using System.Globalization;

namespace DayKeep.Core;

public enum TaskKind { Todo, WaitingForCondition }
public enum TaskState { Waiting, Ready, Completed }
public enum CheckOutcome { StillWaiting, ConditionReady }
public sealed class TaskValidationException(string message) : Exception(message);
public sealed class StoreVersionException(string message) : Exception(message);
public sealed class TaskConflictException() : Exception("任务已在其他窗口发生变化，请关闭表单并重新打开后操作。");

public sealed record ReminderRule(string Times, int Weekdays = 127, int RepeatMinutes = 30, int MaxExtraReminders = 3);
public sealed record TaskDraft(string Title, TaskKind Kind, string Url = "", string Notes = "",
    string Times = "09:00", int Weekdays = 127, string DueDate = "", int RepeatMinutes = 30, int MaxExtraReminders = 3);
public sealed record TaskItem(Guid Id, string Title, TaskKind Kind, TaskState State, bool Paused,
    string Url, string Notes, ReminderRule Reminder, DateOnly? DueDate, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, int Revision = 1, DateTimeOffset? CompletedAt = null,
    TaskState? BeforeCompletion = null, bool PausedBeforeCompletion = false, DateTimeOffset? ArchivedAt = null);
public sealed record TaskCheck(Guid Id, Guid TaskId, DateOnly LocalDate, DateTimeOffset RecordedAt, string Notes, CheckOutcome Outcome = CheckOutcome.StillWaiting);

public static class TaskRules
{
    public static TaskDraft Validate(TaskDraft draft)
    {
        var title = draft.Title.Trim();
        if (title.Length is < 1 or > 120) throw new TaskValidationException("任务名称需要 1—120 个字符。");
        if (!Enum.IsDefined(draft.Kind)) throw new TaskValidationException("请选择有效的任务类型。");
        var url = draft.Url.Trim();
        if (url.Length > 0) WebLink.Parse(url);
        if (draft.Notes.Length > 4000) throw new TaskValidationException("备注最多 4000 个字符。");
        if (draft.Weekdays is < 1 or > 127) throw new TaskValidationException("请至少选择一个提醒星期。");
        if (draft.RepeatMinutes is < 5 or > 1440) throw new TaskValidationException("追加间隔需要 5—1440 分钟。");
        if (draft.MaxExtraReminders is < 0 or > 24) throw new TaskValidationException("每天追加上限需要 0—24 次；0 表示关闭自动追加。");
        var tokens = draft.Times.Replace('，', ',').Split(',', StringSplitOptions.TrimEntries);
        if (tokens.Length is < 1 or > 8) throw new TaskValidationException("请填写 1—8 个提醒时间。");
        var times = new SortedSet<TimeOnly>();
        foreach (var token in tokens)
        {
            if (!TimeOnly.TryParseExact(token, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                throw new TaskValidationException("提醒时间请使用 24 小时格式，如 09:00, 14:30。");
            times.Add(time);
        }
        var due = draft.DueDate.Trim();
        if (due.Length > 0 && !DateOnly.TryParseExact(due, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new TaskValidationException("截止日期请填写 yyyy-MM-dd；未知时留空。");
        return draft with { Title = title, Url = url, Notes = draft.Notes.Trim(), Times = string.Join(", ", times.Select(t => t.ToString("HH:mm", CultureInfo.InvariantCulture))), DueDate = due };
    }

    public static TaskItem Create(TaskDraft input, DateTimeOffset now)
    {
        var draft = Validate(input);
        return new(Guid.NewGuid(), draft.Title, draft.Kind, InitialState(draft.Kind), false,
            draft.Url, draft.Notes, new(draft.Times, draft.Weekdays, draft.RepeatMinutes, draft.MaxExtraReminders), ParseDate(draft.DueDate), now, now);
    }

    public static TaskItem Edit(TaskItem item, TaskDraft input, DateTimeOffset now)
    {
        RequireActive(item);
        var draft = Validate(input);
        return Advance(item, now) with { Title = draft.Title, Kind = draft.Kind,
            State = draft.Kind == item.Kind ? item.State : InitialState(draft.Kind), Url = draft.Url, Notes = draft.Notes,
            Reminder = new(draft.Times, draft.Weekdays, draft.RepeatMinutes, draft.MaxExtraReminders), DueDate = ParseDate(draft.DueDate) };
    }
    public static TaskItem Pause(TaskItem item, DateTimeOffset now) { RequireActive(item); return Advance(item, now) with { Paused = !item.Paused }; }
    public static TaskItem MarkReady(TaskItem item, DateTimeOffset now)
    {
        RequireActive(item);
        if (item.State != TaskState.Waiting || item.Paused) throw new TaskValidationException("只有未暂停的等待条件任务可以确认开放。");
        return Advance(item, now) with { State = TaskState.Ready };
    }
    public static TaskItem Complete(TaskItem item, DateTimeOffset now)
    {
        RequireActive(item);
        return Advance(item, now) with { State = TaskState.Completed, CompletedAt = now, BeforeCompletion = item.State, PausedBeforeCompletion = item.Paused, Paused = false };
    }
    public static TaskItem UndoComplete(TaskItem item, DateTimeOffset now)
    {
        if (item.ArchivedAt is not null || item.State != TaskState.Completed || item.BeforeCompletion is null) throw new TaskValidationException("这条任务当前不能撤销完成。");
        return Advance(item, now) with { State = item.BeforeCompletion.Value, Paused = item.PausedBeforeCompletion, CompletedAt = null, BeforeCompletion = null, PausedBeforeCompletion = false };
    }
    public static TaskItem Archive(TaskItem item, DateTimeOffset now)
    {
        if (item.ArchivedAt is not null) throw new TaskValidationException("任务已经归档。");
        return Advance(item, now) with { ArchivedAt = now };
    }
    public static TaskItem Restore(TaskItem item, DateTimeOffset now)
    {
        if (item.ArchivedAt is null) throw new TaskValidationException("任务尚未归档。");
        return Advance(item, now) with { ArchivedAt = null };
    }
    public static void RequireActive(TaskItem item)
    {
        if (item.ArchivedAt is not null || item.State == TaskState.Completed) throw new TaskValidationException("请先恢复归档或撤销完成，再编辑这条任务。");
    }
    public static TaskItem Advance(TaskItem item, DateTimeOffset now) => item with { UpdatedAt = now, Revision = checked(item.Revision + 1) };
    private static TaskState InitialState(TaskKind kind) => kind == TaskKind.WaitingForCondition ? TaskState.Waiting : TaskState.Ready;
    private static DateOnly? ParseDate(string text) => text.Length == 0 ? null : DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}

public static class WebLink
{
    public static Uri Parse(string text)
    {
        if (text.Length > 2048 || !Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != "https" && uri.Scheme != "http") || string.IsNullOrEmpty(uri.Host) || uri.UserInfo.Length > 0)
            throw new TaskValidationException("平台链接仅支持完整的 http/https 地址，且不能包含用户名或密码。");
        return uri;
    }
}
