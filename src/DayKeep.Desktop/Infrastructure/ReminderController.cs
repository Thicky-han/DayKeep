using System.ComponentModel;
using System.Windows.Threading;
using DayKeep.Core;
using DayKeep.Localization;

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
    private readonly Dictionary<Guid,TaskItem> _pending = [];
    private IReadOnlyList<ReminderKey> _visibleKeys = [];
    private readonly bool _systemZone;
    private bool _disposed;
    private bool _ticking;
    private string? _lastStatus;
    private string? _statusKey;
    private object[] _statusArgs = [];
    private DateOnly? _displayDate;
    private IReadOnlyDictionary<Guid,(DateTimeOffset At,bool Missed)> _displayTiming = new Dictionary<Guid,(DateTimeOffset,bool)>();
    private DateOnly? _observedDay;
    public TimeZoneInfo Zone { get; private set; }
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
        _systemZone = zone is null; Zone = zone ?? TimeZoneInfo.Local; _canShow = canShow ?? DesktopAvailability.IsAvailable;
        _openLink = openLink ?? ExternalLinks.Open; _watchingSince = Now;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        Localizer.Changed += RefreshLanguage;
    }
    public void Start() { _timer.Start(); Tick(); }
    public void RefreshEnvironment()
    {
        if(_systemZone) { TimeZoneInfo.ClearCachedData(); Zone=TimeZoneInfo.Local; }
        Tick(); TasksChanged?.Invoke();
    }
    private void OnTick(object? sender, EventArgs e) => Tick();
    public void Tick()
    {
        if (_disposed || _ticking) return;
        _ticking = true;
        try
        {
            if (!_canShow()) { Window?.Hide(); SetStatus("提醒已保留 · 等待桌面可用或退出全屏"); return; }
            var now = Now;
            var date = ReminderPlanner.LocalDate(now,Zone);
            var dayChanged = _observedDay != date;
            if (dayChanged) { _observedDay = date; DayChanged?.Invoke(date); }
            var snapshot = _tasks.ReminderSnapshot(date);
            var checkedIds = snapshot.CheckedWaitingTasks;
            var due = ReminderPlanner.Due(snapshot,now,_watchingSince,Zone,recoverMissed:true);
            foreach (var occurrence in due)
            {
                _tasks.QueueDelivery(occurrence,snapshot.Progress.SingleOrDefault(p=>p.TaskId==occurrence.Task.Id),now,Zone);
            }
            if(due.Count>0) snapshot=_tasks.ReminderSnapshot(date);
            var current = snapshot.Tasks.ToDictionary(t => t.Id);
            var visible = snapshot.Deliveries.Where(d=>d.State is DeliveryState.Pending or DeliveryState.Shown
                && current.TryGetValue(d.Key.TaskId,out var task) && task.Revision==d.TaskRevision && ReminderPlanner.Active(task)
                && !(task.State==TaskState.Waiting && checkedIds.Contains(task.Id))
                && !snapshot.Progress.Any(p=>p.TaskId==task.Id && p.SnoozedUntil>now)).ToList();
            var ids=visible.Select(d=>d.Key.TaskId).Distinct().ToArray();
            var changed=dayChanged || !_visibleKeys.SequenceEqual(visible.Select(d=>d.Key)) || ids.Any(id=>!_pending.TryGetValue(id,out var old) || old!=current[id]);
            _visibleKeys=visible.Select(d=>d.Key).ToArray(); _pending.Clear(); foreach(var id in ids) _pending.Add(id,current[id]);
            if (_pending.Count == 0) Window?.CloseSilently();
            else
            {
                var created = Window is null;
                if (created)
                {
                    Window = new DueReminderWindow(Act,_showTasks,TryDismiss);
                    Window.Closed += (_, _) => { Window = null; _pending.Clear(); };
                }
                if (created || changed)
                {
                    _displayDate=date;
                    _displayTiming=visible.GroupBy(d=>d.Key.TaskId).ToDictionary(g=>g.Key,g=>
                    {
                        var at=g.Max(d=>d.ScheduledAt);
                        return (at,now-at>=TimeSpan.FromMinutes(1));
                    });
                    RefreshVisibleText();
                }
                var showing = !Window!.IsVisible;
                if (showing)
                {
                    var foreground = DesktopAvailability.ForegroundWindow;
                    Window!.Show();
                    LastShowPreservedFocus = foreground == DesktopAvailability.ForegroundWindow;
                }
                var pending=visible.Where(d=>d.State==DeliveryState.Pending).Select(d=>d.Key).ToArray();
                if(pending.Length>0) _tasks.SetDeliveryState(pending,DeliveryState.Shown);
                if (showing) Presented?.Invoke(Window!);
            }
            if (due.Count > 0) { snapshot = _tasks.ReminderSnapshot(date); TasksChanged?.Invoke(); }
            var next = snapshot.Tasks.Select(t => ReminderPlanner.Next(t,checkedIds,now,Zone,snapshot.Progress.SingleOrDefault(p=>p.TaskId==t.Id))).Where(t => t is not null).Min();
            if(next is { } at) SetStatus("定时提醒已启用 · 下次 {0}",TimeZoneInfo.ConvertTime(at,Zone));
            else SetStatus("定时提醒已启用 · 暂无待提醒事项");
        }
        catch (Exception error) when (MainWindow.DataError(error) is not null)
        {
            var message = "提醒暂不可用，请检查数据后重新读取。";
            SetStatus(message); Window?.ShowError(message,true);
        }
        finally { _ticking = false; }
    }
    private bool TryDismiss()
    {
        try { _tasks.SetDeliveryState(_visibleKeys,DeliveryState.Dismissed); return true; }
        catch(Exception error) when(MainWindow.DataError(error) is not null)
        { Window?.ShowError("本次收起未能保存，请重新读取数据后重试。提醒已保留。"); return false; }
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
            Window?.ShowError(()=>MainWindow.DataError(error) ?? Localizer.T("无法打开平台，请检查默认浏览器后重试。任务状态未改变。"));
            Tick();
        }
    }
    // Language changes only redraw the retained delivery model. They never poll or queue reminders.
    private void RefreshLanguage(object? sender,EventArgs e)
    {
        if(_disposed) return;
        RefreshVisibleText();
        PublishStatus();
    }
    private void RefreshVisibleText()
    {
        if(Window is null || _displayDate is not { } date) return;
        Window.SetTasks(_pending.Values.ToList(),date,_displayTiming.ToDictionary(p=>p.Key,p=>p.Value.Missed
            ? Localizer.T("补提醒 · 原定 {0}",TimeZoneInfo.ConvertTime(p.Value.At,Zone).ToString("g",Localizer.Culture))
            : Localizer.T("按计划提醒 · 未处理将继续跟进")));
    }
    private void SetStatus(string key,params object[] args) { _statusKey=key; _statusArgs=args; PublishStatus(); }
    private void PublishStatus()
    {
        if(_statusKey is null) return;
        var args=_statusArgs.Select(value=>value is DateTimeOffset at ? at.ToString("g",Localizer.Culture) : value).ToArray();
        var status=Localizer.T(_statusKey,args);
        if (_lastStatus == status) return; _lastStatus = status; StatusChanged?.Invoke(status);
    }
    public void Dispose()
    {
        _disposed = true; _timer.Stop(); _timer.Tick -= OnTick; Localizer.Changed -= RefreshLanguage; Window?.CloseSilently();
    }
}
