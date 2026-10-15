using System.Drawing;
using System.Drawing.Drawing2D;
using NoctaliaWin.Core.Abstractions;
using WinForms = System.Windows.Forms;

namespace NoctaliaWin.Windows;

/// <summary>
/// Tray presence via the documented shell notification icon API (through
/// NotifyIcon, which is part of the Windows Desktop runtime we already reference).
/// Left-click reopens the launcher; the context menu offers Settings and Exit, and exiting
/// never touches the taskbar or any other shell process.
/// </summary>
public sealed class TrayService : ITrayService
{
    // The documented cap for a notification icon tooltip.
    internal const int MaxTooltipLength = 63;

    private readonly WinForms.NotifyIcon _icon;

    public event Action? IconActivated;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public TrayService()
    {
        var menu = new WinForms.ContextMenuStrip();
        var settingsItem = new WinForms.ToolStripMenuItem("Settings");
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke();
        var exitItem = new WinForms.ToolStripMenuItem("Exit Noctalia");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(settingsItem);
        menu.Items.Add(exitItem);

        _icon = new WinForms.NotifyIcon
        {
            Icon = BuildIcon(),
            Visible = true,
            Text = ClampTooltip("Noctalia"),
            ContextMenuStrip = menu,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left) IconActivated?.Invoke();
        };
    }

    public void SetTooltip(string text) => _icon.Text = ClampTooltip(text);

    internal static string ClampTooltip(string text) =>
        text.Length <= MaxTooltipLength ? text : text[..MaxTooltipLength];

    /// Drawn here so the app ships no third-party icon asset: the tray glyph is a
    /// crescent, which is Noctalia's motif, rendered without reusing its artwork.
    private static Icon BuildIcon()
    {
        using var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var outer = new GraphicsPath();
        outer.AddEllipse(1, 1, 14, 14);
        using var carved = new GraphicsPath();
        carved.AddEllipse(5, -2, 14, 14);

        using var crescent = new Region(outer);
        crescent.Exclude(carved);

        graphics.SetClip(crescent, CombineMode.Replace);
        using var brush = new SolidBrush(Color.FromArgb(230, 214, 255));
        graphics.FillEllipse(brush, 1, 1, 14, 14);
        graphics.ResetClip();

        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
