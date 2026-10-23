using System.IO;
using System.Windows;
using System.Windows.Controls;
using DayKeep.Core;
using Microsoft.Data.Sqlite;

namespace DayKeep.Infrastructure;

internal static class DataUiChecks
{
    public static void Run(string directory,Action<string,Action> check)
    {
        var folder=Path.Combine(directory,"data-ui");Directory.CreateDirectory(folder);
        var now=new DateTimeOffset(2026,10,26,8,59,0,TimeSpan.Zero);
        var service=new TaskService(new TaskRepository(Path.Combine(folder,"tasks.db")),()=>now);service.Initialize();
        var day=ReminderPlanner.LocalDate(now,TimeZoneInfo.Local);
        var waiting=service.Create(new("专利入口（合成验收）",TaskKind.WaitingForCondition));
        waiting=service.RecordWaitingCheck(waiting,day,"尚未开放，需要明天继续检查");service.Archive(waiting);
        var completed=service.Complete(service.TogglePause(service.Create(new("缴费资料（合成验收）",TaskKind.Todo))));
        var window=new MainWindow(new SettingsStore(Path.Combine(folder,"settings.json")),service) {AllowClose=true,ShowInTaskbar=false,ShowActivated=false};
        window.Show();var picker=new Files {ExportPath=Path.Combine(folder,"ui.daykeep.json")};
        var data=window.OpenDataTools(picker)!;
        Button B(string name)=>(Button)data.FindName(name);
        TextBlock Label(string name)=>(TextBlock)data.FindName(name);
        void Click(string name)=>SmokeRunner.Invoke(B(name));
        string Hash()=>BackupFile.Hash(service.Capture());
        void SelectBackup() {picker.ImportPath=picker.ExportPath;Click("ChooseButton");}
        void Confirm()=>((CheckBox)data.FindName("ConfirmInput")).IsChecked=true;
        static void Assert(bool condition,string message="Unexpected L5 UI result") {if(!condition) throw new InvalidOperationException(message);}
        try
        {
            check("L5 history window queries real checks and task operations",()=>
            {
                Assert(((ItemsControl)data.FindName("HistoryList")).Items.Count==6);
                Assert(((ComboBox)data.FindName("TaskInput")).Items.Count==3);
                Assert(!SmokeRunner.Descendants<TextBlock>(data).Any(t=>t.Text.Contains("TaskChoice")),"Internal choice details leaked into UI");
                SmokeRunner.Render(data,Path.Combine(directory,"daykeep-history.png"));
            });
            check("L5 history search matches check notes without claiming completion",()=>
            {
                ((TextBox)data.FindName("SearchInput")).Text="明天继续";Click("QueryButton");data.UpdateLayout();
                Assert(((ItemsControl)data.FindName("HistoryList")).Items.Count==1 && SmokeRunner.Descendants<TextBlock>(data).Any(t=>t.Text=="今日已检查，尚未开放"));
            });
            check("L5 history task and inclusive date filters work for archived tasks",()=>
            {
                ((TextBox)data.FindName("SearchInput")).Clear();var combo=(ComboBox)data.FindName("TaskInput");combo.SelectedItem=combo.Items.Cast<object>().Single(t=>t.ToString()!.Contains("专利入口"));
                ((DatePicker)data.FindName("FromInput")).SelectedDate=day.ToDateTime(TimeOnly.MinValue);((DatePicker)data.FindName("ToInput")).SelectedDate=day.ToDateTime(TimeOnly.MinValue);
                Click("QueryButton");Assert(((ItemsControl)data.FindName("HistoryList")).Items.Count==3);
            });
            check("L5 inverted date query displays an error without modifying tasks",()=>
            {
                var before=Hash();((DatePicker)data.FindName("FromInput")).SelectedDate=day.AddDays(1).ToDateTime(TimeOnly.MinValue);Click("QueryButton");
                Assert(Label("StatusLabel").Text.Contains("日期") && Hash()==before);Click("ResetButton");
            });
            check("L5 export button writes a validated complete backup",()=>
            {
                Click("BackupTab");var before=Hash();Click("ExportButton");Assert(BackupFile.Read(picker.ExportPath!).Data.Tasks.Count==2 && Hash()==before && Label("StatusLabel").Text.Contains("已导出"));
            });
            check("L5 export refuses to overwrite an existing backup",()=>
            {
                var bytes=File.ReadAllBytes(picker.ExportPath!);Click("ExportButton");Assert(Label("StatusLabel").Text.Contains("已存在") && bytes.SequenceEqual(File.ReadAllBytes(picker.ExportPath!)));
            });
            service.Create(new("本机后来新增",TaskKind.Todo));
            check("L5 selecting a backup previews effects with no database mutation",()=>
            {
                var before=Hash();SelectBackup();Assert(Hash()==before && Label("PreviewLabel").Text.Contains("本机 3 件") && Label("PlanLabel").Text.Contains("保留本机") && !B("ImportButton").IsEnabled);
            });
            check("L5 confirmation enables merge and preserves existing local task groups",()=>
            {
                Confirm();Assert(B("ImportButton").IsEnabled);Click("ImportButton");Assert(service.LoadAll().Count==3 && Label("StatusLabel").Text.Contains("导入已完成"));
            });
            check("L5 switching to replacement resets confirmation and shows removal count",()=>
            {
                SelectBackup();Confirm();((ComboBox)data.FindName("ModeInput")).SelectedIndex=1;
                Assert(!B("ImportButton").IsEnabled && ((CheckBox)data.FindName("ConfirmInput")).IsChecked==false && Label("PlanLabel").Text.Contains("移除") && Label("PlanLabel").Text.Contains("1 件"));
                SmokeRunner.Render(data,Path.Combine(directory,"daykeep-backup-preview.png"));
            });
            check("L5 an open task editor blocks import and preserves its draft",()=>
            {
                var editor=window.OpenEditor(null)!;((TextBox)editor.FindName("TitleInput")).Text="未保存草稿";var before=Hash();Confirm();Click("ImportButton");
                Assert(Hash()==before && Label("StatusLabel").Text.Contains("表单") && ((TextBox)editor.FindName("TitleInput")).Text=="未保存草稿");editor.Close();
            });
            check("L5 confirmed replacement writes recovery and refreshes main task list",()=>
            {
                Click("ImportButton");Assert(service.LoadAll().Count==2 && Label("StatusLabel").Text.Contains("导入已完成"));
                var recovery=((TextBox)data.FindName("RecoveryPath")).Text;Assert(BackupFile.Read(recovery).Data.Tasks.Count==3 && ((ItemsControl)window.FindName("TaskList")).Items.Count==0);
            });
            check("L5 restored completion still undoes to original paused state",()=>
            {
                var restored=service.UndoComplete(service.LoadAll().Single(t=>t.Id==completed.Id));Assert(restored.State==TaskState.Ready && restored.Paused);window.ReloadTasks();
            });
            check("L5 target changes invalidate preview and leave newer data intact",()=>
            {
                SelectBackup();((ComboBox)data.FindName("ModeInput")).SelectedIndex=1;Confirm();service.Create(new("预览后的新事项",TaskKind.Todo));var before=Hash();Click("ImportButton");
                Assert(Hash()==before && !B("ImportButton").IsEnabled && Label("StatusLabel").Text.Contains("重新预览"));
                SmokeRunner.Render(data,Path.Combine(directory,"daykeep-backup-stale.png"));
            });
            check("L5 failed database import displays error and rolls back",()=>
            {
                Click("RefreshPreviewButton");((ComboBox)data.FindName("ModeInput")).SelectedIndex=1;Confirm();
                Sql("CREATE TRIGGER fail_import BEFORE INSERT ON tasks BEGIN SELECT RAISE(ABORT,'controlled'); END;");var before=Hash();Click("ImportButton");
                Assert(Hash()==before && Label("StatusLabel").Text.Contains("未确认成功") && !B("ImportButton").IsEnabled);Sql("DROP TRIGGER fail_import;");
            });
            check("L5 damaged backup shows an error and disables confirmation",()=>
            {
                var damaged=Path.Combine(folder,"damaged.daykeep.json");File.WriteAllText(damaged,"{bad");picker.ImportPath=damaged;var before=Hash();Click("ChooseButton");
                Assert(Hash()==before && !B("ImportButton").IsEnabled && Label("StatusLabel").Text.Contains("格式"));
            });
            check("L5 canceled file selection preserves current data",()=>
            {
                picker.ImportPath=null;picker.ExportPath=null;var before=Hash();Click("ChooseButton");Click("ExportButton");Assert(Hash()==before);
            });
            check("L5 history tool hides and reopens with the main window",()=>
            {
                window.HideToTray();Assert(!data.IsVisible);window.ShowFromTray();Assert(data.IsVisible && ReferenceEquals(data,window.OpenDataTools()));
            });
            check("L5 compact main window has a working history and backup entry",()=>
            {
                data.Close();window.SetCompact(true);SmokeRunner.Invoke((Button)window.FindName("DataToolsButton"));Assert(window.OpenDataTools() is {IsVisible:true});
            });
        }
        finally {window.Close();}
        CheckLiveRestore();
        void CheckLiveRestore()
        {
            var at=new DateTimeOffset(2026,10,26,8,59,0,TimeSpan.Zero);
            var live=new TaskService(new TaskRepository(Path.Combine(folder,"live-import.db")),()=>at);live.Initialize();
            live.Create(new("导入恢复中的提醒一",TaskKind.Todo));live.Create(new("导入恢复中的提醒二",TaskKind.Todo));
            var shell=new MainWindow(new SettingsStore(Path.Combine(folder,"live-settings.json")),live) {AllowClose=true,ShowInTaskbar=false,ShowActivated=false};
            shell.Show();var controller=shell.EnableReminders(()=>at,TimeZoneInfo.Utc,()=>true);
            var files=new Files {ExportPath=Path.Combine(folder,"active.daykeep.json")};var tools=shell.OpenDataTools(files)!;
            void Tap(string name)=>SmokeRunner.Invoke((Button)tools.FindName(name));
            try
            {
                at=at.AddMinutes(1);controller.Tick();var keys=live.Capture().Deliveries.Select(d=>d.Key).ToHashSet();Tap("BackupTab");Tap("ExportButton");
                foreach(var item in live.LoadAll()) live.Complete(item);controller.Tick();
                check("L5 importing active reminders refreshes a running controller without duplicate keys",()=>
                {
                    Assert(controller.Window is null);files.ImportPath=files.ExportPath;Tap("ChooseButton");((ComboBox)tools.FindName("ModeInput")).SelectedIndex=1;
                    ((CheckBox)tools.FindName("ConfirmInput")).IsChecked=true;Tap("ImportButton");
                    Assert(controller.Window is {IsVisible:true,TaskCount:2} && live.Capture().Deliveries.Select(d=>d.Key).ToHashSet().SetEquals(keys) && live.Capture().Days.All(d=>d.ExtraCount==0));
                });
                check("L5 replacing with an empty backup clears obsolete visible reminder cards",()=>
                {
                    var empty=new TaskService(new TaskRepository(Path.Combine(folder,"empty.db")));empty.Initialize();var path=Path.Combine(folder,"empty.daykeep.json");empty.ExportBackup(path);
                    files.ImportPath=path;Tap("ChooseButton");((ComboBox)tools.FindName("ModeInput")).SelectedIndex=1;((CheckBox)tools.FindName("ConfirmInput")).IsChecked=true;Tap("ImportButton");
                    Assert(controller.Window is null && live.LoadAll().Count==0);
                });
            }
            finally {shell.Close();}
        }
        void Sql(string sql)
        {
            using var connection=new SqliteConnection($"Data Source={service.Repository.DatabasePath};Pooling=False");connection.Open();using var command=connection.CreateCommand();command.CommandText=sql;command.ExecuteNonQuery();
        }
    }
    private sealed class Files : IDataFilePicker
    {
        public string? ExportPath {get;set;}
        public string? ImportPath {get;set;}
        public string? Export(Window owner)=>ExportPath;
        public string? Import(Window owner)=>ImportPath;
    }
}
