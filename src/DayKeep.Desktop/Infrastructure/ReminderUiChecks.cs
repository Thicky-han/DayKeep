using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DayKeep.Core;
using DayKeep.Models;

namespace DayKeep.Infrastructure;

internal static class ReminderUiChecks
{
    public static void Run(string directory, Action<string,Action> check)
    {
        var now=new DateTimeOffset(2026,10,26,8,59,58,TimeSpan.Zero);
        var available=true;
        var links=new List<Uri>();
        var service=new TaskService(new TaskRepository(Path.Combine(directory,"reminders.db")),()=>now);
        service.Initialize();
        var a=service.Create(new("每日检查入口（测试）",TaskKind.WaitingForCondition,"https://example.org/a",Times:"09:00, 14:00"));
        var b=service.Create(new("等待条件后办理（测试）",TaskKind.WaitingForCondition,Times:"09:00, 14:00"));
        var c=service.Create(new("办理事项（测试）",TaskKind.Todo,Times:"09:00, 14:00"));
        var main=new MainWindow(new SettingsStore(Path.Combine(directory,"reminders-ui.json")),service)
        { AllowClose=true, ShowActivated=false,ShowInTaskbar=false,Left=-20000,Top=-20000,WindowStartupLocation=WindowStartupLocation.Manual };
        main.Show();
        var controller=main.EnableReminders(()=>now,TimeZoneInfo.Utc,()=>available,uri=>links.Add(uri));
        var presented=0; controller.Presented+=_=>presented++;
        static void Assert(bool result,string message="Reminder UI expectation failed") { if(!result) throw new InvalidOperationException(message); }
        Button Action(Guid id,string label)
        {
            var window=controller.Window ?? throw new InvalidOperationException("Reminder missing"); window.UpdateLayout();
            return SmokeRunner.Descendants<Button>(window).First(x=>x.Tag is TaskItem task && task.Id==id && x.Content?.ToString()==label && x.Visibility==Visibility.Visible);
        }
        try
        {
            check("L2 future slots do not create an early window",()=>Assert(controller.Window is null));
            now=now.AddSeconds(2); controller.Tick();
            check("L2 simultaneous tasks merge in one topmost non-activating window",()=>Assert(controller.Window is { TaskCount:3,Topmost:true,ShowActivated:false } && presented==1));
            if(controller.Window is { } merged) SmokeRunner.Render(merged,Path.Combine(directory,"daykeep-due-merged.png"));
            check("L2 repeated ticks reuse existing window without duplicate tasks",()=> { var before=controller.Window; controller.Tick(); controller.Tick(); Assert(ReferenceEquals(before,controller.Window) && controller.Window?.TaskCount==3 && presented==1); });
            check("L2 opening platform dispatches only a validated URI and no task mutation",()=>
            {
                var before=service.LoadAll().Single(t=>t.Id==a.Id); SmokeRunner.Invoke(Action(a.Id,"打开平台"));
                Assert(links.Single().AbsoluteUri=="https://example.org/a" && service.LoadAll().Single(t=>t.Id==a.Id)==before && service.Repository.LoadChecks(a.Id).Count==0);
            });
            check("L2 today's check removes only its reminder and preserves waiting state",()=>
            {
                SmokeRunner.Invoke(Action(a.Id,"今日已检查，尚未开放"));
                Assert(controller.Window?.TaskCount==2 && service.LoadAll().Single(t=>t.Id==a.Id).State==TaskState.Waiting && service.Repository.LoadChecks(a.Id).Single().LocalDate==new DateOnly(2026,10,26));
                var cards=((ItemsControl)main.FindName("TaskList")).Items.Cast<TaskCard>(); Assert(cards.Single(x=>x.Item.Id==a.Id).CheckedToday);
            });
            check("L2 confirmation of opening keeps the task pending with completion action",()=>
            {
                SmokeRunner.Invoke(Action(b.Id,"已开放，转待办理"));
                Assert(service.LoadAll().Single(t=>t.Id==b.Id).State==TaskState.Ready && service.LoadAll().Single(t=>t.Id==b.Id).CompletedAt is null && controller.Window?.TaskCount==2);
                _=Action(b.Id,"确认已完成");
            });
            if(controller.Window is { } ready) SmokeRunner.Render(ready,Path.Combine(directory,"daykeep-due-ready.png"));
            check("L2 explicit completion removes its reminder and persists completion",()=> { SmokeRunner.Invoke(Action(b.Id,"确认已完成")); Assert(controller.Window?.TaskCount==1 && service.LoadAll().Single(t=>t.Id==b.Id).State==TaskState.Completed); });
            check("L2 reminder stays visible after its scheduled minute",()=> { now=now.AddMinutes(1); controller.Tick(); Assert(controller.Window is {IsVisible:true,TaskCount:1}); });
            check("L2 hiding main window keeps actual reminder active",()=> { main.HideToTray(); Assert(!main.IsVisible && controller.Window is {IsVisible:true}); main.Show(); });
            check("L2 dismiss does not complete task or re-show same fixed slot",()=> { controller.Window!.Close(); controller.Tick(); Assert(controller.Window is null && service.LoadAll().Single(t=>t.Id==c.Id).State==TaskState.Ready); });
            check("L2 later fixed slot reminds unhandled task but skips checked and completed ones",()=> { now=new(2026,10,26,14,0,0,TimeSpan.Zero); controller.Tick(); Assert(controller.Window?.TaskCount==1 && presented==2); });
            check("L2 paused task is removed from an already visible reminder",()=> { service.TogglePause(service.LoadAll().Single(t=>t.Id==c.Id)); main.ReloadTasks(); Assert(controller.Window is null); });
            var d=service.Create(new("桌面可用检查（测试）",TaskKind.WaitingForCondition,Times:"14:01"));
            check("L2 unavailable desktop does not consume a due slot",()=>
            {
                available=false; now=now.AddMinutes(1); controller.Tick(); Assert(controller.Window is null);
                available=true; now=now.AddSeconds(2); controller.Tick(); Assert(controller.Window?.TaskCount==1); controller.Window!.Close();
            });
            check("L2 next-day schedule resets today's check label and reminder eligibility",()=>
            {
                now=new(2026,10,27,9,0,0,TimeSpan.Zero); controller.Tick();
                Assert(controller.Window?.TaskCount==1);
                var cards=((ItemsControl)main.FindName("TaskList")).Items.Cast<TaskCard>(); Assert(!cards.Single(x=>x.Item.Id==a.Id).CheckedToday);
            });
            check("L2 stale reminder action cannot overwrite an edited task",()=>
            {
                var stale=service.LoadAll().Single(t=>t.Id==a.Id);
                service.Edit(stale,new("较新标题（测试）",TaskKind.WaitingForCondition,"https://example.org/a",Times:"09:00, 14:00"));
                SmokeRunner.Invoke(Action(a.Id,"今日已检查，尚未开放"));
                Assert(service.Repository.LoadChecks(a.Id).Count==1 && controller.Window is not null && ((FrameworkElement)controller.Window.FindName("ErrorLabel")).Visibility==Visibility.Visible);
            });
        }
        finally { main.Close(); }

        check("L2 logically incomplete schema shows error and cannot start reminders",()=>
        {
            var path=Path.Combine(directory,"incomplete-reminder.db"); var writer=new TaskService(new TaskRepository(path),()=>now); writer.Initialize(); writer.Create(new("缺失字段（测试）",TaskKind.Todo));
            using(var connection=new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder {DataSource=path,Pooling=false}.ToString()))
            { connection.Open(); using var command=connection.CreateCommand(); command.CommandText="ALTER TABLE tasks DROP COLUMN notes;"; command.ExecuteNonQuery(); }
            var window=new MainWindow(new SettingsStore(Path.Combine(directory,"incomplete-ui.json")),new TaskService(new TaskRepository(path))) {AllowClose=true};
            try { var engine=window.EnableReminders(()=>now,TimeZoneInfo.Utc,()=>true); Assert(((FrameworkElement)window.FindName("DataErrorPanel")).Visibility==Visibility.Visible && !((Button)window.FindName("NewTaskButton")).IsEnabled && engine.Window is null); }
            finally {window.Close();}
        });

        check("L2 failed reminder save stays visible without false acknowledgment",()=>
        {
            now=new(2026,10,26,8,59,0,TimeSpan.Zero);
            var path=Path.Combine(directory,"readonly-reminder.db");
            var writer=new TaskService(new TaskRepository(path),()=>now); writer.Initialize(); var task=writer.Create(new("只读保存失败（测试）",TaskKind.WaitingForCondition,Times:"09:00"));
            var dueAt=now.AddMinutes(1); var fixture=writer.ReminderSnapshot(new(2026,10,26));
            writer.RecordDelivery(ReminderPlanner.Due(fixture,dueAt,now,TimeZoneInfo.Utc).Single(),null,dueAt,TimeZoneInfo.Utc); now=dueAt;
            var reader=new TaskService(new TaskRepository(path,true),()=>now);
            var window=new MainWindow(new SettingsStore(Path.Combine(directory,"readonly-reminder-ui.json")),reader) {AllowClose=true,ShowActivated=false,ShowInTaskbar=false}; window.Show();
            var readController=window.EnableReminders(()=>now,TimeZoneInfo.Utc,()=>true,_=>throw new Win32Exception("controlled"));
            try
            {
                now=now.AddMinutes(1); readController.Tick(); readController.Act(task,ReminderAction.CheckedWaiting);
                Assert(readController.Window is {IsVisible:true,TaskCount:1} && reader.Repository.LoadChecks(task.Id).Count==0 && ((FrameworkElement)readController.Window.FindName("ErrorLabel")).Visibility==Visibility.Visible);
                readController.Act(task with {Url="https://example.org"},ReminderAction.OpenLink);
                Assert(((TextBlock)readController.Window!.FindName("ErrorLabel")).Text.Contains("无法打开平台") && reader.LoadAll().Single()==task);
            }
            finally { window.Close(); }
        });
    }
}
