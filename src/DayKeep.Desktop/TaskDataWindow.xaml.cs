using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using DayKeep.Core;
using DayKeep.Infrastructure;
using DayKeep.Localization;
using Microsoft.Win32;

namespace DayKeep;

public interface IDataFilePicker
{
    string? Export(Window owner);
    string? Import(Window owner);
}

internal sealed class DataFilePicker : IDataFilePicker
{
    private const string Filter="日守备份 (*.daykeep.json)|*.daykeep.json|JSON 文件 (*.json)|*.json";
    public string? Export(Window owner)
    {
        var dialog=new SaveFileDialog {Title=Localizer.T("导出日守完整备份"),Filter=Localizer.T(Filter),FileName="DayKeep-"+DateTime.Now.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)+".daykeep.json",DefaultExt=".daykeep.json",AddExtension=true};
        return dialog.ShowDialog(owner)==true ? dialog.FileName : null;
    }
    public string? Import(Window owner)
    {
        var dialog=new OpenFileDialog {Title=Localizer.T("选择日守备份"),Filter=Localizer.T(Filter),CheckFileExists=true};
        return dialog.ShowDialog(owner)==true ? dialog.FileName : null;
    }
}

public partial class TaskDataWindow : Window
{
    private readonly TaskService _tasks;
    private readonly TimeZoneInfo _zone;
    private readonly IDataFilePicker _picker;
    private readonly Func<bool> _editorOpen;
    private readonly Action _changed;
    private ImportPreview? _preview;
    private string? _sourcePath;
    private IReadOnlyList<TaskItem> _historyTasks=[];
    private HistoryResult? _history;
    private Func<string>? _statusText;
    private bool _statusFailed;
    private bool _ready;
    private ImportMode Mode=>ModeInput.SelectedIndex==1 ? ImportMode.Replace : ImportMode.Merge;
    private sealed record TaskChoice(Guid? Id,string Label)
    {
        public override string ToString()=>Label;
    }

    public TaskDataWindow(TaskService tasks,TimeZoneInfo zone,Func<bool> editorOpen,Action changed,IDataFilePicker? picker=null)
    {
        _tasks=tasks;_zone=zone;_editorOpen=editorOpen;_changed=changed;_picker=picker ?? new DataFilePicker();
        InitializeComponent();
        Language=XmlLanguage.GetLanguage(Localizer.Culture.IetfLanguageTag);
        Localizer.Changed+=LanguageChanged;
        Closed+=(_,_)=>Localizer.Changed-=LanguageChanged;
        _ready=true;ReloadHistoryTasks();Query();
    }
    private void LanguageChanged(object? sender,EventArgs e)
    {
        // Translate the existing view without rereading the reviewed backup or refreshing its target hash.
        var fromInvalid=FromInput.SelectedDate is null ? FromInput.Text : null;
        var toInvalid=ToInput.SelectedDate is null ? ToInput.Text : null;
        Language=XmlLanguage.GetLanguage(Localizer.Culture.IetfLanguageTag);
        if(fromInvalid is not null) FromInput.Text=fromInvalid;
        if(toInvalid is not null) ToInput.Text=toInvalid;
        RenderHistoryTasks();RenderHistory();RenderPreview();
        ConfirmInput.IsChecked=false;UpdateEnabled();RefreshStatus();
    }
    private void OnLoaded(object sender,RoutedEventArgs e) {WindowPlacement.EnsureVisible(this);WindowPlacement.WatchDpi(this);}
    private void DragHeader(object sender,MouseButtonEventArgs e)
    {
        if(e.OriginalSource is DependencyObject source)
        {
            while(source is not null) {if(source is Button) return;source=VisualTreeHelper.GetParent(source);}
        }
        if(e.LeftButton==MouseButtonState.Pressed) DragMove();
    }
    private void CloseClick(object sender,RoutedEventArgs e)=>Close();
    private void OnKeyDown(object sender,KeyEventArgs e) {if(e.Key==Key.Escape) {Close();e.Handled=true;}}
    private void Status(string text,bool failed=false)=>Status(()=>Localizer.Message(text),failed);
    private void Status(Func<string> text,bool failed=false)
    {
        _statusText=text;_statusFailed=failed;RefreshStatus();
    }
    private void RefreshStatus()
    {
        if(_statusText is not null) StatusLabel.Text=_statusText();
        StatusLabel.Foreground=(Brush)new BrushConverter().ConvertFromString(_statusFailed ? "#984939" : "#607573")!;
    }
    private void Run(Action action)
    {
        try {action();}
        catch(Exception error) when(MainWindow.DataError(error) is not null) {Status(MainWindow.DataError(error)!,true);}
    }
    private void HistoryClick(object sender,RoutedEventArgs e) {ShowPage(false);ReloadHistoryTasks();Query();}
    private void BackupClick(object sender,RoutedEventArgs e)=>ShowPage(true);
    private void ShowPage(bool backup)
    {
        HistoryPage.Visibility=backup ? Visibility.Collapsed : Visibility.Visible;BackupPage.Visibility=backup ? Visibility.Visible : Visibility.Collapsed;
        HistoryTab.Background=backup ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString("#DCE9DE")!;
        BackupTab.Background=backup ? (Brush)new BrushConverter().ConvertFromString("#DCE9DE")! : Brushes.Transparent;
    }
    private void ReloadHistoryTasks()=>Run(()=>
    {
        _historyTasks=_tasks.LoadAll();RenderHistoryTasks();
    });
    private void RenderHistoryTasks()
    {
        var selected=(TaskInput.SelectedItem as TaskChoice)?.Id;
        var choices=new List<TaskChoice> {new(null,Localizer.T("全部事项"))};
        choices.AddRange(_historyTasks.OrderBy(t=>t.Title).Select(t=>new TaskChoice(t.Id,t.Title+(t.ArchivedAt is not null ? Localizer.T(" · 已归档") : t.State==TaskState.Completed ? Localizer.T(" · 已完成") : ""))));
        TaskInput.ItemsSource=choices;TaskInput.SelectedItem=choices.FirstOrDefault(c=>c.Id==selected) ?? choices[0];
    }
    private void QueryClick(object sender,RoutedEventArgs e)=>Query();
    private void ResetClick(object sender,RoutedEventArgs e) {SearchInput.Clear();FromInput.SelectedDate=null;ToInput.SelectedDate=null;TaskInput.SelectedIndex=0;Query();}
    private void Query()=>Run(()=>
    {
        if((!string.IsNullOrWhiteSpace(FromInput.Text) && FromInput.SelectedDate is null) || (!string.IsNullOrWhiteSpace(ToInput.Text) && ToInput.SelectedDate is null))
            throw new TaskValidationException("请选择有效的开始和结束日期，或清空日期。");
        _history=_tasks.History(new(SearchInput.Text,(TaskInput.SelectedItem as TaskChoice)?.Id,
            FromInput.SelectedDate is {} from ? DateOnly.FromDateTime(from) : null,ToInput.SelectedDate is {} to ? DateOnly.FromDateTime(to) : null,
            Localizer.Catalog(Localizer.LanguageCode)),_zone);
        RenderHistory();
        Status("检查记录与完成记录分别保留；名称显示事项当前名称。");
    });
    private void RenderHistory()
    {
        if(_history is not {} result) return;
        HistoryList.ItemsSource=result.Entries.Select(e=>new
        {
            e.Title,Action=Localizer.T(e.Action),e.Notes,
            Time=Localizer.T("日期 {0} · 记录于 {1}",e.LocalDate.ToString("d",Localizer.Culture),TimeZoneInfo.ConvertTime(e.RecordedAt,_zone).ToString("G",Localizer.Culture))
        }).ToList();
        HistoryEmpty.Visibility=result.Entries.Count==0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryCount.Text=result.HasMore ? Localizer.T("显示最近 200 条匹配记录；请缩小日期或事项范围查看更早记录。") : Localizer.T("共 {0} 条匹配记录 · 日期包含起止当天",result.Entries.Count);
    }
    private void ExportClick(object sender,RoutedEventArgs e)=>Run(()=>
    {
        var path=_picker.Export(this);if(path is null) return;
        if(File.Exists(path)) throw new TaskValidationException("目标文件已存在，请选择新的文件名；现有文件不会覆盖。");
        _tasks.ExportBackup(path);Status(()=>Localizer.T("完整备份已导出：{0}",path));
    });
    private void ChooseClick(object sender,RoutedEventArgs e)=>Run(()=>
    {
        var path=_picker.Import(this);if(path is null) return;
        _sourcePath=path;FilePathLabel.Text=path;RefreshPreviewButton.IsEnabled=true;Preview();
    });
    private void RefreshPreviewClick(object sender,RoutedEventArgs e)=>Run(Preview);
    private void Preview()
    {
        _preview=null;ConfirmInput.IsChecked=false;ImportButton.IsEnabled=false;PreviewPanel.Visibility=Visibility.Collapsed;
        if(_sourcePath is null) return;
        var preview=_tasks.PreviewImport(_sourcePath);_preview=preview;ModeInput.SelectedIndex=0;
        RenderPreview();PreviewPanel.Visibility=Visibility.Visible;Status("预览完成；确认导入前不会修改任务。");
        UpdateLayout();PreviewPanel.BringIntoView();
    }
    private void ModeChanged(object sender,SelectionChangedEventArgs e) {if(!_ready) return;ConfirmInput.IsChecked=false;UpdatePlan();}
    private void ConfirmChanged(object sender,RoutedEventArgs e) {if(_ready) UpdateEnabled();}
    private void RenderPreview()
    {
        if(_preview is not {} preview) return;
        PreviewLabel.Text=Localizer.T("导出于 {0}\n备份含 {1} 件事项、{2} 条检查；本机 {3} 件事项。",TimeZoneInfo.ConvertTime(preview.ExportedAt,_zone).ToString("G",Localizer.Culture),preview.IncomingTasks,preview.IncomingChecks,preview.CurrentTasks);
        UpdatePlan();
    }
    private void UpdatePlan()
    {
        if(_preview is null) return;
        PlanLabel.Text=(Mode==ImportMode.Merge
            ? Localizer.T("合并将新增 {0} 件事项；同 ID 的 {1} 件保留本机任务和记录。现有 {2} 件均保留。",_preview.NewTasks,_preview.KeptIds,_preview.CurrentTasks)
            : Localizer.T("替换后为 {0} 件事项。将移除本机备份中没有的 {1} 件，并恢复备份内的状态和记录。",_preview.IncomingTasks,_preview.RemovedByReplace))
            +(Mode==ImportMode.Merge && _preview.MergeError is {} error ? "\n"+Localizer.T("此文件无法合并：{0}",Localizer.Message(error)) : "");UpdateEnabled();
    }
    private void UpdateEnabled()=>ImportButton.IsEnabled=_preview is not null && ConfirmInput.IsChecked==true && (Mode!=ImportMode.Merge || _preview.MergeError is null);
    private void ImportClick(object sender,RoutedEventArgs e)
    {
        if(!ImportButton.IsEnabled || _preview is null) return;
        Run(()=>
        {
            if(_editorOpen()) {Status("任务表单仍然打开。请先保存或关闭表单，再确认导入；草稿已保留。",true);return;}
            var preview=_preview;_preview=null;ConfirmInput.IsChecked=false;ImportButton.IsEnabled=false;PreviewPanel.Visibility=Visibility.Collapsed;
            try
            {
                var result=_tasks.ImportBackup(preview,Mode);
                RecoveryPath.Text=result.RecoveryPath;RecoveryPanel.Visibility=Visibility.Visible;PreviewPanel.Visibility=Visibility.Collapsed;
                _changed();ReloadHistoryTasks();Status(()=>Localizer.T("导入已完成，恢复 {0} 件事项。导入前的数据已另存为恢复备份。",result.ImportedTasks));
            }
            catch(Exception error) when(MainWindow.DataError(error) is not null)
            {
                var message=MainWindow.DataError(error)!;
                Status(()=>Localizer.T("{0} 请重新预览后再确认。",Localizer.Message(message)),true);
            }
        });
    }
}
