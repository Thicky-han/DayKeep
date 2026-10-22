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
                window = new MainWindow(store) { ShowActivated = false, ShowInTaskbar = false, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual };
                application.MainWindow = window;
                window.Show();
                window.UpdateLayout();
                Check("WPF main window loads with three explicitly marked examples", () => Assert(window.IsLoaded && ((ItemsControl)window.FindName("TaskList")).Items.Count == 3, "Main window or examples missing"));
                Check("Only the surface is translucent; text and content remain opaque", () => Assert(window.AllowsTransparency && window.Background == Brushes.Transparent && window.Opacity == 1 && Math.Abs(window.SurfaceOpacity - 0.88) < 0.001, "Transparency contract failed"));
                Check("L0 does not expose new task as a working feature", () => Assert(Descendants<Button>(window).Any(b => b.Content?.ToString()?.Contains("L1") == true && !b.IsEnabled), "Unimplemented task action must be disabled"));
                Check("Primary action text inherits white foreground", () =>
                {
                    var primary = Descendants<Button>(window).First(b => b.Content?.ToString() == "预览提醒");
                    var text = Descendants<TextBlock>(primary).First();
                    Assert(text.Foreground is SolidColorBrush brush && brush.Color == Colors.White, "Primary button label must be white");
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
                results.Add(new { name = "Global hotkey registration observation", registered = window.GlobalHotkeyRegistered });
            }
            catch (Exception error) { failures++; results.Add(new { name = "Smoke runner", passed = false, error = error.ToString() }); }
            finally
            {
                if (window is not null) { window.AllowClose = true; window.Close(); }
                File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(new { stage = "L0", failures, checks = results, scope = "Real WPF layout and controlled shell interactions; no real-day scheduling or human tray/keyboard acceptance" }, new JsonSerializerOptions { WriteIndented = true }));
                application.Shutdown(failures == 0 ? 0 : 1);
            }
        }));
    }

    private static void Invoke(Button button)
    {
        var peer = new ButtonAutomationPeer(button);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
        // Automation schedules its click at Input priority. Drain it before checking.
        button.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T item) yield return item;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void Render(Window window, string path)
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
