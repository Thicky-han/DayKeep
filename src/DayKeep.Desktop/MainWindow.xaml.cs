using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DayKeep.Infrastructure;
using DayKeep.Models;
using DayKeep.Core;
using Microsoft.Data.Sqlite;
using System.IO;

namespace DayKeep;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore;
    private ShellSettings _settings;
    private HotkeyService? _hotkey;
    private ReminderWindow? _preview;
    private bool _ready;
    private readonly TaskService _tasks;
    private IReadOnlyList<TaskItem> _allTasks = [];
    private bool _dataReady;
    private TaskEditorWindow? _editor;
    private HashSet<Guid> _checkedToday = [];
    private IReadOnlyList<ReminderProgress> _progress = [];
    public ReminderController? Reminders { get; private set; }
    public DateOnly Today => ReminderPlanner.LocalDate(Reminders?.Now ?? DateTimeOffset.UtcNow,Reminders?.Zone ?? TimeZoneInfo.Local);
    public TaskService Tasks => _tasks;
    public TaskEditorWindow? Editor => _editor;
    public bool AllowClose { get; set; }
    public bool IsCompact => _settings.Compact;
    public double SurfaceOpacity => SurfaceBrush.Opacity;
    public bool GlobalHotkeyRegistered => _hotkey?.Registered == true;

    public MainWindow(SettingsStore settingsStore, TaskService? tasks = null)
    {
        _settingsStore = settingsStore;
        _settings = settingsStore.Load();
        _tasks = tasks ?? new TaskService(new TaskRepository(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DayKeep", "tasks.db")));
        InitializeComponent();
        DateLabel.Text = DateTime.Today.ToString("M 月 d 日 · dddd", CultureInfo.GetCultureInfo("zh-CN"));
        RoadmapText.Text = "L0   工程基础与半透明桌面界面  ·  已完成\n\nL1   任务管理、状态模型与本地数据库  ·  已完成\n\nL2   固定时间、高亮弹窗与每日跟进  ·  已完成\n\nL3   未完成重复、稍后提醒与截止日期  ·  当前阶段\n\nL4   重启 / 休眠补提醒、去重与启动设置\n\nL5   历史记录、备份恢复与体验验收\n\nL6   图标定稿、安装包、开源发行与使用说明";
        OpacitySlider.Value = _settings.SurfaceOpacity;
        ApplyAppearance();
        _ready = true;
        ReloadTasks();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_settingsStore.LastError is { } error) StatusLabel.Text = error;
        var work = SystemParameters.WorkArea;
        if (Height > work.Height) Height = work.Height;
        if (Width > work.Width) SetCompact(true);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hotkey = new HotkeyService(this, ToggleVisibility);
        if (!_hotkey.Registered)
        {
            ShortcutLabel.Text = "快捷键已被占用 · 可单击托盘打开";
            StatusLabel.Text = "快捷键被占用 · 可单击托盘打开";
        }
    }

    public void ToggleVisibility() { if (IsVisible) HideToTray(); else ShowFromTray(); }
    public void HideToTray() { _preview?.Close(); _editor?.Hide(); Hide(); }
    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        if (_editor is not null) _editor.Show();
    }
    public void SetCompact(bool compact)
    {
        _settings = _settings with { Compact = compact };
        ApplyAppearance();
        PersistAppearance();
    }
    public void SetSurfaceOpacity(double opacity) => OpacitySlider.Value = Math.Clamp(opacity, 0.65, 0.98);

    private void ApplyAppearance()
    {
        SurfaceBrush.Opacity = _settings.SurfaceOpacity;
        OpacityLabel.Text = $"{_settings.SurfaceOpacity:P0}";
        Topmost = _settings.Pinned;
        PinButton.Content = _settings.Pinned ? "已置顶" : "置顶";
        Sidebar.Visibility = _settings.Compact ? Visibility.Collapsed : Visibility.Visible;
        SidebarColumn.Width = new GridLength(_settings.Compact ? 0 : 207);
        Width = _settings.Compact ? 450 : 1060;
        CompactButton.Content = _settings.Compact ? "展开" : "窄窗";
        ShortcutLabel.Visibility = _settings.Compact ? Visibility.Collapsed : Visibility.Visible;
        if (IsLoaded)
        {
            var work = SystemParameters.WorkArea;
            Left = Math.Clamp(Left, work.Left, Math.Max(work.Left, work.Right - Width));
            Top = Math.Clamp(Top, work.Top, Math.Max(work.Top, work.Bottom - Height));
        }
    }

    private void PersistAppearance()
    {
        if (!_ready) return;
        _settingsStore.Save(_settings);
        if (_settingsStore.LastError is { } error) StatusLabel.Text = error;
    }

    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _settings = _settings with { SurfaceOpacity = e.NewValue };
        ApplyAppearance();
        PersistAppearance();
    }
    private void TogglePin(object sender, RoutedEventArgs e) { _settings = _settings with { Pinned = !_settings.Pinned }; ApplyAppearance(); PersistAppearance(); }
    private void ToggleCompact(object sender, RoutedEventArgs e) => SetCompact(!_settings.Compact);
    private void HideClick(object sender, RoutedEventArgs e) => HideToTray();
    private void DragHeader(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindButton(source)) return;
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private static bool FindButton(DependencyObject source)
    {
        while (source is not null)
        {
            if (source is Button) return true;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }
        return false;
    }
    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) { HideToTray(); e.Handled = true; } }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!AllowClose) { e.Cancel = true; HideToTray(); } }
    private void OnClosed(object? sender, EventArgs e) { Reminders?.Dispose(); _preview?.Close(); _editor?.Close(); _hotkey?.Dispose(); }

    public ReminderController EnableReminders(Func<DateTimeOffset>? clock = null, TimeZoneInfo? zone = null, Func<bool>? canShow = null, Action<Uri>? openLink = null)
    {
        if (Reminders is not null) return Reminders;
        Reminders = new ReminderController(_tasks,ShowFromTray,clock,zone,canShow,openLink);
        Reminders.StatusChanged += text => ReminderStatusLabel.Text = text;
        Reminders.TasksChanged += ReloadTasks;
        Reminders.DayChanged += _ => ReloadTasks();
        Reminders.Start();
        return Reminders;
    }

    private void NavigateToday(object sender, RoutedEventArgs e) => Navigate(TodayPage, TodayNav);
    private void NavigateRoadmap(object sender, RoutedEventArgs e) => Navigate(RoadmapPage, RoadmapNav);
    private void NavigateSettings(object sender, RoutedEventArgs e) => Navigate(SettingsPage, SettingsNav);
    private void Navigate(FrameworkElement page, Button active)
    {
        foreach (var item in new FrameworkElement[] { TodayPage, RoadmapPage, SettingsPage }) item.Visibility = item == page ? Visibility.Visible : Visibility.Collapsed;
        foreach (var button in new[] { TodayNav, RoadmapNav, SettingsNav }) button.Background = button == active ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 235, 225)) : System.Windows.Media.Brushes.Transparent;
    }

    private void PreviewClick(object sender, RoutedEventArgs e) => ShowReminderPreview();
    public void ShowReminderPreview() => OpenPreview(PreviewTask.Examples[0]);
    public ReminderWindow OpenPreview(PreviewTask task)
    {
        if (_preview is { IsVisible: true }) { _preview.Activate(); return _preview; }
        _preview = new ReminderWindow(task);
        _preview.Closed += (_, _) => _preview = null;
        _preview.Show();
        return _preview;
    }

    public void ReloadTasks()
    {
        try
        {
            _tasks.Initialize();
            var snapshot = _tasks.ReminderSnapshot(Today);
            _allTasks = snapshot.Tasks;
            _checkedToday = snapshot.CheckedWaitingTasks;
            _progress = snapshot.Progress;
            DateLabel.Text = Today.ToDateTime(TimeOnly.MinValue).ToString("M 月 d 日 · dddd",CultureInfo.GetCultureInfo("zh-CN"));
            _dataReady = true;
            DataErrorPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception error) when (DataError(error) is not null) { ShowDataError(DataError(error)!); }
        NewTaskButton.IsEnabled = _dataReady;
        TaskList.IsEnabled = _dataReady;
        ApplyTaskFilter();
        Reminders?.Tick();
    }
    private void ApplyTaskFilter()
    {
        if (!_ready) return;
        var filter = FilterInput.SelectedIndex;
        var visible = _allTasks.Where(t => filter switch { 0 => t.ArchivedAt is null && t.State != TaskState.Completed, 1 => t.ArchivedAt is null && t.State == TaskState.Completed, 2 => t.ArchivedAt is not null, _ => true })
            .OrderBy(t=>ReminderPlanner.DeadlinePriority(t,Today)).ThenBy(t=>t.DueDate)
            .Select(t => new TaskCard(t,_checkedToday.Contains(t.Id),Today,_progress.SingleOrDefault(p=>p.TaskId==t.Id)?.SnoozedUntil,Reminders?.Zone)).ToList();
        TaskList.ItemsSource = visible;
        TaskCountLabel.Text = $"事项 · {visible.Count}";
        EmptyPanel.Visibility = visible.Count == 0 && _dataReady ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = _allTasks.Count == 0 ? "这里，留给下一件重要的事。" : "这个分类里，暂时没有事项。";
        EmptyDescription.Text = _allTasks.Count == 0 ? "从一条任务开始。它会留在本机，等你下次打开。" : "切换上方分类，可以查看其他事项。";
    }
    private void FilterChanged(object sender, SelectionChangedEventArgs e) => ApplyTaskFilter();
    private void RetryLoad(object sender, RoutedEventArgs e) => ReloadTasks();
    private void NewTaskClick(object sender, RoutedEventArgs e) => OpenEditor(null);
    private void EditTaskClick(object sender, RoutedEventArgs e) => OpenEditor((TaskItem)((Button)sender).Tag);
    public TaskEditorWindow? OpenEditor(TaskItem? item)
    {
        if (!_dataReady) return null;
        if (_editor is not null) { _editor.Show(); _editor.Activate(); return _editor; }
        _editor = new TaskEditorWindow(item, draft =>
        {
            try
            {
                if (item is null) _tasks.Create(draft); else _tasks.Edit(item, draft);
                ReloadTasks();
                StatusLabel.Text = "●  任务已保存在本机";
                return null;
            }
            catch (Exception error) when (DataError(error) is not null) { return DataError(error); }
        }) { Owner = this };
        _editor.Closed += (_, _) => _editor = null;
        _editor.Show();
        return _editor;
    }
    private void PauseTaskClick(object sender, RoutedEventArgs e) => ChangeTask(sender, _tasks.TogglePause);
    private void CompleteTaskClick(object sender, RoutedEventArgs e) => ChangeTask(sender, _tasks.Complete);
    private void UndoTaskClick(object sender, RoutedEventArgs e) => ChangeTask(sender, _tasks.UndoComplete);
    private void ArchiveTaskClick(object sender, RoutedEventArgs e) => ChangeTask(sender, _tasks.Archive);
    private void RestoreTaskClick(object sender, RoutedEventArgs e) => ChangeTask(sender, _tasks.Restore);
    private void CheckTaskClick(object sender, RoutedEventArgs e) => ChangeTask(sender,t => _tasks.RecordWaitingCheck(t,Today,"用户已检查，尚未开放"));
    private void ReadyTaskClick(object sender, RoutedEventArgs e) => ChangeTask(sender,t => _tasks.MarkReady(t,Today));
    private void OpenLinkClick(object sender, RoutedEventArgs e)
    {
        try { ExternalLinks.Open(WebLink.Parse(((TaskItem)((Button)sender).Tag).Url)); }
        catch (Exception error) when (DataError(error) is not null || error is Win32Exception or InvalidOperationException)
        { StatusLabel.Text = "平台未能打开，请检查链接和默认浏览器。"; }
    }
    private void ChangeTask(object sender, Func<TaskItem, TaskItem> action)
    {
        try { action((TaskItem)((Button)sender).Tag); ReloadTasks(); StatusLabel.Text = "●  变更已保存在本机"; }
        catch (Exception error) when (DataError(error) is not null) { ShowDataError(DataError(error)!); }
    }
    private void ShowDataError(string error)
    {
        _dataReady = false;
        DataErrorLabel.Text = error;
        DataErrorPanel.Visibility = Visibility.Visible;
        NewTaskButton.IsEnabled = false;
        TaskList.IsEnabled = false;
    }
    public static string? DataError(Exception error) => error switch
    {
        TaskValidationException or TaskConflictException or StoreVersionException => error.Message,
        SqliteException => "任务数据无法读取或保存，操作未确认成功。原文件已保留，请检查文件权限、占用或数据完整性后重新读取。",
        IOException or UnauthorizedAccessException => "本地文件无法读写，原文件已保留。请检查文件夹权限和磁盘空间后重试。",
        FormatException or InvalidCastException or IndexOutOfRangeException or ArgumentOutOfRangeException => "任务数据格式异常，原文件已保留。请检查备份或升级程序后再试。",
        _ => null
    };
}
