using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DayKeep.Models;
using DayKeep.Core;

namespace DayKeep.Infrastructure;

internal static class SmokeRunner
{
    public static void Run(App application, string directory)
    {
        Directory.CreateDirectory(directory);
        var results = new List<object>();
        var failures = 0;
        MainWindow? window = null;
        void Check(string name, Action assertion)
        {
            try { assertion(); results.Add(new { name, passed = true }); }
            catch (Exception error) { failures++; results.Add(new { name, passed = false, error = error.Message }); }
        }
        static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        application.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            try
            {
                var store = new SettingsStore(Path.Combine(directory, "ui-settings.json"));
                var service = new TaskService(new TaskRepository(Path.Combine(directory, "tasks.db")));
                window = new MainWindow(store, service) { ShowActivated = false, ShowInTaskbar = false, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual };
                application.MainWindow = window;
                window.Show();
                window.UpdateLayout();
                Check("Fresh main window uses an empty real database without seeded examples", () => Assert(window.IsLoaded && ((ItemsControl)window.FindName("TaskList")).Items.Count == 0 && ((FrameworkElement)window.FindName("EmptyPanel")).Visibility == Visibility.Visible, "Fresh task list should be empty"));
                Check("Only the surface is translucent; text and content remain opaque", () => Assert(window.AllowsTransparency && window.Background == Brushes.Transparent && window.Opacity == 1 && Math.Abs(window.SurfaceOpacity - 0.88) < 0.001, "Transparency contract failed"));
                Check("L1 new task action is enabled after database initialization", () => Assert(((Button)window.FindName("NewTaskButton")).IsEnabled, "New task should be available"));
                Check("Primary action text inherits white foreground", () =>
                {
                    var primary = (Button)window.FindName("NewTaskButton");
                    var text = Descendants<TextBlock>(primary).First();
                    Assert(text.Foreground is SolidColorBrush brush && brush.Color == Colors.White, "Primary button label must be white");
                });
                Render(window, Path.Combine(directory, "daykeep-empty.png"));
                Check("Styled filter opens through automation and applies selected filter", () =>
                {
                    var combo = (ComboBox)window.FindName("FilterInput");
                    var toggle = Descendants<System.Windows.Controls.Primitives.ToggleButton>(combo).Single();
                    ((IToggleProvider)new ToggleButtonAutomationPeer(toggle).GetPattern(PatternInterface.Toggle)).Toggle();
                    combo.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    Assert(combo.IsDropDownOpen, "Dropdown did not open");
                    combo.SelectedIndex = 3;
                    Assert(combo.SelectedIndex == 3, "Dropdown selection failed");
                    combo.IsDropDownOpen = false; combo.SelectedIndex = 0;
                });
                Check("New-task button opens the real editor", () => { Invoke((Button)window.FindName("NewTaskButton")); Assert(window.Editor is { IsVisible: true }, "Editor did not open"); });
                var editor = window.Editor!;
                Render(editor, Path.Combine(directory, "daykeep-editor.png"));
                Check("Invalid form stays open and never creates a phantom task", () =>
                {
                    Invoke((Button)editor.FindName("SaveButton"));
                    Assert(editor.IsVisible && ((FrameworkElement)editor.FindName("ErrorLabel")).Visibility == Visibility.Visible && service.LoadAll().Count == 0, "Invalid task must not be stored");
                });
                ((TextBox)editor.FindName("TitleInput")).Text = "查看专利缴费入口（测试）";
                ((TextBox)editor.FindName("UrlInput")).Text = "https://example.org/patent";
                ((TextBox)editor.FindName("NotesInput")).Text = "合成界面验证，不是真实缴费事项。";
                ((TextBox)editor.FindName("TimesInput")).Text = "14:30, 09:00";
                Check("L3 non-integer repeat settings stay in editor without saving",()=>
                {
                    ((TextBox)editor.FindName("RepeatInput")).Text="abc"; Invoke((Button)editor.FindName("SaveButton"));
                    Assert(editor.IsVisible && service.LoadAll().Count==0,"Invalid repeat setting saved");
                    ((TextBox)editor.FindName("RepeatInput")).Text="15"; ((TextBox)editor.FindName("MaxExtraInput")).Text="2";
                });
                Check("Saving through the editor updates real database and task list", () =>
                {
                    Invoke((Button)editor.FindName("SaveButton"));
                    Assert(window.Editor is null && service.LoadAll().Single().State == TaskState.Waiting && ((ItemsControl)window.FindName("TaskList")).Items.Count == 1, "Save action failed");
                });
                Button TaskAction(string label)
                {
                    window.UpdateLayout();
                    return Descendants<Button>(window).First(b => b.Content?.ToString() == label && b.Tag is TaskItem && b.Visibility == Visibility.Visible);
                }
                Check("Edit button opens persisted fields and saves changes", () =>
                {
                    Invoke(TaskAction("编辑"));
                    var editing = window.Editor!;
                    Assert(((TextBox)editing.FindName("UrlInput")).Text == "https://example.org/patent" && ((TextBox)editing.FindName("TimesInput")).Text == "09:00, 14:30" && ((TextBox)editing.FindName("RepeatInput")).Text == "15" && ((TextBox)editing.FindName("MaxExtraInput")).Text == "2", "Existing fields not populated");
                    ((TextBox)editing.FindName("DueInput")).Text = "2026-12-01";
                    Invoke((Button)editing.FindName("SaveButton"));
                    Assert(service.LoadAll().Single().DueDate == new DateOnly(2026,12,1), "Edit not saved");
                });
                Check("Pause and resume card actions persist", () => { Invoke(TaskAction("暂停")); Assert(service.LoadAll().Single().Paused, "Pause failed"); Invoke(TaskAction("恢复跟进")); Assert(!service.LoadAll().Single().Paused, "Resume failed"); });
                Check("Completion leaves active list and appears under completed filter", () =>
                {
                    Invoke(TaskAction("完成")); Assert(((ItemsControl)window.FindName("TaskList")).Items.Count == 0 && service.LoadAll().Single().State == TaskState.Completed, "Completion failed");
                    ((ComboBox)window.FindName("FilterInput")).SelectedIndex = 1;
                    window.UpdateLayout(); Assert(((ItemsControl)window.FindName("TaskList")).Items.Count == 1, "Completed filter failed");
                });
                Check("Undo completion restores waiting state without deleting the task", () => { Invoke(TaskAction("撤销完成")); Assert(service.LoadAll().Single().State == TaskState.Waiting, "Undo failed"); ((ComboBox)window.FindName("FilterInput")).SelectedIndex = 0; });
                Check("Archive and restore card actions preserve task data", () =>
                {
                    Invoke(TaskAction("归档")); Assert(service.LoadAll().Single().ArchivedAt is not null, "Archive failed");
                    ((ComboBox)window.FindName("FilterInput")).SelectedIndex = 2;
                    Invoke(TaskAction("恢复归档")); Assert(service.LoadAll().Single().ArchivedAt is null && service.LoadAll().Single().DueDate is not null, "Restore failed");
                    ((ComboBox)window.FindName("FilterInput")).SelectedIndex = 0;
                });
                Check("Concurrent task change rejects stale form without silently overwriting", () =>
                {
                    Invoke(TaskAction("编辑")); var editing = window.Editor!;
                    service.TogglePause(service.LoadAll().Single());
                    ((TextBox)editing.FindName("TitleInput")).Text = "不能覆盖较新状态";
                    Invoke((Button)editing.FindName("SaveButton"));
                    Assert(editing.IsVisible && ((TextBlock)editing.FindName("ErrorLabel")).Text.Contains("发生变化") && service.LoadAll().Single().Title != "不能覆盖较新状态", "Stale form overwrote data");
                    editing.Close(); window.ReloadTasks(); Invoke(TaskAction("恢复跟进"));
                });
                Check("Second window reloads task from SQLite instead of examples", () =>
                {
                    var reopened = new MainWindow(new SettingsStore(Path.Combine(directory,"reload-ui.json")), new TaskService(new TaskRepository(service.Repository.DatabasePath))) { AllowClose = true };
                    Assert(reopened.Tasks.LoadAll().Single() == service.LoadAll().Single(), "Database reload mismatch"); reopened.Close();
                });
                Render(window, Path.Combine(directory, "daykeep-desktop.png"));
                Check("Roadmap navigation is connected", () =>
                {
                    Invoke((Button)window.FindName("RoadmapNav"));
                    Assert(((FrameworkElement)window.FindName("RoadmapPage")).Visibility == Visibility.Visible && ((FrameworkElement)window.FindName("TodayPage")).Visibility == Visibility.Collapsed, "Roadmap navigation failed");
                });
                Render(window, Path.Combine(directory, "daykeep-roadmap.png"));
                Check("Appearance page navigation is connected", () => { Invoke((Button)window.FindName("SettingsNav")); Assert(((FrameworkElement)window.FindName("SettingsPage")).Visibility == Visibility.Visible, "Settings navigation failed"); });
                Check("Opacity slider changes surface but preserves content opacity", () => { window.SetSurfaceOpacity(0.74); Assert(Math.Abs(window.SurfaceOpacity - 0.74) < 0.001 && window.Opacity == 1, "Slider did not update the surface" ); });
                Check("Saved appearance survives reload from disk", () => Assert(Math.Abs(new SettingsStore(store.FilePath).Load().SurfaceOpacity - 0.74) < 0.001, "Saved setting was not restored"));
                window.SetSurfaceOpacity(0.88);
                Render(window, Path.Combine(directory, "daykeep-settings.png"));
                Check("Pin action toggles topmost", () => { var before = window.Topmost; Invoke((Button)window.FindName("PinButton")); Assert(window.Topmost != before, "Pin action failed"); Invoke((Button)window.FindName("PinButton")); });
                Invoke((Button)window.FindName("TodayNav"));
                Check("Compact mode provides a 450 DIP desktop window", () => { Invoke((Button)window.FindName("CompactButton")); Assert(window.Width == 450 && window.IsCompact, "Compact mode failed"); });
                Render(window, Path.Combine(directory, "daykeep-compact.png"));
                Check("Expanded mode restores the full navigation", () => { Invoke((Button)window.FindName("CompactButton")); Assert(window.Width == 1060 && !window.IsCompact, "Expand failed"); });
                ReminderWindow? preview = null;
                Check("Reminder preview is topmost without requesting focus", () => { preview = window.OpenPreview(PreviewTask.Examples[0]); Assert(preview.IsVisible && preview.Topmost && !preview.ShowActivated, "Preview window behavior failed"); });
                if (preview is not null)
                {
                    Render(preview, Path.Combine(directory, "daykeep-reminder.png"));
                    Check("Repeated preview calls reuse the existing reminder", () => Assert(ReferenceEquals(preview, window.OpenPreview(PreviewTask.Examples[0])), "Duplicate reminder windows"));
                    Check("Preview dismiss button closes its window", () => { Invoke(Descendants<Button>(preview).First(b => b.Content?.ToString() == "了解了，收起预览")); Assert(!preview.IsVisible, "Dismiss action failed"); });
                }
                Check("Closing main window hides it without ending the application", () => { window.Close(); Assert(!window.IsVisible && !window.AllowClose, "Close should hide to tray"); });
                Check("Tray show callback restores the same window", () => { window.ShowFromTray(); Assert(window.IsVisible, "Show callback failed"); });
                Check("Global toggle hides and reopens the same window", () => { window.ToggleVisibility(); Assert(!window.IsVisible, "Toggle did not hide"); window.ToggleVisibility(); Assert(window.IsVisible, "Toggle did not show"); });
                Check("NotifyIcon can be created and disposed", () => { using var tray = new TrayService(window.ShowFromTray, window.HideToTray, window.ShowReminderPreview, () => { }); });
                Check("Invalid settings fall back safely without overwriting the original", () =>
                {
                    var path = Path.Combine(directory, "corrupted-settings.json");
                    File.WriteAllText(path, "invalid-json");
                    var damaged = new SettingsStore(path);
                    Assert(damaged.Load() == new ShellSettings() && damaged.LastError is not null && File.ReadAllText(path) == "invalid-json", "Corrupt settings handling failed");
                });
                Check("Out-of-range opacity from disk is clamped", () => { var path = Path.Combine(directory, "range-settings.json"); File.WriteAllText(path, "{\"SurfaceOpacity\":0.1}"); Assert(new SettingsStore(path).Load().SurfaceOpacity == 0.65, "Opacity clamp failed"); });
                Check("Corrupt database displays an error and disables writes", () =>
                {
                    var path = Path.Combine(directory,"corrupt-tasks.db"); File.WriteAllText(path,"invalid-sqlite-keep-original");
                    var failed = new MainWindow(new SettingsStore(Path.Combine(directory,"error-ui.json")),new TaskService(new TaskRepository(path))) { AllowClose = true };
                    Assert(((FrameworkElement)failed.FindName("DataErrorPanel")).Visibility == Visibility.Visible && !((Button)failed.FindName("NewTaskButton")).IsEnabled && File.ReadAllText(path) == "invalid-sqlite-keep-original", "Read failure hidden or original modified"); failed.Close();
                });
                Check("Failed SQLite save keeps the editor open with a visible error", () =>
                {
                    var path = Path.Combine(directory,"readonly-tasks.db"); new TaskRepository(path).Initialize();
                    var failed = new MainWindow(new SettingsStore(Path.Combine(directory,"readonly-ui.json")), new TaskService(new TaskRepository(path,true))) { AllowClose = true, ShowInTaskbar = false, ShowActivated = false };
                    failed.Show(); var form = failed.OpenEditor(null)!;
                    ((TextBox)form.FindName("TitleInput")).Text = "保存失败测试";
                    Invoke((Button)form.FindName("SaveButton"));
                    Assert(form.IsVisible && ((FrameworkElement)form.FindName("ErrorLabel")).Visibility == Visibility.Visible && failed.Tasks.LoadAll().Count == 0, "Failed write appeared successful"); failed.Close();
                });
                ReminderUiChecks.Run(directory,Check);
                RepeatUiChecks.Run(directory,Check);
                RecoveryUiChecks.Run(directory,Check);
                results.Add(new { name = "Global hotkey registration observation", registered = window.GlobalHotkeyRegistered });
            }
            catch (Exception error) { failures++; results.Add(new { name = "Smoke runner", passed = false, error = error.ToString() }); }
            finally
            {
                if (window is not null) { window.AllowClose = true; window.Close(); }
                File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(new { stage = "L4", failures, checks = results, scope = "Real WPF task/reminder actions and SQLite with controlled clock; actual clock measured separately; no human tray/keyboard acceptance" }, new JsonSerializerOptions { WriteIndented = true }));
                application.Shutdown(failures == 0 ? 0 : 1);
            }
        }));
    }

    internal static void Invoke(Button button)
    {
        var peer = new ButtonAutomationPeer(button);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
        // Automation schedules its click at Input priority. Drain it before checking.
        button.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    internal static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T item) yield return item;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    internal static void Render(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path);
        encoder.Save(output);
    }
}
