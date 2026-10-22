using System.Globalization;

namespace DayKeep.Core;

public sealed record ReminderProgress(Guid TaskId, DateOnly LocalDate, int ExtraCount = 0,
    DateTimeOffset? NextRepeatAt = null, DateTimeOffset? SnoozedUntil = null, int Version = 0, DateTimeOffset? LastDeliveredAt = null);
public sealed record ReminderSnapshot(IReadOnlyList<TaskItem> Tasks, IReadOnlyList<TaskCheck> Checks,
    IReadOnlyList<ReminderProgress>? SavedProgress = null)
{
    public HashSet<Guid> CheckedWaitingTasks => Checks.Where(c => c.Outcome == CheckOutcome.StillWaiting).Select(c => c.TaskId).ToHashSet();
    public IReadOnlyList<ReminderProgress> Progress => SavedProgress ?? [];
}
public enum ReminderKind { Fixed, Repeat, Snooze }
public readonly record struct ReminderKey(Guid TaskId, DateOnly Date, TimeOnly Time, ReminderKind Kind = ReminderKind.Fixed);
public sealed record ReminderOccurrence(TaskItem Task, ReminderKey Key, DateTimeOffset ScheduledAt);

public static class ReminderPlanner
{
    public static DateOnly LocalDate(DateTimeOffset now, TimeZoneInfo zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
    public static bool Active(TaskItem task) => task.ArchivedAt is null && !task.Paused && task.State != TaskState.Completed;
    public static IReadOnlyList<ReminderOccurrence> Due(ReminderSnapshot snapshot, DateTimeOffset now, DateTimeOffset watchingSince, TimeZoneInfo zone)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var date = DateOnly.FromDateTime(localNow.DateTime);
        var minute = new TimeOnly(localNow.Hour, localNow.Minute);
        var checkedTasks = snapshot.Checks.Where(c => c.LocalDate == date && c.Outcome == CheckOutcome.StillWaiting).Select(c => c.TaskId).ToHashSet();
        var result = new List<ReminderOccurrence>();
        foreach (var task in snapshot.Tasks)
        {
            if (!Active(task) || (task.State == TaskState.Waiting && checkedTasks.Contains(task.Id))) continue;
            var progress = snapshot.Progress.SingleOrDefault(p => p.TaskId == task.Id);
            if (progress?.SnoozedUntil is { } snooze)
            {
                if (snooze <= now) result.Add(Occurrence(task, snooze, ReminderKind.Snooze, zone));
                continue; // Explicit postponement overrides every intervening automatic slot.
            }
            if (!OnDay(task, date)) continue;
            ReminderOccurrence? fixedSlot = null;
            foreach (var time in Times(task))
            {
                if (time != minute || At(date,time,zone) is not { } at || at < watchingSince || at < task.UpdatedAt || at > now || at <= progress?.LastDeliveredAt) continue;
                fixedSlot = Occurrence(task, at, ReminderKind.Fixed, zone);
            }
            if (fixedSlot is not null) result.Add(fixedSlot);
            else if (progress is not null && progress.LocalDate == date && progress.ExtraCount < task.Reminder.MaxExtraReminders
                && progress.NextRepeatAt is { } repeat && LocalDate(repeat,zone) == date && repeat <= now)
                result.Add(Occurrence(task, repeat, ReminderKind.Repeat, zone));
        }
        return result;
    }
    public static DateTimeOffset? Next(TaskItem task, IReadOnlySet<Guid> checkedToday, DateTimeOffset now, TimeZoneInfo zone, ReminderProgress? progress = null)
    {
        if (!Active(task)) return null;
        var today = LocalDate(now, zone);
        if (progress?.SnoozedUntil is { } snooze && !(task.State == TaskState.Waiting && checkedToday.Contains(task.Id))) return snooze;
        DateTimeOffset? next = progress is not null && progress.LocalDate == today && OnDay(task,today)
            && progress.ExtraCount < task.Reminder.MaxExtraReminders && !(task.State == TaskState.Waiting && checkedToday.Contains(task.Id))
            && progress.NextRepeatAt is { } repeat && LocalDate(repeat,zone) == today ? repeat : null;
        for (var day = 0; day <= 7; day++)
        {
            var date = today.AddDays(day);
            if (!OnDay(task,date) || (day == 0 && task.State == TaskState.Waiting && checkedToday.Contains(task.Id))) continue;
            foreach (var time in Times(task)) if (At(date,time,zone) is { } at && at > now && at >= task.UpdatedAt) return next is { } extra && extra < at ? extra : at;
        }
        return next;
    }
    public static ReminderProgress AfterDelivery(ReminderOccurrence occurrence, ReminderProgress? previous, DateTimeOffset now, TimeZoneInfo zone)
    {
        var date = LocalDate(now,zone);
        var count = previous?.LocalDate == date ? previous.ExtraCount : 0;
        if (occurrence.Key.Kind == ReminderKind.Repeat) count++;
        var next = occurrence.ScheduledAt.AddMinutes(occurrence.Task.Reminder.RepeatMinutes);
        // Skip elapsed intervals without a burst, keeping the original cadence instead of timer drift.
        if (next <= now) next = next.AddMinutes((Math.Floor((now-next).TotalMinutes / occurrence.Task.Reminder.RepeatMinutes)+1) * occurrence.Task.Reminder.RepeatMinutes);
        return new(occurrence.Task.Id,date,count,
            count < occurrence.Task.Reminder.MaxExtraReminders && LocalDate(next,zone) == date ? next : null,
            null,previous?.Version ?? 0,now);
    }
    public static int DeadlinePriority(TaskItem task, DateOnly today) => !Active(task) || task.DueDate is null ? 3 : task.DueDate < today ? 0 : task.DueDate == today ? 1 : 2;
    private static ReminderOccurrence Occurrence(TaskItem task, DateTimeOffset at, ReminderKind kind, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(at,zone);
        return new(task,new(task.Id,DateOnly.FromDateTime(local.DateTime),TimeOnly.FromDateTime(local.DateTime),kind),at);
    }
    private static bool OnDay(TaskItem task, DateOnly date) => (task.Reminder.Weekdays & (1 << (((int)date.DayOfWeek + 6) % 7))) != 0;
    private static IEnumerable<TimeOnly> Times(TaskItem task) => task.Reminder.Times.Split(',', StringSplitOptions.TrimEntries)
        .Select(t => TimeOnly.ParseExact(t,"HH:mm",CultureInfo.InvariantCulture)).Distinct().Order();
    private static DateTimeOffset? At(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local)) return null;
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}
