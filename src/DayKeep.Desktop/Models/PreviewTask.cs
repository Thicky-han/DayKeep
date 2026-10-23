using DayKeep.Localization;

namespace DayKeep.Models;

public sealed record PreviewTask(string TitleKey, string CategoryKey, string StatusKey, string TimeKey,
    string DescriptionKey, string Icon, string Tint, string Accent)
{
    public string Title => Localizer.T(TitleKey);
    public string Category => Localizer.T(CategoryKey);
    public string Status => Localizer.T(StatusKey);
    public string Time => Localizer.T(TimeKey);
    public string Description => Localizer.T(DescriptionKey);
    public static IReadOnlyList<PreviewTask> Examples { get; } =
    [
        new("查看专利缴费入口", "等待开放", "等待条件满足", "每天 09:00", "今天查过还未开放？记录一次检查，明天继续跟进。", "◷", "#EEF3E9", "#697C45"),
        new("提交论文修改稿", "待办理", "尚未完成", "每天 14:00", "现在来不及处理，可以稍后提醒，直到明确完成。", "↗", "#F8EEE6", "#B67B4D"),
        new("确认项目报销进度", "持续跟进", "等待反馈", "工作日 16:30", "把需要反复查看的事项留在列表里，等到有结果。", "≋", "#EAF0F5", "#678294")
    ];
}
