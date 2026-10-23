using System.Drawing;
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
        _menu.Items.Add("打开日守", null, (_, _) => show());
        _menu.Items.Add("收起到托盘", null, (_, _) => hide());
        _menu.Items.Add("预览提醒（演示）", null, (_, _) => preview());
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("退出日守", null, (_, _) => exit());
        _icon = new Forms.NotifyIcon { Icon = _drawing, Text = "DayKeep 日守 · 持续跟进（L5）", ContextMenuStrip = _menu, Visible = true };
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) show(); };
    }

    public void Dispose() { _icon.Visible = false; _icon.Dispose(); _menu.Dispose(); _drawing.Dispose(); }
}
