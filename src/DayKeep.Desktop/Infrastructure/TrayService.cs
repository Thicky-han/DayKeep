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
        // The original DayKeep mark is a small sunrise in a rounded green tile.
        using var bitmap = new Bitmap(32, 32);
        using (var graphic = Graphics.FromImage(bitmap))
        {
            graphic.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphic.Clear(Color.Transparent);
            using var green = new SolidBrush(Color.FromArgb(52, 117, 104));
            using var cream = new SolidBrush(Color.FromArgb(249, 244, 217));
            using var pen = new Pen(Color.FromArgb(249, 244, 217), 2.3f);
            graphic.FillEllipse(green, 1, 1, 30, 30);
            graphic.FillEllipse(cream, 10, 10, 12, 12);
            graphic.FillRectangle(green, 7, 18, 19, 7);
            graphic.DrawLine(pen, 7, 20, 25, 20);
            graphic.DrawLine(pen, 16, 5, 16, 8);
        }
        var handle = bitmap.GetHicon();
        try { using var temporary = Icon.FromHandle(handle); _drawing = (Icon)temporary.Clone(); }
        finally { DestroyIcon(handle); }
        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add("打开日守", null, (_, _) => show());
        _menu.Items.Add("收起到托盘", null, (_, _) => hide());
        _menu.Items.Add("预览提醒（演示）", null, (_, _) => preview());
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("退出日守", null, (_, _) => exit());
        _icon = new Forms.NotifyIcon { Icon = _drawing, Text = "DayKeep 日守 · 本地任务（L1）", ContextMenuStrip = _menu, Visible = true };
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) show(); };
    }

    public void Dispose() { _icon.Visible = false; _icon.Dispose(); _menu.Dispose(); _drawing.Dispose(); }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
