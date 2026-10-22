using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DayKeep.Core;

namespace DayKeep;

public partial class TaskEditorWindow : Window
{
    private readonly Func<TaskDraft, string?> _save;
    public TaskEditorWindow(TaskItem? item, Func<TaskDraft, string?> save)
    {
        _save = save;
        InitializeComponent();
        if (item is null) return;
        Heading.Text = "把这件事，再理清一点。";
        TitleInput.Text = item.Title;
        KindInput.SelectedIndex = (int)item.Kind;
        UrlInput.Text = item.Url;
        NotesInput.Text = item.Notes;
        TimesInput.Text = item.Reminder.Times;
        DueInput.Text = item.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
        foreach (CheckBox checkbox in WeekdayPanel.Children) checkbox.IsChecked = (item.Reminder.Weekdays & int.Parse((string)checkbox.Tag, CultureInfo.InvariantCulture)) != 0;
    }
    private void OnLoaded(object sender, RoutedEventArgs e) { Height = Math.Min(Height, SystemParameters.WorkArea.Height); TitleInput.Focus(); }
    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var days = WeekdayPanel.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Sum(c => int.Parse((string)c.Tag, CultureInfo.InvariantCulture));
        var error = _save(new(TitleInput.Text, (TaskKind)KindInput.SelectedIndex, UrlInput.Text, NotesInput.Text, TimesInput.Text, days, DueInput.Text));
        if (error is null) { Close(); return; }
        ErrorLabel.Text = error;
        ErrorLabel.Visibility = Visibility.Visible;
    }
    private void CancelClick(object sender, RoutedEventArgs e) => Close();
    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }
    private void DragHeader(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is TextBlock && e.LeftButton == MouseButtonState.Pressed) DragMove(); }
}
