using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Markup;
using DayKeep.Core;
using DayKeep.Models;
using DayKeep.Localization;

namespace DayKeep;

public enum ReminderAction { OpenLink, CheckedWaiting, MarkReady, Complete, Snooze10, Snooze30, Snooze60 }
public partial class DueReminderWindow : Window
{
    private readonly Action<TaskItem, ReminderAction> _action;
    private readonly Action _showTasks;
    private readonly Func<bool> _dismiss;
    private bool _silent;
    private Func<string>? _error;
    private IReadOnlyList<TaskItem> _displayTasks = [];
    private DateOnly? _displayDate;
    private IReadOnlyDictionary<Guid,string>? _displayTiming;
    public int TaskCount => DueTaskList.Items.Count;
    public DueReminderWindow(Action<TaskItem, ReminderAction> action, Action showTasks, Func<bool>? dismiss = null)
    {
        _action = action; _showTasks = showTasks; _dismiss=dismiss ?? (()=>true);
        InitializeComponent();
        Language=XmlLanguage.GetLanguage(Localizer.Culture.IetfLanguageTag);
        Localizer.Changed += RefreshLanguage;
        Closed += (_,_) => Localizer.Changed -= RefreshLanguage;
    }
    public void SetTasks(IReadOnlyList<TaskItem> tasks, DateOnly? today = null, IReadOnlyDictionary<Guid,string>? timing = null)
    {
        var date = today ?? DateOnly.FromDateTime(DateTime.Now);
        _displayTasks=tasks; _displayDate=date; _displayTiming=timing;
        DueTaskList.ItemsSource = tasks.OrderBy(t=>ReminderPlanner.DeadlinePriority(t,date)).ThenBy(t=>t.DueDate).ThenBy(t=>t.CreatedAt).Select(t => new TaskCard(t,Today:date,ReminderDetail:timing?.GetValueOrDefault(t.Id) ?? "")).ToList();
        CountLabel.Text = Localizer.T("{0} 件事项 · 需要继续跟进",tasks.Count);
    }
    public void ShowError(string message, bool preserveExisting = false) => ShowError(()=>Localizer.T(message),preserveExisting);
    public void ShowError(Func<string> message, bool preserveExisting = false) { if (preserveExisting && ErrorLabel.Visibility == Visibility.Visible) return; _error=message; ErrorLabel.Text = message(); ErrorLabel.Visibility = Visibility.Visible; }
    public void ClearError() { _error=null; ErrorLabel.Visibility = Visibility.Collapsed; }
    private void RefreshLanguage(object? sender,EventArgs e)
    {
        Language=XmlLanguage.GetLanguage(Localizer.Culture.IetfLanguageTag);
        SetTasks(_displayTasks,_displayDate,_displayTiming);
        if (_error is not null && ErrorLabel.Visibility == Visibility.Visible) ErrorLabel.Text = _error();
    }
    public void CloseSilently() { _silent=true; Close(); }
    protected override void OnClosing(CancelEventArgs e) { if(!_silent && !_dismiss()) e.Cancel=true; base.OnClosing(e); }
    private void OnLoaded(object sender, RoutedEventArgs e) { Infrastructure.WindowPlacement.WatchDpi(this); Infrastructure.WindowPlacement.EnsureVisible(this); }
    private void OpenLinkClick(object sender, RoutedEventArgs e) => _action((TaskItem)((Button)sender).Tag, ReminderAction.OpenLink);
    private void CheckClick(object sender, RoutedEventArgs e) => _action((TaskItem)((Button)sender).Tag, ReminderAction.CheckedWaiting);
    private void ReadyClick(object sender, RoutedEventArgs e) => _action((TaskItem)((Button)sender).Tag, ReminderAction.MarkReady);
    private void CompleteClick(object sender, RoutedEventArgs e) => _action((TaskItem)((Button)sender).Tag, ReminderAction.Complete);
    private void Snooze10Click(object sender, RoutedEventArgs e) => _action((TaskItem)((Button)sender).Tag, ReminderAction.Snooze10);
    private void Snooze30Click(object sender, RoutedEventArgs e) => _action((TaskItem)((Button)sender).Tag, ReminderAction.Snooze30);
    private void Snooze60Click(object sender, RoutedEventArgs e) => _action((TaskItem)((Button)sender).Tag, ReminderAction.Snooze60);
    private void Dismiss(object sender, RoutedEventArgs e) => Close();
    private void ShowTasks(object sender, RoutedEventArgs e) => _showTasks();
    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }
    private void DragHeader(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is TextBlock && e.LeftButton == MouseButtonState.Pressed) DragMove(); }
}
