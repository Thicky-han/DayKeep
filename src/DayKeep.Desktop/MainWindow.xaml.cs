using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DayKeep.Infrastructure;
using DayKeep.Models;

namespace DayKeep;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore;
    private ShellSettings _settings;
    private HotkeyService? _hotkey;
    private ReminderWindow? _preview;
    private bool _ready;
    public bool AllowClose { get; set; }
    public bool IsCompact => _settings.Compact;
    public double SurfaceOpacity => SurfaceBrush.Opacity;
    public bool GlobalHotkeyRegistered => _hotkey?.Registered == true;

    public MainWindow(SettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        _settings = settingsStore.Load();
        InitializeComponent();
        DateLabel.Text = DateTime.Today.ToString("M 月 d 日 · dddd", CultureInfo.GetCultureInfo("zh-CN"));
        TaskList.ItemsSource = PreviewTask.Examples;
        RoadmapText.Text = "L0   工程基础与半透明桌面界面  ·  当前阶段\n\nL1   任务管理、状态模型与本地数据库\n\nL2   固定时间、高亮弹窗与每日跟进\n\nL3   未完成重复、稍后提醒与截止日期\n\nL4   重启 / 休眠补提醒、去重与启动设置\n\nL5   历史记录、备份恢复与体验验收\n\nL6   图标定稿、安装包、开源发行与使用说明";
        OpacitySlider.Value = _settings.SurfaceOpacity;
        ApplyAppearance();
        _ready = true;
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
    public void HideToTray() { _preview?.Close(); Hide(); }
    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
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
    private void OnClosed(object? sender, EventArgs e) { _preview?.Close(); _hotkey?.Dispose(); }

    private void NavigateToday(object sender, RoutedEventArgs e) => Navigate(TodayPage, TodayNav);
    private void NavigateRoadmap(object sender, RoutedEventArgs e) => Navigate(RoadmapPage, RoadmapNav);
    private void NavigateSettings(object sender, RoutedEventArgs e) => Navigate(SettingsPage, SettingsNav);
    private void Navigate(FrameworkElement page, Button active)
    {
        foreach (var item in new FrameworkElement[] { TodayPage, RoadmapPage, SettingsPage }) item.Visibility = item == page ? Visibility.Visible : Visibility.Collapsed;
        foreach (var button in new[] { TodayNav, RoadmapNav, SettingsNav }) button.Background = button == active ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 235, 225)) : System.Windows.Media.Brushes.Transparent;
    }

    private void PreviewClick(object sender, RoutedEventArgs e) => ShowReminderPreview();
    private void PreviewTaskClick(object sender, RoutedEventArgs e) => OpenPreview(((Button)sender).Tag as PreviewTask ?? PreviewTask.Examples[0]);
    public void ShowReminderPreview() => OpenPreview(PreviewTask.Examples[0]);
    public ReminderWindow OpenPreview(PreviewTask task)
    {
        if (_preview is { IsVisible: true }) { _preview.Activate(); return _preview; }
        _preview = new ReminderWindow(task);
        _preview.Closed += (_, _) => _preview = null;
        _preview.Show();
        return _preview;
    }
}
