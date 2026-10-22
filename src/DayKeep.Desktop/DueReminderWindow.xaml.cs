using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DayKeep.Core;
using DayKeep.Models;

namespace DayKeep;

public enum ReminderAction { OpenLink, CheckedWaiting, MarkReady, Complete }
public partial class DueReminderWindow : Window
{
    private readonly Action<TaskItem, ReminderAction> _action;
    private readonly Action _showTasks;
    public int TaskCount => DueTaskList.Items.Count;
    public DueReminderWindow(Action<TaskItem, ReminderAction> action, Action showTasks)
    {
        _action = action; _showTasks = showTasks;
        InitializeComponent();
    }
    public void SetTasks(IReadOnlyList<TaskItem> tasks)
    {
        DueTaskList.ItemsSource = tasks.Select(t => new TaskCard(t)).ToList();
        CountLabel.Text = $"{tasks.Count} 件事项 · 需要继续跟进";
    }
    public void ShowError(string message) { ErrorLabel.Text = message; ErrorLabel.Visibility = Visibility.Visible; }
    public void ClearError() => ErrorLabel.Visibility = Visibility.Collapsed;
    private void OnLoaded(object sender, RoutedEventArgs e) => Height = Math.Min(Height,SystemParameters.WorkArea.Height);
    private void OpenLinkClick(object sender, RoutedEventArgs e) => _action((TaskItem)((Button)sender).Tag, ReminderAction.OpenLink);
    private void CheckClick(object sender, RoutedEventArgs e) => _action((TaskItem)((Button)sender).Tag, ReminderAction.CheckedWaiting);
    private void ReadyClick(object sender, RoutedEventArgs e) => _action((TaskItem)((Button)sender).Tag, ReminderAction.MarkReady);
    private void CompleteClick(object sender, RoutedEventArgs e) => _action((TaskItem)((Button)sender).Tag, ReminderAction.Complete);
    private void Dismiss(object sender, RoutedEventArgs e) => Close();
    private void ShowTasks(object sender, RoutedEventArgs e) => _showTasks();
    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }
    private void DragHeader(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is TextBlock && e.LeftButton == MouseButtonState.Pressed) DragMove(); }
}
