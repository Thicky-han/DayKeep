using System.IO;
using System.Windows;
using System.Windows.Controls;
using DayKeep.Core;
using DayKeep.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace DayKeep.Infrastructure;

internal static class RecoveryUiChecks
{
    public static void Run(string directory,Action<string,Action> check)
    {
        var now=new DateTimeOffset(2026,10,26,8,59,0,TimeSpan.Zero); var available=true;
        var service=new TaskService(new TaskRepository(Path.Combine(directory,"recovery-ui.db")),()=>now); service.Initialize();
        service.Create(new("休眠后查看缴费入口（测试）",TaskKind.WaitingForCondition,Times:"09:00, 14:00"));
        service.Create(new("重启后待办理事项（测试）",TaskKind.Todo,Times:"09:00, 14:00"));
        ReminderController Open() { var engine=new ReminderController(service,()=>{},()=>now,TimeZoneInfo.Utc,()=>available); engine.Start(); return engine; }
        var controller=Open();
        void Sql(string sql) { using var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=service.Repository.DatabasePath,Pooling=false}.ToString());c.Open();using var command=c.CreateCommand();command.CommandText=sql;command.ExecuteNonQuery(); }
        static void Assert(bool value) { if(!value) throw new InvalidOperationException("L4 UI expectation failed"); }
        try
        {
            check("L4 startup after a missed time shows one merged recovery window",()=> { controller.Dispose();now=now.AddHours(2);controller=Open();Assert(controller.Window is {TaskCount:2}); var cards=((ItemsControl)controller.Window!.FindName("DueTaskList")).Items.Cast<TaskCard>();Assert(cards.All(c=>c.ReminderDetail.Contains("原定 10月26日 09:00"))); });
            if(controller.Window is { } recovered) SmokeRunner.Render(recovered,Path.Combine(directory,"daykeep-l4-recovered.png"));
            check("L4 dismissed delivery remains dismissed after controller restart",()=> { controller.Window!.Close();controller.Dispose();controller=Open();Assert(controller.Window is null); });
            check("L4 visible delivery restores with the same identifiers and budget",()=> {now=new(2026,10,26,14,10,0,TimeSpan.Zero);controller.Tick();var before=service.ReminderSnapshot(new(2026,10,26));controller.Dispose();controller=Open();var after=service.ReminderSnapshot(new(2026,10,26));Assert(controller.Window?.TaskCount==2 && before.Deliveries.SequenceEqual(after.Deliveries) && before.Progress.SequenceEqual(after.Progress));});
            check("L4 unavailable desktop hides the window without consuming new reminders",()=> {available=false;var count=service.ReminderSnapshot(new(2026,10,26)).Deliveries.Count;now=now.AddDays(2);controller.Tick();Assert(controller.Window is {IsVisible:false} && service.ReminderSnapshot(new(2026,10,28)).Deliveries.Count==count);});
            check("L4 resume merges several missed days once and reuses a single window",()=> {available=true;controller.RefreshEnvironment();Assert(controller.Window is {IsVisible:true,TaskCount:2});var count=service.ReminderSnapshot(new(2026,10,28)).Deliveries.Count;controller.RefreshEnvironment();controller.RefreshEnvironment();Assert(service.ReminderSnapshot(new(2026,10,28)).Deliveries.Count==count);});
            check("L4 a backwards clock does not replay dismissed fixed reminders",()=> {controller.Window!.Close();now=new(2026,10,26,13,50,0,TimeSpan.Zero);controller.RefreshEnvironment();Assert(controller.Window is null);});
            check("L4 failed dismissal keeps the real window open with a visible error",()=> {now=new(2026,10,28,14,30,0,TimeSpan.Zero);controller.Tick();Sql("CREATE TRIGGER reject_dismiss BEFORE UPDATE ON reminder_deliveries WHEN NEW.status=2 BEGIN SELECT RAISE(ABORT,'controlled'); END;");controller.Window!.Close();Assert(controller.Window is {IsVisible:true} && ((FrameworkElement)controller.Window.FindName("ErrorLabel")).Visibility==Visibility.Visible);Sql("DROP TRIGGER reject_dismiss;");controller.Window.Close();Assert(controller.Window is null);});
        }
        finally {controller.Dispose();}
        check("L4 fullscreen geometry distinguishes maximized work area and full monitor",()=> {Assert(DesktopAvailability.CoversScreen(new(-1920,0,1920,1080),new(-1920,0,1920,1080)));Assert(!DesktopAvailability.CoversScreen(new(-1920,0,1920,1040),new(-1920,0,1920,1080)));});
        check("L4 physical pixel placement handles negative monitors and reduced work areas",()=> {Assert(WindowPlacement.Fit(new(-3000,-200,2000,1300),new(-1920,0,1920,1040))==new PixelBounds(-1920,0,1920,1040));Assert(WindowPlacement.Fit(new(6000,2000,900,700),new(1920,0,2560,1400))==new PixelBounds(3580,700,900,700));});
        var startup=new FakeStartup(); var main=new MainWindow(new SettingsStore(Path.Combine(directory,"startup-ui.json")),service,startup) {AllowClose=true,ShowActivated=false,ShowInTaskbar=false,Left=-20000,Top=-20000,WindowStartupLocation=WindowStartupLocation.Manual};main.Show();
        try
        {
            SmokeRunner.Invoke((Button)main.FindName("SettingsNav"));
            check("L4 login startup is off by default and UI toggle writes only on explicit change",()=> {var toggle=(CheckBox)main.FindName("StartupInput");Assert(toggle.IsChecked==false && startup.Writes==0);toggle.IsChecked=true;Assert(startup.Enabled && startup.Writes==1);toggle.IsChecked=false;Assert(!startup.Enabled && startup.Writes==2);});
            check("L4 startup failure is visible and checkbox reflects the stored value",()=> {startup.Fail=true;((CheckBox)main.FindName("StartupInput")).IsChecked=true;Assert(((CheckBox)main.FindName("StartupInput")).IsChecked==false && ((TextBlock)main.FindName("StartupStatus")).Text.Contains("未能完成"));});
            check("L4 real offscreen window returns inside the current monitor work area",()=> {WindowPlacement.EnsureVisible(main);var handle=new System.Windows.Interop.WindowInteropHelper(main).Handle;var work=System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;var bounds=WindowPlacement.Bounds(main)!.Value;Assert(bounds==WindowPlacement.Fit(bounds,new(work.X,work.Y,work.Width,work.Height)));});
            SmokeRunner.Render(main,Path.Combine(directory,"daykeep-l4-startup-settings.png"));
        }
        finally {main.Close();}
        check("L4 startup registry adapter quotes paths and preserves unrelated values",()=>
        {
            var key=@"Software\DayKeep\Tests\"+Guid.NewGuid().ToString("N");
            var folder=Path.Combine(directory,"startup path with spaces");Directory.CreateDirectory(folder);var executable=Path.Combine(folder,"DayKeep.exe");File.WriteAllText(executable,"not executed: registry adapter fixture");
            var store=new RegistryStartupValue(key,"TestValue"); var actual=new LoginStartup(store,executable);
            try
            {
                Assert(!actual.Read().Enabled);actual.SetEnabled(true);Assert(store.Read()==$"\"{executable}\" --background" && actual.Read().Enabled);actual.SetEnabled(false);Assert(store.Read() is null);
                store.Write("unrelated-command");try {actual.SetEnabled(true);throw new InvalidOperationException("Unrelated startup replaced");}catch(IOException) {}Assert(store.Read()=="unrelated-command");
                using var binary=Registry.CurrentUser.OpenSubKey(key,true)!;binary.SetValue("TestValue",new byte[]{1,2,3},RegistryValueKind.Binary);
                try {actual.SetEnabled(true);throw new InvalidOperationException("Unrecognized startup type replaced");}catch(IOException) {}Assert(binary.GetValueKind("TestValue")==RegistryValueKind.Binary);
            }
            finally {Registry.CurrentUser.DeleteSubKeyTree(key,false);}
        });
    }
    private sealed class FakeStartup : ILoginStartup
    {
        public bool Enabled; public bool Fail; public int Writes;
        public LoginStartupState Read()=>new(Enabled,Enabled ? "已配置托盘启动（隔离验证）" : "登录启动已关闭（隔离验证）");
        public LoginStartupState SetEnabled(bool enabled) {if(Fail) throw new IOException("controlled");Writes++;Enabled=enabled;return Read();}
    }
}
