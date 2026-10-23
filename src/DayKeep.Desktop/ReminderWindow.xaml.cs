using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using DayKeep.Models;
using DayKeep.Localization;

namespace DayKeep;

public partial class ReminderWindow : Window
{
    private readonly PreviewTask _task;
    public ReminderWindow(PreviewTask task)
    {
        _task = task;
        InitializeComponent();
        RefreshLanguage(null,EventArgs.Empty);
        Localizer.Changed += RefreshLanguage;
        Closed += (_,_) => Localizer.Changed -= RefreshLanguage;
    }
    private void RefreshLanguage(object? sender,EventArgs e) { Language=XmlLanguage.GetLanguage(Localizer.Culture.IetfLanguageTag); TaskTitle.Text = _task.Title; TaskDescription.Text = _task.Description; }
    private void Dismiss(object sender, RoutedEventArgs e) => Close();
    private void ReturnToMain(object sender, RoutedEventArgs e) { Close(); if (System.Windows.Application.Current.MainWindow is MainWindow main) main.ShowFromTray(); }
    private void DragHeader(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is System.Windows.Controls.TextBlock && e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }
}
