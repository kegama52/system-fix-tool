using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TreasuryFixTool.Notifications;

using System.Windows.Media;
using System.Windows.Threading;
using TreasuryFixTool.Notifications;

namespace TreasuryFixTool.SystemTray
{
    /// <summary>
    /// Manages an in-app system-tray-like panel for WPF (no WinForms dependency).
    /// Provides a banner-style notification area that can be docked inside any Window.
    /// </summary>
    public class TrayManager : IDisposable
    {
        private readonly Window       _mainWindow;
        private readonly Border       _trayPanel;
        private readonly TextBlock    _statusText;
        private readonly TextBlock    _iconText;
        private readonly DispatcherTimer _pulseTimer;
        private bool _disposed;

        public string Status
        {
            get => _statusText.Text;
            set => _statusText.Text = value;
        }

        public TrayManager(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

            // Outer tray panel: docked to bottom-right of the window
            _trayPanel = new Border
            {
                Background  = new SolidColorBrush(Color.FromRgb(0x1F, 0x49, 0x7D)),
                CornerRadius = new CornerRadius(8),
                Padding     = new Thickness(14, 8, 14, 8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x6E, 0xA5)),
                BorderThickness = new BorderThickness(1)
            };

            // Inner horizontal stack: icon + label
            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            _iconText = new TextBlock
            {
                Text             = "🔧",
                FontSize         = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin           = new Thickness(0, 0, 8, 0)
            };

            _statusText = new TextBlock
            {
                Text         = "TreasuryFixTool — Running",
                Foreground   = Brushes.White,
                FontSize     = 13,
                FontWeight   = FontWeights.SemiBold,
                Cursor       = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            _statusText.MouseLeftButtonUp += (_, _) => ToggleMainWindow();

            panel.Children.Add(_iconText);
            panel.Children.Add(_statusText);

            // Right-click context menu
            var cm = new ContextMenu();
            var showItem = new MenuItem { Header = "Show Dashboard" };
            showItem.Click += (_, _) => ShowMainWindow();
            var scanItem = new MenuItem { Header = "Run Full Scan" };
            scanItem.Click += (_, _) => TriggerBackgroundScan();
            var exitItem = new MenuItem { Header = "Exit" };
            exitItem.Click += (_, _) => ExitApplication();
            cm.Items.Add(showItem);
            cm.Items.Add(scanItem);
            cm.Items.Add(exitItem);
            _trayPanel.ContextMenu = cm;

            _trayPanel.Child = panel;

            // Pulse timer: blinks the tray background when an alert is active
            _pulseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _pulseTimer.Tick += (_, _) => PulseTray();
        }

        /// <summary>
        /// Adds the tray panel to the specified parent grid (bottom row).
        /// </summary>
        public void AttachTo(Grid? hostGrid)
        {
            if (hostGrid == null) return;
            hostGrid.Children.Add(_trayPanel);
            Grid.SetRow    (_trayPanel,
                hostGrid.RowDefinitions.Count > 0
                    ? hostGrid.RowDefinitions.Count - 1
                    : 0);
            Grid.SetColumn(_trayPanel, 0);
            Grid.SetColumnSpan(_trayPanel, hostGrid.ColumnDefinitions.Count > 0 ? hostGrid.ColumnDefinitions.Count : 1);
            _trayPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        /// <summary>
        /// Call this when a critical issue is detected so the user notices the tray.
        /// </summary>
        public void Alert(string message)
        {
            Status  = message;
            _trayPanel.Background = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));
            _pulseTimer.Start();
            ToastManager.ShowToast(
                "TreasuryFixTool",
                message,
                timeoutMs: 15000,
                iconType: ToastManager.ToolTipIcon.Error);
        }

        /// <summary>
        /// Clears the alert state — tray returns to normal colour.
        /// </summary>
        public void ClearAlert()
        {
            Status       = "TreasuryFixTool — Running";
            _pulseTimer.Stop();
            _trayPanel.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x49, 0x7D));
        }

        private void PulseTray()
        {
            var baseColor = Color.FromRgb(0x1F, 0x49, 0x7D);
            var now       = DateTime.Now;
            _trayPanel.Background = new SolidColorBrush(
                (now.Second % 2 == 0)
                    ? Color.FromRgb(0xC0, 0x39, 0x2B)
                    : baseColor);
        }

        private void ToggleMainWindow()
        {
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }
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
            ToastManager.ShowToast("TreasuryFixTool", "Full system scan initiated…", 5000);
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
}
