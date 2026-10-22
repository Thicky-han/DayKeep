using System.Windows;
using System.Windows.Input;
using DayKeep.Models;

namespace DayKeep;

public partial class ReminderWindow : Window
{
    public ReminderWindow(PreviewTask task)
    {
        InitializeComponent();
        TaskTitle.Text = task.Title;
        TaskDescription.Text = task.Description;
    }
    private void Dismiss(object sender, RoutedEventArgs e) => Close();
    private void ReturnToMain(object sender, RoutedEventArgs e) { Close(); if (System.Windows.Application.Current.MainWindow is MainWindow main) main.ShowFromTray(); }
    private void DragHeader(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is System.Windows.Controls.TextBlock && e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }
}
