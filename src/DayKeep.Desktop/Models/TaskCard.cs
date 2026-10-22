using System.Globalization;
using System.Windows;
using DayKeep.Core;

namespace DayKeep.Models;

public sealed record TaskCard(TaskItem Item)
{
    public string Title => Item.Title;
    public string Status => Item.ArchivedAt is not null ? "已归档" : Item.Paused ? "已暂停" : Item.State switch { TaskState.Waiting => "等待条件满足", TaskState.Ready => "待办理", _ => "已完成" };
    public string Time => $"{Days(Item.Reminder.Weekdays)}  {Item.Reminder.Times}";
    public string Summary => Item.DueDate is { } due ? $"截止 {due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" : "截止日期未确定";
    public string Icon => Item.State == TaskState.Completed ? "✓" : Item.Kind == TaskKind.WaitingForCondition ? "◷" : "↗";
    public string Tint => Item.State == TaskState.Ready ? "#F8EEE6" : "#EEF3E9";
    public string Accent => Item.State == TaskState.Ready ? "#9C683E" : "#567248";
    public string PauseText => Item.Paused ? "恢复跟进" : "暂停";
    public Visibility ActiveVisibility => Item.ArchivedAt is null && Item.State != TaskState.Completed ? Visibility.Visible : Visibility.Collapsed;
    public Visibility UndoVisibility => Item.ArchivedAt is null && Item.State == TaskState.Completed ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ArchiveVisibility => Item.ArchivedAt is null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RestoreVisibility => Item.ArchivedAt is not null ? Visibility.Visible : Visibility.Collapsed;
    public static string Days(int mask) => mask == 127 ? "每天" : mask == 31 ? "工作日" : string.Join("、", Enumerable.Range(0, 7).Where(i => (mask & (1 << i)) != 0).Select(i => new[] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" }[i]));
}
