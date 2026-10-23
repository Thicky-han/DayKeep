using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using DayKeep.Core;
using DayKeep.Localization;
using DayKeep.Models;

namespace DayKeep.Infrastructure;

internal static class LanguageUiChecks
{
    public static void Run(string directory,Action<string,Action> check)
    {
        var folder=Path.Combine(directory,"language-ui");Directory.CreateDirectory(folder);
        Localizer.SetLanguage("zh-CN");
        var now=new DateTimeOffset(2026,10,26,8,59,0,TimeSpan.Zero);
        var service=new TaskService(new TaskRepository(Path.Combine(folder,"tasks.db")),()=>now);service.Initialize();
        var task=service.Create(new("任务名称",TaskKind.WaitingForCondition,Notes:"自定义 메모 • Original notes",Times:"09:00, 14:30",DueDate:"2026-11-02"));
        var store=new SettingsStore(Path.Combine(folder,"settings.json"));
        var window=new MainWindow(store,service) {AllowClose=true,ShowInTaskbar=false,ShowActivated=false};
        window.Show();Pump();
        ComboBox Languages()=>(ComboBox)window.FindName("LanguageInput");
        void Select(string code) {Languages().SelectedValue=code;Pump();Assert(Localizer.LanguageCode==code,"Language selector did not apply "+code);}
        string Hash()=>BackupFile.Hash(service.Capture());
        static string Text(Window owner,string name)=>((TextBlock)owner.FindName(name)).Text;
        static TextBox Input(Window owner,string name)=>(TextBox)owner.FindName(name);
        static void Click(Window owner,string name)=>SmokeRunner.Invoke((Button)owner.FindName(name));
        try
        {
            check("Languages have complete resource keys and matching format placeholders",()=>
            {
                var keys=Localizer.Keys.ToHashSet(StringComparer.Ordinal);
                Assert(Localizer.Languages.Count==9 && keys.Count>=300,"Unexpected language or resource count");
                foreach(var language in Localizer.Languages)
                {
                    Localizer.SetLanguage(language.Code);
                    var catalog=Localizer.Catalog(language.Code);
                    Assert(keys.SetEquals(catalog.Keys),"Missing or extra language keys: "+language.Code);
                    foreach(var (key,value) in catalog)
                    {
                        Assert(!string.IsNullOrWhiteSpace(value),"Empty translation: "+language.Code);
                        Assert(Placeholders(key).SequenceEqual(Placeholders(value)),"Format placeholders differ: "+language.Code+" / "+key);
                        _=Localizer.T(key,Enumerable.Range(0,8).Select(i=>(object?)i).ToArray());
                    }
                }
                Localizer.SetLanguage("zh-CN");
            });
            check("Top language selector lists all nine native language names",()=>
            {
                var choices=Languages().Items.Cast<LanguageChoice>().ToArray();
                Assert(choices.Select(c=>c.Code).SequenceEqual(Localizer.Languages.Select(c=>c.Code)),"Selector languages differ");
                Assert(choices.Select(c=>c.NativeName).SequenceEqual(new[]{"简体中文","繁體中文","English","日本語","한국어","Français","Deutsch","Español","Português"}),"Native self names missing");
                Assert(choices.All(c=>c.ToString()==c.NativeName),"Internal language details leaked");
            });
            check("Language dropdown opens through real WPF automation",()=>
            {
                var combo=Languages();var toggle=SmokeRunner.Descendants<ToggleButton>(combo).Single();
                ((IToggleProvider)new ToggleButtonAutomationPeer(toggle).GetPattern(PatternInterface.Toggle)).Toggle();Pump();
                Assert(combo.IsDropDownOpen,"Language dropdown did not open");combo.IsDropDownOpen=false;
            });
            check("All nine selections update real controls without translating user data or changing SQLite",()=>
            {
                var before=Hash();
                foreach(var language in Localizer.Languages)
                {
                    Select(language.Code);
                    Assert(((Button)window.FindName("NewTaskButton")).Content?.ToString()==Localizer.T("＋ 新建任务"),"Primary action stayed in the previous language: "+language.Code);
                    var card=((ItemsControl)window.FindName("TaskList")).Items.Cast<TaskCard>().Single();
                    Assert(card.Title==task.Title && card.Item.Notes==task.Notes && card.Status==Localizer.T("等待条件满足"),"Task content or status language changed incorrectly");
                    Assert(Hash()==before,"Language selection modified SQLite: "+language.Code);
                }
            });
            check("All nine languages keep compact language and window controls inside the header",()=>
            {
                window.SetCompact(true);
                foreach(var language in Localizer.Languages)
                {
                    Select(language.Code);var combo=Languages();var brand=SmokeRunner.Descendants<TextBlock>(window).Single(t=>t.Text=="日守");
                    var pin=(Button)window.FindName("PinButton");var compact=(Button)window.FindName("CompactButton");
                    var hide=SmokeRunner.Descendants<Button>(window).Single(b=>b.Content?.ToString()=="×" && b.IsVisible);
                    Assert(combo.IsVisible && combo.ActualWidth>70 && combo.ActualHeight>20 && Inside(Bounds(combo,window),window),"Compact language selector was clipped or hidden: "+language.Code);
                    Assert(new[]{pin,compact,hide}.All(button=>button.IsVisible && Inside(Bounds(button,window),window)) && Bounds(pin,window).Left>=Bounds(brand,window).Right-0.5,"Compact window controls overlap branding or leave the window: "+language.Code);
                }
                Select("en");
                SmokeRunner.Render(window,Path.Combine(directory,"daykeep-language-en-compact.png"));window.SetCompact(false);
            });
            check("English main view renders translated task actions and dates",()=>
            {
                Select("en");Pump();
                Assert(Text(window,"DateLabel")==window.Today.ToDateTime(TimeOnly.MinValue).ToString("D",Localizer.Culture),"Date header did not use selected culture");
                Assert(SmokeRunner.Descendants<Button>(window).Any(b=>b.Tag is TaskItem && b.Content?.ToString()==Localizer.T("编辑")),"Translated edit action missing");
                SmokeRunner.Render(window,Path.Combine(directory,"daykeep-language-en-full.png"));
            });
            check("Long French and German wording and Japanese text render through every main page",()=>
            {
                foreach(var code in new[]{"de","fr","ja"})
                {
                    Select(code);Click(window,"SettingsNav");window.UpdateLayout();
                    Assert(((FrameworkElement)window.FindName("SettingsPage")).IsVisible && Languages().IsVisible,"Settings or language control hidden");
                    SmokeRunner.Render(window,Path.Combine(directory,"daykeep-language-"+code+"-settings.png"));
                    Click(window,"TodayNav");SmokeRunner.Render(window,Path.Combine(directory,"daykeep-language-"+code+"-full.png"));
                }
            });
            check("Selected language persists to the real settings file and restores in a new window",()=>
            {
                Select("en");Assert(new SettingsStore(store.FilePath).Load().Language=="en","Language was not saved");
                var reopened=new MainWindow(new SettingsStore(store.FilePath),new TaskService(new TaskRepository(service.Repository.DatabasePath))) {AllowClose=true,ShowInTaskbar=false,ShowActivated=false};
                try {reopened.Show();Pump();Assert(Localizer.LanguageCode=="en" && ((ComboBox)reopened.FindName("LanguageInput")).SelectedValue?.ToString()=="en","Saved language was not restored");}
                finally {reopened.Close();}
            });
            check("Old, null and unsupported language settings fall back without overwriting files",()=>
            {
                var fixtures=new[]{"{\"SurfaceOpacity\":0.75,\"Compact\":true}","{\"Language\":null}","{\"Language\":\"unsupported-Locale\"}"};
                for(var i=0;i<fixtures.Length;i++)
                {
                    var path=Path.Combine(folder,"fallback-"+i+".json");File.WriteAllText(path,fixtures[i]);var loaded=new SettingsStore(path).Load();
                    Assert(loaded.Language=="zh-CN" && File.ReadAllText(path)==fixtures[i],"Fallback changed the original settings");
                    if(i==0) Assert(loaded.Compact && loaded.SurfaceOpacity==0.75,"Old appearance settings were lost");
                }
            });
            check("Failed language setting writes visibly retain the prior saved language and selection",()=>
            {
                var path=Path.Combine(folder,"blocked-settings.json");var original="{\"Language\":\"en\"}";File.WriteAllText(path,original);Directory.CreateDirectory(path+".tmp");
                var blockedStore=new SettingsStore(path);
                var blocked=new MainWindow(blockedStore,new TaskService(new TaskRepository(Path.Combine(folder,"blocked-tasks.db")))) {AllowClose=true,ShowInTaskbar=false,ShowActivated=false};
                try
                {
                    blocked.Show();blocked.SetLanguage("fr");Pump();
                    Assert(Localizer.LanguageCode=="en" && ((ComboBox)blocked.FindName("LanguageInput")).SelectedValue?.ToString()=="en" && blockedStore.LastError is not null && Text(blocked,"StatusLabel")==Localizer.T("语言设置未能保存，已保留原语言。请检查文件夹权限后重试。") && File.ReadAllText(path)==original,"Failed settings write was hidden or changed the saved language");
                }
                finally {blocked.Close();window.SetLanguage("en");}
            });
            check("Switching language preserves new task fields, weekdays and selected filter",()=>
            {
                ((ComboBox)window.FindName("FilterInput")).SelectedIndex=3;
                var form=window.OpenEditor(null)!;var before=Hash();
                Input(form,"TitleInput").Text="未保存の草稿 Draft";Input(form,"NotesInput").Text="Original 草稿 메모";Input(form,"UrlInput").Text="https://example.org/patent";
                Input(form,"TimesInput").Text="09:05, 14:30";Input(form,"DueInput").Text="2026-11-02";Input(form,"RepeatInput").Text="45";Input(form,"MaxExtraInput").Text="2";
                ((ComboBox)form.FindName("KindInput")).SelectedIndex=0;
                var weekdays=((Panel)form.FindName("WeekdayPanel")).Children.OfType<CheckBox>().ToArray();weekdays[2].IsChecked=false;
                var selectedDays=weekdays.Select(c=>c.IsChecked).ToArray();Select("fr");
                Assert(ReferenceEquals(window.Editor,form) && form.IsVisible && Input(form,"TitleInput").Text=="未保存の草稿 Draft" && Input(form,"NotesInput").Text=="Original 草稿 메모" && Input(form,"UrlInput").Text=="https://example.org/patent" && Input(form,"TimesInput").Text=="09:05, 14:30" && Input(form,"DueInput").Text=="2026-11-02" && Input(form,"RepeatInput").Text=="45" && Input(form,"MaxExtraInput").Text=="2" && ((ComboBox)form.FindName("KindInput")).SelectedIndex==0 && selectedDays.SequenceEqual(weekdays.Select(c=>c.IsChecked)),"New task draft was reset");
                Assert(((ComboBox)window.FindName("FilterInput")).SelectedIndex==3 && Hash()==before,"Language switch changed filter or saved a draft");
                Select("en");SmokeRunner.Render(form,Path.Combine(directory,"daykeep-language-en-editor.png"));form.Close();
            });
            check("An existing task edit stays unsaved and preserves its draft while switching language",()=>
            {
                var before=Hash();var form=window.OpenEditor(service.LoadAll().Single())!;
                Input(form,"TitleInput").Text="Editing 草稿";Input(form,"NotesInput").Text="not saved";Select("ja");
                Assert(ReferenceEquals(window.Editor,form) && Input(form,"TitleInput").Text=="Editing 草稿" && Input(form,"NotesInput").Text=="not saved" && Hash()==before,"Existing edit was lost or silently saved");form.Close();
            });
            check("Visible validation errors translate immediately without resubmitting a form",()=>
            {
                Select("en");var before=Hash();var form=window.OpenEditor(null)!;Click(form,"SaveButton");
                Assert(Text(form,"ErrorLabel")==Localizer.T("任务名称需要 1—120 个字符。"),"English validation error missing");Select("de");
                Assert(((FrameworkElement)form.FindName("ErrorLabel")).Visibility==Visibility.Visible && Text(form,"ErrorLabel")==Localizer.T("任务名称需要 1—120 个字符。") && Hash()==before,"Visible error was not retranslated safely");form.Close();
            });
            check("Date and clock input syntax remains invariant under a French UI",()=>
            {
                Select("fr");var form=window.OpenEditor(null)!;Input(form,"TitleInput").Text="Date syntax check";Input(form,"TimesInput").Text="14:30, 09:05";Input(form,"DueInput").Text="2026-11-02";
                Click(form,"SaveButton");var saved=service.LoadAll().Single(t=>t.Title=="Date syntax check");
                Assert(window.Editor is null && saved.DueDate==new DateOnly(2026,11,2) && saved.Reminder.Times=="09:05, 14:30","Translated UI changed date/time parsing");
            });
            check("Open sample reminder updates language without creating tasks",()=>
            {
                Select("zh-CN");var before=Hash();var preview=window.OpenPreview(PreviewTask.Examples[0]);Select("en");
                Assert(preview.IsVisible && Text(preview,"TaskTitle")==Localizer.T("查看专利缴费入口") && Text(preview,"TaskDescription")==Localizer.T("今天查过还未开放？记录一次检查，明天继续跟进。") && preview.Language.IetfLanguageTag.Equals(Localizer.Culture.IetfLanguageTag,StringComparison.OrdinalIgnoreCase) && Hash()==before,"Preview did not translate safely");
                SmokeRunner.Render(preview,Path.Combine(directory,"daykeep-language-en-preview.png"));
                foreach(var language in Localizer.Languages)
                {
                    Select(language.Code);var body=SmokeRunner.Descendants<ScrollViewer>(preview).Single();
                    var footer=SmokeRunner.Descendants<Button>(preview).Where(button=>button.Content?.ToString()==Localizer.T("了解了，收起预览") || button.Content?.ToString()==Localizer.T("返回日守")).ToArray();
                    Assert(body.IsVisible && body.ActualHeight>100 && body.ViewportHeight>0 && Inside(Bounds(body,preview),preview) && footer.Length==2 && footer.All(button=>Inside(Bounds(button,preview),preview) && Bounds(button,preview).Top>=Bounds(body,preview).Bottom-0.5),"Translated preview body overlaps footer or cannot scroll: "+language.Code);
                    if(body.ScrollableHeight>0) {body.ScrollToEnd();Pump();Assert(Math.Abs(body.VerticalOffset-body.ScrollableHeight)<1,"Preview text cannot scroll to its end: "+language.Code);body.ScrollToHome();}
                    if(language.Code=="de") SmokeRunner.Render(preview,Path.Combine(directory,"daykeep-language-de-preview.png"));
                }
                Assert(Hash()==before,"Preview translation created data");preview.Close();
            });
            check("History translates operation labels but preserves task titles and notes",()=>
            {
                Select("en");var before=Hash();var data=window.OpenDataTools()!;var list=(ItemsControl)data.FindName("HistoryList");
                var row=list.Items.Cast<object>().First(item=>item.GetType().GetProperty("Title")?.GetValue(item)?.ToString()==task.Title);
                Assert(row.GetType().GetProperty("Title")!.GetValue(row)?.ToString()==task.Title && row.GetType().GetProperty("Action")!.GetValue(row)?.ToString()==Localizer.T("创建事项") && Hash()==before,"History translated user content or kept a Chinese operation");
                Select("fr");Assert(((TextBox)data.FindName("SearchInput")).Text=="" && Hash()==before,"History language change modified the database");data.Close();
            });
            check("Real English and French history searches match partial localized operations",()=>
            {
                var data=window.OpenDataTools()!;var before=Hash();
                try
                {
                    foreach(var code in new[]{"en","fr"})
                    {
                        Select(code);var word=Localizer.T("创建事项").Split(' ').Last();Input(data,"SearchInput").Text=word[..3];Click(data,"QueryButton");
                        var rows=((ItemsControl)data.FindName("HistoryList")).Items.Cast<object>().ToArray();
                        Assert(rows.Length==2 && rows.All(row=>row.GetType().GetProperty("Action")!.GetValue(row)?.ToString()==Localizer.T("创建事项")),"Partial localized operation did not match: "+code);
                    }
                    Assert(Hash()==before,"Localized search changed SQLite");
                }
                finally {data.Close();}
            });
            check("History language switching preserves task and date selections and the user's search text",()=>
            {
                Select("en");var data=window.OpenDataTools()!;var before=Hash();
                try
                {
                    var taskInput=(ComboBox)data.FindName("TaskInput");
                    taskInput.SelectedItem=taskInput.Items.Cast<object>().Single(item=>item.GetType().GetProperty("Id")!.GetValue(item) is Guid id && id==task.Id);
                    var date=ReminderPlanner.LocalDate(now,TimeZoneInfo.Local).ToDateTime(TimeOnly.MinValue);
                    ((DatePicker)data.FindName("FromInput")).SelectedDate=date;((DatePicker)data.FindName("ToInput")).SelectedDate=date;
                    Input(data,"SearchInput").Text=task.Title;Click(data,"QueryButton");Assert(((ItemsControl)data.FindName("HistoryList")).Items.Count==1,"History filter fixture failed");Select("fr");
                    Assert(Input(data,"SearchInput").Text==task.Title && ((DatePicker)data.FindName("FromInput")).SelectedDate==date && ((DatePicker)data.FindName("ToInput")).SelectedDate==date && taskInput.SelectedItem!.GetType().GetProperty("Id")!.GetValue(taskInput.SelectedItem) is Guid selectedId && selectedId==task.Id,"History filter draft changed during translation");
                    Click(data,"QueryButton");Assert(((ItemsControl)data.FindName("HistoryList")).Items.Count==1 && Hash()==before,"Original title query or history dates changed meaning");
                }
                finally {data.Close();}
            });
            check("Backup preview retains its reviewed file and mode, clears confirmation and translates effects",()=>
            {
                var path=Path.Combine(folder,"reviewed.daykeep.json");service.ExportBackup(path);var sourceBytes=File.ReadAllBytes(path);var files=new Files(path);
                Select("zh-CN");var data=window.OpenDataTools(files)!;Click(data,"BackupTab");Click(data,"ChooseButton");((ComboBox)data.FindName("ModeInput")).SelectedIndex=1;
                ((CheckBox)data.FindName("ConfirmInput")).IsChecked=true;Assert(((Button)data.FindName("ImportButton")).IsEnabled,"Fixture not confirmed");var before=Hash();Select("en");
                Assert(((ComboBox)data.FindName("ModeInput")).SelectedIndex==1 && Input(data,"FilePathLabel").Text==path && ((FrameworkElement)data.FindName("PreviewPanel")).Visibility==Visibility.Visible && ((CheckBox)data.FindName("ConfirmInput")).IsChecked==false && !((Button)data.FindName("ImportButton")).IsEnabled,"Language change lost preview or kept stale confirmation");
                Assert(Text(data,"PlanLabel")==Localizer.T("替换后为 {0} 件事项。将移除本机备份中没有的 {1} 件，并恢复备份内的状态和记录。",service.LoadAll().Count,0) && Hash()==before && File.ReadAllBytes(path).SequenceEqual(sourceBytes),"Translated preview changed data or source file");
                SmokeRunner.Render(data,Path.Combine(directory,"daykeep-language-en-backup.png"));data.Close();
            });
            ReminderChecks();
            check("Closing translated windows detaches language handlers and does not revive them",()=>
            {
                var form=window.OpenEditor(null)!;var preview=window.OpenPreview(PreviewTask.Examples[0]);var data=window.OpenDataTools()!;
                form.Close();preview.Close();data.Close();window.Close();
                Assert(!Subscribed(form) && !Subscribed(preview) && !Subscribed(data) && !Subscribed(window),"A closed window remains subscribed to the global language event");
                foreach(var language in Localizer.Languages) Localizer.SetLanguage(language.Code);
                Assert(!window.IsVisible && !form.IsVisible && !preview.IsVisible && !data.IsVisible,"Language change revived a closed window");
            });
        }
        finally {window.Close();Localizer.SetLanguage("zh-CN");}

        void ReminderChecks()
        {
            var at=new DateTimeOffset(2026,10,26,8,59,0,TimeSpan.Zero);
            var reminders=new TaskService(new TaskRepository(Path.Combine(folder,"reminder-tasks.db")),()=>at);reminders.Initialize();
            reminders.Create(new("Reminder 原文",TaskKind.WaitingForCondition));
            var shell=new MainWindow(new SettingsStore(Path.Combine(folder,"reminder-settings.json")),reminders) {AllowClose=true,ShowInTaskbar=false,ShowActivated=false};
            shell.Show();var engine=shell.EnableReminders(()=>at,TimeZoneInfo.Utc,()=>true);var presented=0;engine.Presented+=_=>presented++;
            // This test owns an explicit clock and tick. Stop background timer races while changing that clock.
            ((DispatcherTimer)typeof(ReminderController).GetField("_timer",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(engine)!).Stop();
            try
            {
                at=at.AddMinutes(1);engine.Tick();var popup=engine.Window!;
                check("Current reminder language updates preserve popup, delivery identities and daily budget",()=>
                {
                    Assert(popup is {IsVisible:true} && presented==1,"Reminder fixture missing");var before=BackupFile.Hash(reminders.Capture());
                    shell.SetLanguage("en");Pump();
                    Assert(ReferenceEquals(popup,engine.Window) && popup.IsVisible && Text(popup,"CountLabel")==Localizer.T("{0} 件事项 · 需要继续跟进",1) && ((ItemsControl)popup.FindName("DueTaskList")).Items.Cast<TaskCard>().Single().Status==Localizer.T("等待条件满足") && popup.Language.IetfLanguageTag.Equals(Localizer.Culture.IetfLanguageTag,StringComparison.OrdinalIgnoreCase) && BackupFile.Hash(reminders.Capture())==before && presented==1,"Translation mutated or duplicated a visible delivery");
                    SmokeRunner.Render(popup,Path.Combine(directory,"daykeep-language-en-due.png"));
                });
                check("Switching language at a due repeat redraws only; explicit timer tick still delivers once",()=>
                {
                    at=at.AddMinutes(30);var before=BackupFile.Hash(reminders.Capture());var beforeKeys=reminders.Capture().Deliveries.Select(d=>d.Key).ToArray();
                    shell.SetLanguage("fr");Pump();
                    Assert(BackupFile.Hash(reminders.Capture())==before && reminders.Capture().Deliveries.Select(d=>d.Key).SequenceEqual(beforeKeys) && reminders.Capture().Days.All(d=>d.ExtraCount==0) && presented==1 && ReferenceEquals(popup,engine.Window),"Language switch ran the scheduler");
                    engine.Tick();Assert(reminders.Capture().Days.Single().ExtraCount==1 && reminders.Capture().Deliveries.Count(d=>d.Key.Kind==ReminderKind.Repeat)==1,"Timer did not deliver exactly one repeat after translation");
                });
                check("Reminder errors retranslate and disposed reminders detach language handlers",()=>
                {
                    engine.Window!.ShowError("本次收起未能保存，请重新读取数据后重试。提醒已保留。");shell.SetLanguage("de");Pump();
                    Assert(Text(engine.Window!,"ErrorLabel")==Localizer.T("本次收起未能保存，请重新读取数据后重试。提醒已保留。"),"Visible reminder error did not translate");
                    var closed=engine.Window;engine.Dispose();Assert(!Subscribed(engine) && !Subscribed(closed!),"Reminder or controller language handler leaked");
                });
            }
            finally {shell.Close();window.SetLanguage("en");}
        }
    }

    private static string[] Placeholders(string text)=>Regex.Matches(text,@"\{(\d+)(?:[^{}]*)\}").Select(m=>m.Groups[1].Value).OrderBy(v=>v,StringComparer.Ordinal).ToArray();
    private static Rect Bounds(FrameworkElement element,Window owner)=>element.TransformToAncestor(owner).TransformBounds(new Rect(0,0,element.ActualWidth,element.ActualHeight));
    private static bool Inside(Rect bounds,Window owner)=>bounds.Left>=-0.5 && bounds.Top>=-0.5 && bounds.Right<=owner.ActualWidth+0.5 && bounds.Bottom<=owner.ActualHeight+0.5;
    private static bool Subscribed(object target)=>typeof(Localizer).GetField("Changed",BindingFlags.NonPublic|BindingFlags.Static)?.GetValue(null) is MulticastDelegate handlers && handlers.GetInvocationList().Any(handler=>ReferenceEquals(handler.Target,target));
    private static void Pump()=>System.Windows.Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Assert(bool condition,string message) {if(!condition) throw new InvalidOperationException(message);}
    private sealed class Files(string path) : IDataFilePicker
    {
        public string? Export(Window owner)=>null;
        public string? Import(Window owner)=>path;
    }
}
