namespace DayKeep.Core;

public sealed class TaskService(TaskRepository repository, Func<DateTimeOffset>? clock = null)
{
    private bool _initialized;
    private DateTimeOffset Now => (clock ?? (() => DateTimeOffset.UtcNow))();
    public TaskRepository Repository => repository;
    public void Initialize() { _initialized=false; repository.Initialize(); _initialized=true; }
    public IReadOnlyList<TaskItem> LoadAll() => repository.LoadAll();
    public TaskItem Create(TaskDraft draft) => Commit(TaskRules.Create(draft, Now), null, "created");
    public TaskItem Edit(TaskItem item, TaskDraft draft) => Commit(TaskRules.Edit(item, draft, Now), item.Revision, "edited");
    public TaskItem TogglePause(TaskItem item) => Commit(TaskRules.Pause(item, Now), item.Revision, item.Paused ? "resumed" : "paused");
    public TaskItem Complete(TaskItem item) => Commit(TaskRules.Complete(item, Now), item.Revision, "completed");
    public TaskItem UndoComplete(TaskItem item) => Commit(TaskRules.UndoComplete(item, Now), item.Revision, "completion-undone");
    public TaskItem Archive(TaskItem item) => Commit(TaskRules.Archive(item, Now), item.Revision, "archived");
    public TaskItem Restore(TaskItem item) => Commit(TaskRules.Restore(item, Now), item.Revision, "restored");

    public ReminderSnapshot ReminderSnapshot(DateOnly date) { RequireInitialized(); return repository.LoadReminderSnapshot(date); }
    public TaskItem MarkReady(TaskItem item, DateOnly localDate)
    {
        RequireInitialized();
        var now = Now;
        var updated = TaskRules.MarkReady(item, now);
        repository.Save(updated, item.Revision, "condition-ready", new(Guid.NewGuid(), item.Id, localDate, now, "用户确认条件已满足", CheckOutcome.ConditionReady),
            localDate, nextRepeat: now.AddMinutes(item.Reminder.RepeatMinutes));
        return updated;
    }
    public TaskItem Snooze(TaskItem item, DateOnly localDate, int minutes)
    {
        RequireInitialized(); TaskRules.RequireActive(item);
        if (item.Paused || minutes is not (10 or 30 or 60)) throw new TaskValidationException("请选择 10、30 或 60 分钟，且任务应处于跟进中。");
        if (item.State == TaskState.Waiting && ReminderSnapshot(localDate).CheckedWaitingTasks.Contains(item.Id))
            throw new TaskValidationException("今天已检查的任务会在下一提醒日继续跟进。");
        var now = Now; var updated = TaskRules.Advance(item,now);
        repository.Save(updated,item.Revision,"snoozed",reminderDate:localDate,snoozedUntil:now.AddMinutes(minutes));
        return updated;
    }
    public void RecordDelivery(ReminderOccurrence occurrence, ReminderProgress? previous, DateTimeOffset now, TimeZoneInfo zone)
    {
        RequireInitialized();
        repository.SaveDelivery(occurrence.Task,ReminderPlanner.AfterDelivery(occurrence,previous,now,zone));
    }
    public TaskItem RecordWaitingCheck(TaskItem item, DateOnly localDate, string notes)
    {
        RequireInitialized();
        TaskRules.RequireActive(item);
        if (item.State != TaskState.Waiting || item.Paused) throw new TaskValidationException("只有未暂停的等待条件任务可以记录检查。");
        if (notes.Length > 4000) throw new TaskValidationException("检查备注最多 4000 个字符。");
        var now = Now;
        var updated = TaskRules.Advance(item, now);
        repository.Save(updated, item.Revision, "checked-still-waiting", new(Guid.NewGuid(), item.Id, localDate, now, notes.Trim()));
        return updated;
    }
    private void RequireInitialized() { if(!_initialized) throw new StoreVersionException("任务库尚未成功打开，提醒已暂停。请检查数据并重新读取。"); }
    private TaskItem Commit(TaskItem item, int? expected, string kind) { RequireInitialized(); repository.Save(item, expected, kind); return item; }
}
