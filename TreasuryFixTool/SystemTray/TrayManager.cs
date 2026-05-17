using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TreasuryFixTool.Notifications;

namespace TreasuryFixTool.SystemTray;

/// <summary>
/// In-app system-tray panel — a coloured banner docked in the parent window that shows
/// background health status and posts toast notifications on alerts.
/// </summary>
public class TrayManager : IDisposable
{
    private readonly Window _mainWindow;
    private readonly Border _trayPanel;
    private readonly TextBlock _statusText;
    private readonly DispatcherTimer _pulseTimer;
    private bool _disposed;

    /// <summary>Gets or sets the status text shown in the tray panel.</summary>
    public string Status
    {
        get => _statusText.Text;
        set => _statusText.Text = value;
    }

    public TrayManager(Window mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

        _trayPanel = new Border
        {
            Background      = new SolidColorBrush(Color.FromRgb(0x1F, 0x49, 0x7D)),
            CornerRadius    = new CornerRadius(8),
            Padding         = new Thickness(16, 8, 16, 8),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(0x3A, 0x6E, 0xA5)),
            BorderThickness = new Thickness(1)
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        var iconText = new TextBlock
        {
            Text             = "🔧",
            FontSize         = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Margin           = new Thickness(0, 0, 8, 0)
        };

        _statusText = new TextBlock
        {
            Text            = "TreasuryFixTool — Running",
            Foreground      = Brushes.White,
            FontSize        = 13,
            FontWeight      = FontWeights.SemiBold,
            Cursor          = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };
        _statusText.MouseLeftButtonUp += (_, _) => ToggleMainWindow();

        panel.Children.Add(iconText);
        panel.Children.Add(_statusText);

        var cm = new ContextMenu();
        foreach (var (header, action) in new[]
        {
            ("Show Dashboard",   (Action)(() => ShowMainWindow())),
            ("Run Full Scan",    () => ToastManager.ShowToast("TreasuryFixTool", "Scanning…", 4000, ToastIcon.Info)),
            ("Exit",             ExitApplication)
        })
        {
            var mi = new MenuItem { Header = header };
            mi.Click += (_, _) => action();
            cm.Items.Add(mi);
        }
        _trayPanel.ContextMenu = cm;
        _trayPanel.Child = panel;

        _pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _pulseTimer.Tick += (_, _) => PulseTray();
    }

    /// <summary>Attach the tray banner to a host Grid (last row).</summary>
    public void AttachTo(Grid? hostGrid)
    {
        if (hostGrid == null) return;
        hostGrid.Children.Add(_trayPanel);
        int row = hostGrid.RowDefinitions.Count > 0
            ? hostGrid.RowDefinitions.Count - 1
            : 0;
        int colSpan = hostGrid.ColumnDefinitions.Count > 0
            ? hostGrid.ColumnDefinitions.Count
            : 1;
        Grid.SetRow(_trayPanel, row);
        Grid.SetColumn(_trayPanel, 0);
        Grid.SetColumnSpan(_trayPanel, colSpan);
        _trayPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    /// <summary>Mark an alert condition — red banner + toast to user.</summary>
    public void Alert(string message)
    {
        Status             = message;
        _trayPanel.Background = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));
        _pulseTimer.Start();
        ToastManager.ShowToast("TreasuryFixTool", message, 15000, ToastIcon.Error);
    }

    /// <summary>Clear any active alert — restores normal blue banner.</summary>
    public void ClearAlert()
    {
        Status       = "TreasuryFixTool — Running";
        _pulseTimer.Stop();
        _trayPanel.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x49, 0x7D));
    }

    private void PulseTray()
    {
        var baseColor = Color.FromRgb(0x1F, 0x49, 0x7D);
        _trayPanel.Background = new SolidColorBrush(
            DateTime.Now.Second % 2 == 0
                ? Color.FromRgb(0xC0, 0x39, 0x2B)
                : baseColor);
    }

    private void ToggleMainWindow()
    {
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    private void TriggerBackgroundScan()
    {
        ToastManager.ShowToast("TreasuryFixTool", "Full system scan initiated…", 5000, ToastIcon.Info);
    }

    private void ExitApplication()
    {
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _pulseTimer.Stop();
            _statusText.MouseLeftButtonUp -= (_, _) => ToggleMainWindow();
        }
        _disposed = true;
    }
}
