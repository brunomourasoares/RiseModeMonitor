using WinForms = System.Windows.Forms;

namespace RiseModeMonitor.Services;

/// <summary>
/// System tray icon with an Open / Exit menu. Uses the WinForms NotifyIcon, which is
/// the most stable option for WPF apps.
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;

    public event EventHandler? OpenRequested;

    public event EventHandler? ExitRequested;

    public TrayService()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _icon = new WinForms.NotifyIcon
        {
            // TODO: replace with the Rise Mode brand icon (.ico) and also set <ApplicationIcon>.
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Rise Mode Monitor",
            ContextMenuStrip = menu,
            Visible = true
        };

        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
                OpenRequested?.Invoke(this, EventArgs.Empty);
        };
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
