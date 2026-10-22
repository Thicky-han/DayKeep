using System.Globalization;

namespace DayKeep.Core;

public sealed record ReminderSnapshot(IReadOnlyList<TaskItem> Tasks, IReadOnlyList<TaskCheck> Checks)
{
    public HashSet<Guid> CheckedWaitingTasks => Checks.Where(c => c.Outcome == CheckOutcome.StillWaiting).Select(c => c.TaskId).ToHashSet();
}
public readonly record struct ReminderKey(Guid TaskId, DateOnly Date, TimeOnly Time);
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
            if (!Active(task) || (task.State == TaskState.Waiting && checkedTasks.Contains(task.Id)) || !OnDay(task, date)) continue;
            foreach (var time in Times(task))
            {
                if (time != minute || At(date,time,zone) is not { } at || at < watchingSince || at < task.UpdatedAt || at > now) continue;
                result.Add(new(task, new(task.Id,date,time), at));
            }
        }
        return result;
    }
    public static DateTimeOffset? Next(TaskItem task, IReadOnlySet<Guid> checkedToday, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (!Active(task)) return null;
        var today = LocalDate(now, zone);
        for (var day = 0; day <= 7; day++)
        {
            var date = today.AddDays(day);
            if (!OnDay(task,date) || (day == 0 && task.State == TaskState.Waiting && checkedToday.Contains(task.Id))) continue;
            foreach (var time in Times(task)) if (At(date,time,zone) is { } at && at > now && at >= task.UpdatedAt) return at;
        }
        return null;
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
