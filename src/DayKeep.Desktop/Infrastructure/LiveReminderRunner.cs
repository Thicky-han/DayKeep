using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using DayKeep.Core;

namespace DayKeep.Infrastructure;

internal static class LiveReminderRunner
{
    public static void Run(App application, string directory)
    {
        Directory.CreateDirectory(directory);
        var localNow = DateTimeOffset.Now;
        var deadline = new DateTimeOffset(localNow.Year,localNow.Month,localNow.Day,localNow.Hour,localNow.Minute,0,localNow.Offset).AddMinutes(1);
        if (deadline-localNow < TimeSpan.FromSeconds(5)) deadline=deadline.AddMinutes(1);
        var service=new TaskService(new TaskRepository(Path.Combine(directory,"live-tasks.db")));
        service.Initialize();
        service.Create(new("真实时钟检查（合成测试）",TaskKind.WaitingForCondition,Times:deadline.ToString("HH:mm")));
        service.Create(new("真实时钟办理（合成测试）",TaskKind.Todo,Times:deadline.ToString("HH:mm")));
        var main=new MainWindow(new SettingsStore(Path.Combine(directory,"live-ui.json")),service)
        { AllowClose=true,ShowActivated=false,ShowInTaskbar=false,Left=-20000,Top=-20000,WindowStartupLocation=WindowStartupLocation.Manual };
        application.MainWindow=main;
        main.Show();
        var controller=main.EnableReminders();
        var timeout=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(200) };
        var finished=false;
        File.WriteAllText(Path.Combine(directory,"target.json"),JsonSerializer.Serialize(new { targetLocal=deadline,scope="Actual HH:mm schedule and production 1-second timer; isolated synthetic tasks" }));
        void Finish(bool passed,string? error,DueReminderWindow? reminder)
        {
            if(finished) return; finished=true; timeout.Stop();
            if(reminder is not null) SmokeRunner.Render(reminder,Path.Combine(directory,"live-reminder.png"));
            File.WriteAllText(Path.Combine(directory,"live-report.json"),JsonSerializer.Serialize(new
            {
                passed,error,targetLocal=deadline,shownAtLocal=reminder is not null ? DateTimeOffset.Now : (DateTimeOffset?)null,
                delaySeconds=reminder is not null ? (DateTimeOffset.Now-deadline).TotalSeconds : (double?)null,
                mergedTasks=reminder?.TaskCount,topmost=reminder?.Topmost,showActivated=reminder?.ShowActivated,
                preservedForeground=controller.LastShowPreservedFocus,desktopAvailable=DesktopAvailability.IsAvailable(),
                scope="One real clock observation, not a guarantee for every system, lock/resume or fullscreen condition"
            },new JsonSerializerOptions { WriteIndented=true }));
            application.Dispatcher.BeginInvoke(new Action(()=> { main.Close(); application.Shutdown(passed ? 0 : 1); }));
        }
        controller.Presented+=window =>
        {
            var delay=DateTimeOffset.Now-deadline;
            var passed=delay >= TimeSpan.Zero && delay <= TimeSpan.FromSeconds(10) && window.TaskCount==2 && window.Topmost && !window.ShowActivated && controller.LastShowPreservedFocus==true;
            Finish(passed,passed ? null : "Real reminder did not meet timing, grouping or focus expectation",window);
        };
        timeout.Tick+=(_,_)=> { if(DateTimeOffset.Now>deadline.AddSeconds(12)) Finish(false,"No reminder was displayed in the expected window",null); };
        timeout.Start();
    }
}
