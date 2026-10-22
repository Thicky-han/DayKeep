namespace DayKeep.Core;

public sealed class TaskService(TaskRepository repository, Func<DateTimeOffset>? clock = null)
{
    private DateTimeOffset Now => (clock ?? (() => DateTimeOffset.UtcNow))();
    public TaskRepository Repository => repository;
    public void Initialize() => repository.Initialize();
    public IReadOnlyList<TaskItem> LoadAll() => repository.LoadAll();
    public TaskItem Create(TaskDraft draft) => Commit(TaskRules.Create(draft, Now), null, "created");
    public TaskItem Edit(TaskItem item, TaskDraft draft) => Commit(TaskRules.Edit(item, draft, Now), item.Revision, "edited");
    public TaskItem TogglePause(TaskItem item) => Commit(TaskRules.Pause(item, Now), item.Revision, item.Paused ? "resumed" : "paused");
    public TaskItem Complete(TaskItem item) => Commit(TaskRules.Complete(item, Now), item.Revision, "completed");
    public TaskItem UndoComplete(TaskItem item) => Commit(TaskRules.UndoComplete(item, Now), item.Revision, "completion-undone");
    public TaskItem Archive(TaskItem item) => Commit(TaskRules.Archive(item, Now), item.Revision, "archived");
    public TaskItem Restore(TaskItem item) => Commit(TaskRules.Restore(item, Now), item.Revision, "restored");

    // L1 establishes the storage contract. The user-facing daily check action belongs to L2.
    public TaskItem RecordWaitingCheck(TaskItem item, DateOnly localDate, string notes)
    {
        TaskRules.RequireActive(item);
        if (item.State != TaskState.Waiting || item.Paused) throw new TaskValidationException("只有未暂停的等待条件任务可以记录检查。");
        if (notes.Length > 4000) throw new TaskValidationException("检查备注最多 4000 个字符。");
        var now = Now;
        var updated = TaskRules.Advance(item, now);
        repository.Save(updated, item.Revision, "checked-still-waiting", new(Guid.NewGuid(), item.Id, localDate, now, notes.Trim()));
        return updated;
    }
    private TaskItem Commit(TaskItem item, int? expected, string kind) { repository.Save(item, expected, kind); return item; }
}
