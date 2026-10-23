using System.Drawing;
using DayKeep.Localization;
using Forms = System.Windows.Forms;

namespace DayKeep.Infrastructure;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Icon _drawing;
    private readonly Forms.ContextMenuStrip _menu;

    public TrayService(Action show, Action hide, Action preview, Action exit)
    {
        // Use the same embedded mark as the executable and window, including its small-size frames.
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/daykeep.ico"));
        using (resource.Stream) _drawing = new Icon(resource.Stream, new Size(32, 32));
        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add("", null, (_, _) => show());
        _menu.Items.Add("", null, (_, _) => hide());
        _menu.Items.Add("", null, (_, _) => preview());
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("", null, (_, _) => exit());
        _icon = new Forms.NotifyIcon { Icon = _drawing, ContextMenuStrip = _menu, Visible = true };
        RefreshLanguage(null,EventArgs.Empty);
        Localizer.Changed += RefreshLanguage;
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) show(); };
    }

    private void RefreshLanguage(object? sender,EventArgs e)
    {
        _menu.Items[0].Text=Localizer.T("打开日守"); _menu.Items[1].Text=Localizer.T("收起到托盘");
        _menu.Items[2].Text=Localizer.T("预览提醒（演示）"); _menu.Items[4].Text=Localizer.T("退出日守");
        _icon.Text=Localizer.T("DayKeep · 持续跟进");
    }
    public void Dispose() { Localizer.Changed -= RefreshLanguage; _icon.Visible = false; _icon.Dispose(); _menu.Dispose(); _drawing.Dispose(); }
}
