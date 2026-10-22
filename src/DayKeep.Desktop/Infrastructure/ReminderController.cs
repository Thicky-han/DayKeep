using System.ComponentModel;
using System.Windows.Threading;
using DayKeep.Core;

namespace DayKeep.Infrastructure;

public sealed class ReminderController : IDisposable
{
    private readonly TaskService _tasks;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<bool> _canShow;
    private readonly Action<Uri> _openLink;
    private readonly Action _showTasks;
    private readonly DispatcherTimer _timer;
    private readonly DateTimeOffset _watchingSince;
    private readonly HashSet<ReminderKey> _shown = [];
    private readonly Dictionary<Guid,TaskItem> _pending = [];
    private bool _disposed;
    private bool _ticking;
    private string? _lastStatus;
    private DateOnly? _observedDay;
    public TimeZoneInfo Zone { get; }
    public DateTimeOffset Now => _clock();
    public DueReminderWindow? Window { get; private set; }
    public bool? LastShowPreservedFocus { get; private set; }
    public event Action<string>? StatusChanged;
    public event Action? TasksChanged;
    public event Action<DueReminderWindow>? Presented;
    public event Action<DateOnly>? DayChanged;

    public ReminderController(TaskService tasks, Action showTasks, Func<DateTimeOffset>? clock = null, TimeZoneInfo? zone = null,
        Func<bool>? canShow = null, Action<Uri>? openLink = null)
    {
        _tasks = tasks; _showTasks = showTasks; _clock = clock ?? (() => DateTimeOffset.UtcNow);
        Zone = zone ?? TimeZoneInfo.Local; _canShow = canShow ?? DesktopAvailability.IsAvailable;
        _openLink = openLink ?? ExternalLinks.Open; _watchingSince = Now;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
    }
    public void Start() { _timer.Start(); Tick(); }
    private void OnTick(object? sender, EventArgs e) => Tick();
    public void Tick()
    {
        if (_disposed || _ticking) return;
        _ticking = true;
        try
        {
            if (!_canShow()) { SetStatus("定时提醒等待桌面可用"); return; }
            var now = Now;
            var date = ReminderPlanner.LocalDate(now,Zone);
            var dayChanged = _observedDay != date;
            if (dayChanged) { _observedDay = date; DayChanged?.Invoke(date); }
            var snapshot = _tasks.ReminderSnapshot(date);
            var checkedIds = snapshot.CheckedWaitingTasks;
            var current = snapshot.Tasks.ToDictionary(t => t.Id);
            var changed = dayChanged;
            foreach (var id in _pending.Keys.ToArray())
            {
                if (!current.TryGetValue(id,out var item) || !ReminderPlanner.Active(item) || (item.State == TaskState.Waiting && checkedIds.Contains(id))
                    || snapshot.Progress.Any(p => p.TaskId == id && p.SnoozedUntil > now)) { _pending.Remove(id); changed = true; }
                else if (_pending[id] != item) { _pending[id] = item; changed = true; }
            }
            _shown.RemoveWhere(key => key.Date < date.AddDays(-7));
            var due = ReminderPlanner.Due(snapshot,now,_watchingSince,Zone).Where(o => !_shown.Contains(o.Key)).ToList();
            foreach (var occurrence in due) { _pending[occurrence.Task.Id] = occurrence.Task; changed = true; }
            if (_pending.Count == 0) Window?.Close();
            else
            {
                var created = Window is null;
                if (created)
                {
                    Window = new DueReminderWindow(Act,_showTasks);
                    Window.Closed += (_, _) => { Window = null; _pending.Clear(); };
                }
                if (created || changed) Window!.SetTasks(_pending.Values.ToList(),date);
                if (created)
                {
                    var foreground = DesktopAvailability.ForegroundWindow;
                    Window!.Show();
                    LastShowPreservedFocus = foreground == DesktopAvailability.ForegroundWindow;
                }
                foreach (var occurrence in due)
                {
                    _tasks.RecordDelivery(occurrence,snapshot.Progress.SingleOrDefault(p=>p.TaskId==occurrence.Task.Id),now,Zone);
                    _shown.Add(occurrence.Key);
                }
                if (created) Presented?.Invoke(Window!);
            }
            if (due.Count > 0) { snapshot = _tasks.ReminderSnapshot(date); TasksChanged?.Invoke(); }
            var next = snapshot.Tasks.Select(t => ReminderPlanner.Next(t,checkedIds,now,Zone,snapshot.Progress.SingleOrDefault(p=>p.TaskId==t.Id))).Where(t => t is not null).Min();
            SetStatus(next is { } at ? $"定时提醒已启用 · 下次 {TimeZoneInfo.ConvertTime(at,Zone):M月d日 HH:mm}" : "定时提醒已启用 · 暂无待提醒事项");
        }
        catch (Exception error) when (MainWindow.DataError(error) is not null)
        {
            var message = "提醒暂不可用，请检查数据后重新读取。";
            SetStatus(message); Window?.ShowError(message,true);
        }
        finally { _ticking = false; }
    }
    public void Act(TaskItem item, ReminderAction action)
    {
        try
        {
            if (action == ReminderAction.OpenLink) { _openLink(WebLink.Parse(item.Url)); return; }
            Window?.ClearError();
            var date = ReminderPlanner.LocalDate(Now,Zone);
            switch (action)
            {
                case ReminderAction.CheckedWaiting: _tasks.RecordWaitingCheck(item,date,"用户已检查，尚未开放"); break;
                case ReminderAction.MarkReady: _tasks.MarkReady(item,date); break;
                case ReminderAction.Complete: _tasks.Complete(item); break;
                case ReminderAction.Snooze10: _tasks.Snooze(item,date,10); break;
                case ReminderAction.Snooze30: _tasks.Snooze(item,date,30); break;
                case ReminderAction.Snooze60: _tasks.Snooze(item,date,60); break;
            }
            Tick(); TasksChanged?.Invoke();
        }
        catch (Exception error) when (MainWindow.DataError(error) is not null || error is Win32Exception or InvalidOperationException)
        {
            Window?.ShowError(MainWindow.DataError(error) ?? "无法打开平台，请检查默认浏览器后重试。任务状态未改变。");
            Tick();
        }
    }
    private void SetStatus(string status) { if (_lastStatus == status) return; _lastStatus = status; StatusChanged?.Invoke(status); }
    public void Dispose()
    {
        _disposed = true; _timer.Stop(); _timer.Tick -= OnTick; Window?.Close();
    }
}
