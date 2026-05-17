using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;
using TreasuryFixTool.Diagnostics;
using TreasuryFixTool.Fixes;
using TreasuryFixTool.Notifications;
using TreasuryFixTool.SystemTray;
using TreasuryFixTool.Views;
using TreasuryFixTool.Updates;

namespace TreasuryFixTool.Views
{
    public partial class HealthDashboard : UserControl
    {
        private readonly DiagnosticEngine _diagnosticEngine;
        private readonly FixEngine _fixEngine;
        private readonly TrayManager? _trayManager;
        private readonly DispatcherTimer _refreshTimer;
        private bool _isScanning;

        private static readonly Dictionary<CheckStatus, (Brush Status, Brush Badge)> Theme = new()
        {
            [CheckStatus.Healthy] = (new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)), new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))),
            [CheckStatus.Warning] = (new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12)), new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22))),
            [CheckStatus.Critical] = (new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)), new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B))),
            [CheckStatus.Error] = (new SolidColorBrush(Color.FromRgb(0x95, 0xA5, 0xA6)), new SolidColorBrush(Color.FromRgb(0x7F, 0x8C, 0x8D))),
        };

        public HealthDashboard()
        {
            InitializeComponent();
            _diagnosticEngine = new DiagnosticEngine();
            _fixEngine = new FixEngine();
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _refreshTimer.Tick += async (_, _) => await RunScanAsync();

            _ = RunScanAsync();
        }

        public HealthDashboard(TrayManager trayManager) : this()
        {
            _trayManager = trayManager;
        }

        private async void RunScan_Click(object sender, RoutedEventArgs e) => await RunScanAsync();

        public async Task RunScanAsync()
        {
            if (_isScanning) return;
            _isScanning = true;
            StatusBarText.Text = "Running diagnostics…";

            var results = _diagnosticEngine.RunAllChecks();

            ChecksHost.ItemsSource = results;
            ChecksHost.Items.Refresh();

            StatusBarText.Text = $"Scan complete — {results.Count} checks performed at {DateTime.Now:HH:mm:ss}.";

            foreach (var result in results)
            {
                ApplyTheme(result);

                bool needsFix = result.Status is CheckStatus.Critical or CheckStatus.Warning;
                var fixBtn = (Button)FindCardButton(result.CheckName);
                fixBtn?.Dispatcher.Invoke(() =>
                {
                    if (fixBtn.Visibility != Visibility.Visible && needsFix)
                        fixBtn.Visibility = Visibility.Visible;
                    if (needsFix && _fixEngine.GetFixNameForCheck(result.CheckName) is { } fixName)
                        fixBtn.Tag = fixName;
                });
            }

            if (_trayManager != null)
            {
                var criticalCount = results.FindAll(r => r.Status == CheckStatus.Critical).Count;
                if (criticalCount == 0)
                    _trayManager.ClearAlert();
            }

            _isScanning = false;
        }

        /// <summary>
        /// Called by the "Fix Now" button on each card.  Applies the fix for the failing check.
        /// </summary>
        private async void FixButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string checkName)
                return;

            var fixBtn = btn;
            var card   = FindCardControl(fixBtn, out var msgBlock, out var progBar, out var badge);

            if (card is null) return;

            progBar!.Visibility   = Visibility.Visible;
            fixBtn.IsEnabled      = false;
            fixBtn.Content        = "Fixing…";
            StatusBarText.Text    = $"Fixing: {checkName}…";

            FixResult fixResult;
            try
            {
                fixResult = await _fixEngine.ExecuteFixAsync(checkName);
            }
            catch (Exception ex)
            {
                fixResult = new FixResult { Success = false, Message = ex.Message };
            }

            progBar.Visibility    = Visibility.Collapsed;
            fixBtn.IsEnabled      = true;
            fixBtn.Content        = fixResult.Success ? "Fix Applied ✓" : "Fix Failed";

            if (fixResult.Success)
            {
                ToastManager.ShowToast("TreasuryFixTool",
                    $"{checkName}: {fixResult.Message}", 6000, ToastIcon.Info);

                await RefreshCheckAsync(checkName);
                // Hide the Fix Now button after a successful refresh
                await Dispatcher.InvokeAsync(() =>
                {
                    if (FindCardButton(checkName) is { } b) b.Visibility = Visibility.Collapsed;
                });
            }
            else
            {
                ToastManager.ShowToast("TreasuryFixTool",
                    $"Fix failed: {fixResult.Message}", 8000, ToastIcon.Error);

                if (_trayManager != null)
                    _trayManager.Alert($"Auto-fix failed for {checkName}. Manual assistance required.");

                await Dispatcher.InvokeAsync(() =>
                {
                    if (FindCardButton(checkName) is { } b)
                    {
                        b.Content    = "Escalate…";
                        b.Background = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));
                        b.Tag        = checkName;
                        b.Click     -= FixButton_Click;
                        b.Click     += EscalateButton_Click;
                    }
                });
            }

            StatusBarText.Text = fixResult.Success
                ? $"Fixed: {checkName}"
                : $"Fix failed: {checkName} — see details below.";
        }

        private async void EscalateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string checkName }) return;

            StatusBarText.Text = $"Escalating issue: {checkName}…";
            ToastManager.ShowToast("TreasuryFixTool",
                $"Escalating issue for ICTSU: {checkName}", 8000, ToastIcon.Error);

            // Navigate to the Escalation tab
            if (Window.GetWindow(this) is MainWindow mw && mw.MainTabControl is TabControl tc)
            {
                if (tc.Items.Count > 1)
                    tc.SelectedIndex = 1;
            }
        }

        private void ApplyTheme(CheckResult result)
        {
            if (!Theme.TryGetValue(result.Status, out var theme)) return;

            foreach (var card in FindVisualChildren<Border>(this))
            {
                if (card.DataContext == result)
                {
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var child in FindVisualChildren<Border>(card))
                        {
                            if (child.Name == "StatusBadge")
                                child.Background = theme.Badge;
                            foreach (var tb in FindVisualChildren<TextBlock>(child))
                            {
                                if (tb.Name == "StatusLabel")  tb.Foreground = Brushes.White;
                                if (tb.Name == "NameBlock")    tb.Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                                if (tb.Name == "MessageBlock") tb.Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
                            }
                        }
                        foreach (var tb in FindVisualChildren<TextBlock>(card))
                        {
                            if (tb.Name == "IconBlock")
                                tb.Text = result.Status switch
                                {
                                    CheckStatus.Healthy  => "✅",
                                    CheckStatus.Warning  => "⚠️",
                                    CheckStatus.Critical => "🚨",
                                    _                    => "❓"
                                };
                        }
                    });
                    break;
                }
            }
        }

        private static Button? FindCardButton(string checkName)
        {
            var page = Application.Current?.MainWindow?.FindName("ChecksHost") as ItemsControl;
            return page?.Items?.Cast<CheckResult>()
                       .Select((r, i) => (Result: r, Index: i))
                       .FirstOrDefault(p => p.Result.CheckName == checkName).Index
                is int idx && page.ItemContainerGenerator.ContainerFromIndex(idx) is ContentPresenter cp
                ? (Button)FindChildByName(cp, "FixButton")
                : null;
        }

        private static DependencyObject? FindChildByName(DependencyObject parent, string name)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Name == name)
                    return child;
                var nested = FindChildByName(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        private Border? FindCardControl(Button fixBtn, out TextBlock? msgBlock, out ProgressBar? progBar, out Border? badge)
        {
            msgBlock = null; progBar = null; badge = null;
            var parent = VisualTreeHelper.GetParent(fixBtn);
            while (parent != null)
            {
                if (parent is Border { Name: "cardBorder" or "StatusBadge" })
                    return (Border)parent;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj)
            where T : DependencyObject
        {
            if (depObj == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                    yield return t;
                foreach (var other in FindVisualChildren<T>(child))
                    yield return other;
            }
        }

        private async Task RefreshCheckAsync(string checkName)
        {
            await Task.Delay(1500);
            await RunScanAsync();
        }
    }
}