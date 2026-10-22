using System.Globalization;
using System.Windows;
using DayKeep.Core;

namespace DayKeep.Models;

public sealed record TaskCard(TaskItem Item, bool CheckedToday = false, DateOnly? Today = null, DateTimeOffset? SnoozedUntil = null, TimeZoneInfo? Zone = null, string ReminderDetail = "")
{
    public string Title => Item.Title;
    public string Status => Item.ArchivedAt is not null ? "已归档" : Item.Paused ? "已暂停" : Item.State switch { TaskState.Waiting => CheckedToday ? "今日已检查，尚未开放" : "等待条件满足", TaskState.Ready => "待办理", _ => "已完成" };
    public string Time => $"{Days(Item.Reminder.Weekdays)}  {Item.Reminder.Times}";
    public string Summary => Item.DueDate is not { } due ? "截止日期未确定" : !ReminderPlanner.Active(Item) ? $"截止 {due:yyyy-MM-dd}"
        : due < (Today ?? DateOnly.FromDateTime(DateTime.Now)) ? $"已逾期 {((Today ?? DateOnly.FromDateTime(DateTime.Now)).DayNumber-due.DayNumber)} 天 · 继续跟进"
        : due == (Today ?? DateOnly.FromDateTime(DateTime.Now)) ? "今天截止 · 请优先处理" : $"截止 {due:yyyy-MM-dd}";
    public string DeadlineAccent => ReminderPlanner.DeadlinePriority(Item,Today ?? DateOnly.FromDateTime(DateTime.Now)) <= 1 ? "#A14839" : "#68746F";
    public string Followup => SnoozedUntil is { } at ? $"稍后提醒至 {TimeZoneInfo.ConvertTime(at,Zone ?? TimeZoneInfo.Local):M月d日 HH:mm}" : Item.Reminder.MaxExtraReminders == 0 ? "自动追加已关闭" : $"未处理每 {Item.Reminder.RepeatMinutes} 分钟追加 · 每天最多 {Item.Reminder.MaxExtraReminders} 次";
    public string Icon => Item.State == TaskState.Completed ? "✓" : Item.Kind == TaskKind.WaitingForCondition ? "◷" : "↗";
    public string Tint => Item.State == TaskState.Ready ? "#F8EEE6" : "#EEF3E9";
    public string Accent => Item.State == TaskState.Ready ? "#9C683E" : "#567248";
    public string PauseText => Item.Paused ? "恢复跟进" : "暂停";
    public Visibility ActiveVisibility => Item.ArchivedAt is null && Item.State != TaskState.Completed ? Visibility.Visible : Visibility.Collapsed;
    public Visibility UndoVisibility => Item.ArchivedAt is null && Item.State == TaskState.Completed ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ArchiveVisibility => Item.ArchivedAt is null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RestoreVisibility => Item.ArchivedAt is not null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility WaitingVisibility => ReminderPlanner.Active(Item) && Item.State == TaskState.Waiting ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CheckVisibility => !CheckedToday ? WaitingVisibility : Visibility.Collapsed;
    public Visibility ReadyVisibility => ReminderPlanner.Active(Item) && Item.State == TaskState.Ready ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LinkVisibility => !string.IsNullOrEmpty(Item.Url) ? Visibility.Visible : Visibility.Collapsed;
    public string ReminderInstruction => Item.State == TaskState.Waiting ? "查看办理条件是否已开放。今天还不能办理，也可以记录一次检查。" : "条件已经具备；明确办理完成后，再结束这条任务。";
    public static string Days(int mask) => mask == 127 ? "每天" : mask == 31 ? "工作日" : string.Join("、", Enumerable.Range(0, 7).Where(i => (mask & (1 << i)) != 0).Select(i => new[] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" }[i]));
}
