using System.ComponentModel;
using System.Drawing;
using System.Windows;
using ChildScreenTime.Windows.Services;
using Forms = System.Windows.Forms;

namespace ChildScreenTime.Windows.Views;

public partial class MainWindow : Window, IDisposable
{
    private readonly AppServices services;
    private readonly Forms.NotifyIcon trayIcon;
    private readonly OverlayManager overlayManager;
    private readonly bool serviceManaged;
    private bool allowExit;
    private bool disposed;

    public MainWindow(AppServices services, bool serviceManaged = false)
    {
        this.services = services;
        this.serviceManaged = serviceManaged;
        InitializeComponent();

        overlayManager = new OverlayManager(services);
        trayIcon = BuildTrayIcon();
        services.Monitoring.StatusChanged += Monitoring_StatusChanged;
        UpdateStatus(services.Monitoring.GetCurrentStatus());
    }

    private Forms.NotifyIcon BuildTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Child Screen Time", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("Parent controls", null, (_, _) => Dispatcher.Invoke(OpenParentControls));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(
            serviceManaged ? "Restart monitoring agent (parent password required)" : "Exit (parent password required)",
            null,
            (_, _) => Dispatcher.Invoke(RequestExit));

        var icon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "Child Screen Time — monitoring active",
            Visible = true,
            ContextMenuStrip = menu
        };
        icon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
        return icon;
    }

    private void Monitoring_StatusChanged(object? sender, MonitoringStatus status) =>
        Dispatcher.InvokeAsync(() => UpdateStatus(status));

    private void UpdateStatus(MonitoringStatus status)
    {
        CurrentAppText.Text = status.CurrentApplication;
        var usedMinutes = (int)Math.Floor(status.ActiveSecondsToday / 60d);
        UsedTimeText.Text = $"{usedMinutes} minute(s) used today";
        RemainingTimeText.Text = status.Decision.IsBlocked ? "0 min" : $"{status.Decision.RemainingMinutes} min";

        var monitoringEnabled = services.Settings.GetSnapshot().MonitoringEnabled;
        StatusText.Text = monitoringEnabled ? "Monitoring active" : "Monitoring paused";
        StatusBadge.Background = monitoringEnabled
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 247, 238))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 244, 229));

        trayIcon.Text = status.Decision.IsBlocked
            ? "Child Screen Time — access restricted"
            : $"Child Screen Time — {status.Decision.RemainingMinutes} min remaining";
        overlayManager.Update(status.Decision);
    }

    private void ParentControls_Click(object sender, RoutedEventArgs e) => OpenParentControls();

    private void OpenParentControls()
    {
        var passwordDialog = new PasswordDialog(services) { Owner = IsVisible ? this : null };
        if (passwordDialog.ShowDialog() == true)
        {
            var parentWindow = new ParentWindow(services) { Owner = IsVisible ? this : null };
            parentWindow.ShowDialog();
            UpdateStatus(services.Monitoring.GetCurrentStatus());
        }
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void RequestExit()
    {
        var passwordDialog = new PasswordDialog(services) { Owner = IsVisible ? this : null };
        if (passwordDialog.ShowDialog() != true)
        {
            return;
        }

        services.Audit.Add(
            serviceManaged ? "agent_restart" : "agent_exit",
            serviceManaged
                ? "A parent requested a monitoring-agent restart. The Windows service will relaunch it."
                : "A parent stopped the monitoring agent.");
        allowExit = true;
        trayIcon.Visible = false;
        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!allowExit)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        services.Monitoring.StatusChanged -= Monitoring_StatusChanged;
        overlayManager.Dispose();
        trayIcon.Dispose();
        disposed = true;
        GC.SuppressFinalize(this);
    }
}
