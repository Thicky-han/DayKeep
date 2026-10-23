using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using DayKeep.Core;
using DayKeep.Localization;

namespace DayKeep;

public partial class TaskEditorWindow : Window
{
    private readonly Func<TaskDraft, string?> _save;
    private readonly bool _editing;
    private string? _error;
    public TaskEditorWindow(TaskItem? item, Func<TaskDraft, string?> save)
    {
        _save = save;
        _editing = item is not null;
        InitializeComponent();
        Localizer.Changed += LanguageChanged;
        Closed += (_, _) => Localizer.Changed -= LanguageChanged;
        RefreshLanguage();
        if (item is null) return;
        TitleInput.Text = item.Title;
        KindInput.SelectedIndex = (int)item.Kind;
        UrlInput.Text = item.Url;
        NotesInput.Text = item.Notes;
        TimesInput.Text = item.Reminder.Times;
        RepeatInput.Text = item.Reminder.RepeatMinutes.ToString(CultureInfo.InvariantCulture);
        MaxExtraInput.Text = item.Reminder.MaxExtraReminders.ToString(CultureInfo.InvariantCulture);
        DueInput.Text = item.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
        foreach (CheckBox checkbox in WeekdayPanel.Children) checkbox.IsChecked = (item.Reminder.Weekdays & int.Parse((string)checkbox.Tag, CultureInfo.InvariantCulture)) != 0;
    }
    private void LanguageChanged(object? sender, EventArgs e) => RefreshLanguage();
    private void RefreshLanguage()
    {
        Language = XmlLanguage.GetLanguage(Localizer.Culture.IetfLanguageTag);
        Heading.Text = Localizer.T(_editing ? "把这件事，再理清一点。" : "记下一件重要的事。");
        if (_error is not null) ErrorLabel.Text = Localizer.Message(_error);
    }
    private void ShowError(string error)
    {
        _error = error;
        ErrorLabel.Text = Localizer.Message(error);
        ErrorLabel.Visibility = Visibility.Visible;
    }
    private void OnLoaded(object sender, RoutedEventArgs e) { Infrastructure.WindowPlacement.WatchDpi(this); Infrastructure.WindowPlacement.EnsureVisible(this); TitleInput.Focus(); }
    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var days = WeekdayPanel.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Sum(c => int.Parse((string)c.Tag, CultureInfo.InvariantCulture));
        if (!int.TryParse(RepeatInput.Text,NumberStyles.None,CultureInfo.InvariantCulture,out var repeat)
            || !int.TryParse(MaxExtraInput.Text,NumberStyles.None,CultureInfo.InvariantCulture,out var max))
        { ShowError("追加间隔和次数请填写整数。"); return; }
        var error = _save(new(TitleInput.Text, (TaskKind)KindInput.SelectedIndex, UrlInput.Text, NotesInput.Text, TimesInput.Text, days, DueInput.Text,repeat,max));
        if (error is null) { Close(); return; }
        ShowError(error);
    }
    private void CancelClick(object sender, RoutedEventArgs e) => Close();
    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }
    private void DragHeader(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is TextBlock && e.LeftButton == MouseButtonState.Pressed) DragMove(); }
}
