using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TreasuryFixTool.Diagnostics;
using TreasuryFixTool.Fixes;
using TreasuryFixTool.Infrastructure.Config;
using TreasuryFixTool.Infrastructure.Escalation;
using TreasuryFixTool.Infrastructure.Logging;
using TreasuryFixTool.Infrastructure.Storage;
using TreasuryFixTool.Monitoring;
using TreasuryFixTool.Notifications;
using TreasuryFixTool.SystemTray;
using TreasuryFixTool.Views;
using TreasuryFixTool.Updates;

namespace TreasuryFixTool;

public partial class MainWindow : Window
{
    private readonly FileLogger          _logger;
    private readonly AppConfig           _config;
    private readonly UpdateManager       _updateMgr;
    private readonly UsbUpdateHandler?   _usbHandler;
    private TrayManager?        _trayManager;
    private BackgroundMonitor?  _monitor;

    public MainWindow()
    {
        InitializeComponent();

        _logger    = new(Path.Combine(DataPaths.LogsDirectory,
                         $"TreasuryFix_{DateTime.Now:yyyyMMdd_HHmmss}.log"));
        _config    = new AppConfig();
        _updateMgr = new UpdateManager();
        _usbHandler= new UsbUpdateHandler();

        AppStatusBar.Text = $"TreasuryFixTool v1.0  |  {Environment.MachineName}  |  Offline Mode — National Treasury";

        AttachTrayIfInSilentMode();
    }

    private void AttachTrayIfInSilentMode()
    {
        string[] args = Environment.GetCommandLineArgs();
        bool silent   = Array.Exists(args, a
                            => a.Equals("/silent-start", StringComparison.OrdinalIgnoreCase));
        if (!silent) return;

        _trayManager = new TrayManager(this);

        // Attach tray banner inside the main window's content grid
        if (FindName("MainTabControl") is TabControl tc)
            _trayManager.AttachTo(tc.Parent as Grid);

        _monitor = new BackgroundMonitor(new DiagnosticEngine(), _trayManager);
        _monitor.Start();

        Task.Run(async () =>
        {
            await Task.Delay(2000);
            Dispatcher.Invoke(() => _trayManager?.Alert("Background health check running…"));
        });

        Hide();
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl?.SelectedContent is HealthDashboard d)
            await d.RunScanAsync();
    }

    private void Escalate_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl is TabControl tc)
        {
            tc.SelectedIndex = 1;
            ToastManager.ShowToast("TreasuryFixTool", "Describe your issue, then click Generate Ticket.", 6000, ToastIcon.Info);
        }
    }
}
