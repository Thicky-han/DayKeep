using System.IO;
using System.Windows;
using System.Windows.Controls;
using DayKeep.Core;
using DayKeep.Models;
using DayKeep.Localization;

namespace DayKeep.Infrastructure;

internal static class RepeatUiChecks
{
    public static void Run(string directory, Action<string,Action> check)
    {
        var now=new DateTimeOffset(2026,10,26,8,59,58,TimeSpan.Zero);
        var service=new TaskService(new TaskRepository(Path.Combine(directory,"repeat-ui.db")),()=>now); service.Initialize();
        var today=service.Create(new("专利缴费 · 今天截止（测试）",TaskKind.Todo,DueDate:"2026-10-26"));
        var overdue=service.Create(new("查看开放情况 · 已逾期（测试）",TaskKind.WaitingForCondition,DueDate:"2026-10-25"));
        MainWindow Open() { var window=new MainWindow(new SettingsStore(Path.Combine(directory,"repeat-ui.json")),service) {AllowClose=true,ShowActivated=false,ShowInTaskbar=false,Left=-20000,Top=-20000,WindowStartupLocation=WindowStartupLocation.Manual}; window.Show(); return window; }
        var main=Open(); var controller=main.EnableReminders(()=>now,TimeZoneInfo.Utc,()=>true);
        void At(int hour,int minute) { now=new(2026,10,26,hour,minute,0,TimeSpan.Zero); controller.Tick(); }
        Button Action(Guid id,string label) { var window=controller.Window ?? throw new InvalidOperationException("Reminder missing"); window.UpdateLayout(); return SmokeRunner.Descendants<Button>(window).First(b=>b.Tag is TaskItem t && t.Id==id && b.Content?.ToString()==label && b.Visibility==Visibility.Visible); }
        static void Assert(bool value) { if(!value) throw new InvalidOperationException("L3 UI expectation failed"); }
        try
        {
            At(9,0);
            check("L3 due cards prioritize overdue then today with clear date labels",()=> { var cards=((ItemsControl)controller.Window!.FindName("DueTaskList")).Items.Cast<TaskCard>().ToArray(); Assert(cards[0].Item.Id==overdue.Id && cards[0].Summary.Contains("已逾期 1 天") && cards[1].Summary.Contains("今天截止")); });
            if(controller.Window is { } due) SmokeRunner.Render(due,Path.Combine(directory,"daykeep-l3-deadlines.png"));
            check("L3 10-minute button postpones only its own merged task",()=> { SmokeRunner.Invoke(Action(today.Id,"10 分钟后")); Assert(controller.Window?.TaskCount==1 && service.ReminderSnapshot(new(2026,10,26)).Progress.Single(p=>p.TaskId==today.Id).SnoozedUntil==now.AddMinutes(10)); });
            check("L3 main card exposes the saved postponement in its reminder time zone",()=> { var cards=((ItemsControl)main.FindName("TaskList")).Items.Cast<TaskCard>(); var expected=new DateTimeOffset(2026,10,26,9,10,0,TimeSpan.Zero).ToString("g",Localizer.Culture); Assert(cards.Single(c=>c.Item.Id==today.Id).Followup==Localizer.T("稍后提醒至 {0}",expected)); });
            SmokeRunner.Render(main,Path.Combine(directory,"daykeep-l3-snoozed.png"));
            main.SetCompact(true); SmokeRunner.Render(main,Path.Combine(directory,"daykeep-l3-compact.png")); main.SetCompact(false);
            check("L3 today's check closes final waiting card and cancels extras",()=> { SmokeRunner.Invoke(Action(overdue.Id,"今日已检查，尚未开放")); Assert(controller.Window is null); });
            At(9,9); check("L3 postponed reminder stays hidden before expiry",()=>Assert(controller.Window is null));
            At(9,10); check("L3 expiry creates a real reminder for postponed task",()=>Assert(controller.Window?.TaskCount==1));
            check("L3 dismiss then 30 minutes produces another real window",()=> { controller.Window!.Close(); At(9,39); Assert(controller.Window is null); At(9,40); Assert(controller.Window?.TaskCount==1 && service.ReminderSnapshot(new(2026,10,26)).Progress.Single(p=>p.TaskId==today.Id).ExtraCount==1); });
            check("L3 30-minute postponement preserves the daily used count",()=> { SmokeRunner.Invoke(Action(today.Id,"30 分钟后")); Assert(controller.Window is null && service.ReminderSnapshot(new(2026,10,26)).Progress.Single(p=>p.TaskId==today.Id).ExtraCount==1); });
            check("L3 reopened controller restores future postponement without early popup",()=> { main.Close(); now=now.AddMinutes(1); main=Open(); controller=main.EnableReminders(()=>now,TimeZoneInfo.Utc,()=>true); Assert(controller.Window is null); });
            At(10,10); check("L3 saved postponement displays after reopening",()=>Assert(controller.Window?.TaskCount==1));
            check("L3 daily cap stops automatic windows after three extras",()=> { controller.Window!.Close(); At(10,40); Assert(controller.Window?.TaskCount==1); controller.Window!.Close(); At(11,10); Assert(controller.Window?.TaskCount==1); controller.Window!.Close(); At(11,40); Assert(controller.Window is null && service.ReminderSnapshot(new(2026,10,26)).Progress.Single(p=>p.TaskId==today.Id).ExtraCount==3); });
            check("L3 next day restores check eligibility and daily repeat budget",()=> { now=new(2026,10,27,9,0,0,TimeSpan.Zero); controller.Tick(); Assert(controller.Window?.TaskCount==2 && service.ReminderSnapshot(new(2026,10,27)).Progress.All(p=>p.ExtraCount==0)); });
            check("L3 one-hour button changes saved time without completion",()=> { SmokeRunner.Invoke(Action(today.Id,"1 小时后")); Assert(controller.Window?.TaskCount==1 && service.LoadAll().Single(t=>t.Id==today.Id).State==TaskState.Ready && service.ReminderSnapshot(new(2026,10,27)).Progress.Single(p=>p.TaskId==today.Id).SnoozedUntil==now.AddHours(1)); });
        }
        finally { main.Close(); }
        check("L3 an already visible deadline label updates at local midnight",()=>
        {
            now=new(2026,10,26,8,59,0,TimeSpan.Zero);
            var midnight=new TaskService(new TaskRepository(Path.Combine(directory,"deadline-midnight.db")),()=>now); midnight.Initialize();
            midnight.Create(new("午夜截止（测试）",TaskKind.Todo,DueDate:"2026-10-26",MaxExtraReminders:0));
            using var engine=new ReminderController(midnight,()=>{},()=>now,TimeZoneInfo.Utc,()=>true); engine.Start();
            now=now.AddMinutes(1); engine.Tick(); var original=engine.Window;
            now=new(2026,10,27,0,0,0,TimeSpan.Zero); engine.Tick();
            Assert(ReferenceEquals(original,engine.Window) && ((ItemsControl)engine.Window!.FindName("DueTaskList")).Items.Cast<TaskCard>().Single().Summary.Contains("已逾期 1 天"));
        });
    }
}
