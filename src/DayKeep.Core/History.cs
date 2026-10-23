namespace DayKeep.Core;

public sealed record HistoryQuery(string Text="",Guid? TaskId=null,DateOnly? From=null,DateOnly? To=null,
    IReadOnlyDictionary<string,string>? ActionLabels=null);
public sealed record HistoryEntry(Guid Id,Guid TaskId,string Title,string Kind,DateTimeOffset RecordedAt,DateOnly LocalDate,string Notes)
{
    public string Action=>Kind switch
    {
        "check-waiting"=>"今日已检查，尚未开放", "check-ready"=>"已开放，转待办理", "created"=>"创建事项",
        "edited"=>"编辑事项","paused"=>"暂停跟进","resumed"=>"恢复跟进","completed"=>"确认完成",
        "completion-undone"=>"撤销完成","archived"=>"归档","restored"=>"恢复归档","snoozed"=>"稍后提醒",
        "backup-imported"=>"从备份导入",_=>"操作记录"
    };
}
public sealed record HistoryResult(IReadOnlyList<HistoryEntry> Entries,bool HasMore);
