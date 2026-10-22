using RiseModeMonitor.Localization;
using WinForms = System.Windows.Forms;

namespace RiseModeMonitor.Services;

/// <summary>
/// System tray icon with an Open / Exit menu. Uses the WinForms NotifyIcon, which is
/// the most stable option for WPF apps. Menu texts follow the current language.
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly LocalizationService _loc;
    private readonly WinForms.ToolStripItem _openItem;
    private readonly WinForms.ToolStripItem _exitItem;

    public event EventHandler? OpenRequested;

    public event EventHandler? ExitRequested;

    public TrayService(LocalizationService loc)
    {
        _loc = loc;

        var menu = new WinForms.ContextMenuStrip();
        _openItem = menu.Items.Add(_loc["Tray_Open"], null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        _exitItem = menu.Items.Add(_loc["Tray_Exit"], null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

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

        _loc.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _openItem.Text = _loc["Tray_Open"];
        _exitItem.Text = _loc["Tray_Exit"];
    }

    public void Dispose()
    {
        _loc.LanguageChanged -= OnLanguageChanged;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
