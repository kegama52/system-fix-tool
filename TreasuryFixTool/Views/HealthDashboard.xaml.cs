using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
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
    /// <summary>
    /// Interaction logic for HealthDashboard.xaml
    /// Completely rewritten: metric card dashboard, escalation panel,
    /// real-time scan with progress, collapsible colour-coded log, export.
    /// </summary>
    public partial class HealthDashboard : UserControl
    {
        // ─── Services ──────────────────────────────────────────────────
        private readonly DiagnosticEngine          _diagnosticEngine;
        private readonly FixEngine                 _fixEngine;
        private readonly TrayManager?              _trayManager;
        private readonly DispatcherTimer           _refreshTimer;
        private readonly DispatcherTimer           _escalationAutoRefresh;
        private          bool                       _isScanning;
        private          DateTime                    _lastScanStart;
        private          DateTime?                   _lastScanTime;
        private          TimeSpan                    _appUptime;

        // ─── Scan log entry DTO ──────────────────────────────────────────
        private class ScanLogEntry
        {
            public DateTime Timestamp  { get; set; }
            public string   Status     { get; set; } = "";
            public string   Description{ get; set; } = "";
            public string   Duration   { get; set; } = "";
            public Brush    EntryBg    { get; set; } = Brushes.White;
            public Brush    EntryBorder{ get; set; } = Brushes.LightGray;
            public Brush    StatusBadgeBg { get; set; } = Brushes.Gray;
        }

        private static Brush GetLogEntryBg(string status) => status switch
        {
            "PASS"  => new SolidColorBrush(Color.FromRgb(0xF0, 0xFF, 0xF0)),
            "FAIL"  => new SolidColorBrush(Color.FromRgb(0xFF, 0xF0, 0xF0)),
            "WARN"  => new SolidColorBrush(Color.FromRgb(0xFF, 0xFB, 0xEB)),
            "ERROR" => new SolidColorBrush(Color.FromRgb(0xFF, 0xE8, 0xE8)),
            _       => Brushes.White
        };

        private static Brush GetLogEntryBorder(string status) => status switch
        {
            "PASS"  => new SolidColorBrush(Color.FromRgb(0xC8, 0xE6, 0xC9)),
            "FAIL"  => new SolidColorBrush(Color.FromRgb(0xFF, 0xCD, 0xD2)),
            "WARN"  => new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xB2)),
            "ERROR" => new SolidColorBrush(Color.FromRgb(0xFF, 0xAB, 0x91)),
            _       => Brushes.LightGray
        };

        private static Brush GetLogBadgeBg(string status) => status switch
        {
            "PASS"  => new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)),
            "FAIL"  => new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B)),
            "WARN"  => new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22)),
            "ERROR" => new SolidColorBrush(Color.FromRgb(0x95, 0xA5, 0xA6)),
            _       => Brushes.Gray
        };



        // ─── Scan log persistence ──────────────────────────────────────
        private readonly List<ScanLogEntry>         _allLogEntries = new();

        // ─── Escalation store ──────────────────────────────────────────
        private readonly List<EscalationItem>       _activeEscalations = new();

        // ─── Light / Dark theme brushes ────────────────────────────────
        private static readonly Brush HeaderBgLight  = new SolidColorBrush(Color.FromRgb(0x1F, 0x49, 0x7D));
        private static readonly Brush HeaderBgDark   = new SolidColorBrush(Color.FromRgb(0x0D, 0x1F, 0x3C));
        private static readonly Brush HeaderTxtLight = Brushes.White;
        private static readonly Brush HeaderTxtDark  = new SolidColorBrush(Color.FromRgb(0xBF, 0xD7, 0xED));
        private static readonly Brush PanelBgLight   = new SolidColorBrush(Color.FromRgb(0xF0, 0xF4, 0xF8));
        private static readonly Brush PanelBgDark    = new SolidColorBrush(Color.FromRgb(0x1A, 0x1E, 0x2E));
        private          bool                       _isDarkTheme;

        // ==================================================================
        //  Constructor
        // ==================================================================
        public HealthDashboard()
        {
            InitializeComponent();

            _diagnosticEngine = new DiagnosticEngine();
            _fixEngine        = new FixEngine();
            _refreshTimer     = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _refreshTimer.Tick += async (_, _) =>
            {
                if (!_isScanning && IsWindowVisible())
                    await RunFullScanAsync(silent: true);
            };

            _escalationAutoRefresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _escalationAutoRefresh.Tick += (_, _) => RefreshEscalationList();

            BuildMetricCards();
            ResetMetricCardsToLoading();

            _lastScanTime = DateTime.Now - TimeSpan.FromMinutes(5);
            _appUptime    = TimeSpan.FromMinutes(1);

            // Schedule escalation populating timer (staggered so it doesn't all appear at once)
            var escalateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
            escalateTimer.Tick += (_, _) =>
            {
                escalateTimer.Stop();
                PopulateInitialEscalations();
            };
            escalateTimer.Start();

            BottomStatusBar.Text = "TreasuryFixTool  |  Ready — National Treasury IT Support Unit";

            // Wire F5 / Ctrl+E shortcuts directly on the UserControl
            InputBindings.Add(new KeyBinding(ApplicationCommands.NotACommand, Key.F5, ModifierKeys.None)
            {
                Command = new RelayCommand(_ => _ = RunFullScanAsync())
            });
            InputBindings.Add(new KeyBinding(ApplicationCommands.NotACommand, Key.E, ModifierKeys.Control)
            {
                Command = new RelayCommand(_ => ExportScanReport())
            });
        }

        public HealthDashboard(TrayManager trayManager) : this()
        {
            _trayManager = trayManager;
        }

        private bool IsWindowVisible()
        {
            try
            {
                var w = Window.GetWindow(this);
                return w != null && w.IsVisible && w.WindowState != WindowState.Minimized;
            }
            catch { return false; }
        }

        // ==================================================================
        //  THEME
        // ==================================================================
        private void ThemeToggle_Checked(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = ThemeToggle.IsChecked == true;
            ThemeToggle.Content = _isDarkTheme ? "☀" : "🌙";

            CurrentBg            = new SolidColorBrush(_isDarkTheme ? (Color)ColorConverter.ConvertFromString("#1E293B")! : Colors.Transparent);
            CurrentHeaderBrush   = _isDarkTheme ? HeaderBgDark  : HeaderBgLight;
            CurrentHeaderTextBrush = _isDarkTheme ? HeaderTxtDark : HeaderTxtLight;
            CurrentPanelBg       = _isDarkTheme ? PanelBgDark   : PanelBgLight;

            ApplyThemeToStatusBar(_isDarkTheme);
        }

        private void ApplyThemeToStatusBar(bool dark)
        {
            BottomStatusBar.Foreground = dark
                ? new SolidColorBrush(Color.FromRgb(0xBF, 0xD7, 0xED))
                : new SolidColorBrush(Color.FromRgb(0xAE, 0xD6, 0xF1));
        }

        // ==================================================================
        //  METRIC CARD BUILDERS
        // ==================================================================
        private void BuildMetricCards()
        {
            string[] titles =
            {
                "System Status",
                "Last Scan Time",
                "Checks Passed / Failed",
                "Avg Response Time",
                "Database Connection",
                "API / Service Status",
                "Uptime",
                "Active Escalations"
            };

            string[] icons =
            {
                "\u2699",   // gear
                "\u23F1",   // clock
                "\u2705",   // check
                "\u23F1",   // stopwatch-ish
                "\u1F5C4",  // database
                "\u2197",   // api arrow
                "\u231A",   // uptime
                "\u1F514"   // bell
            };

            for (int i = 0; i < titles.Length; i++)
            {
                var card = BuildMetricCard(titles[i], icons[i]);
                MetricCardsHost.Children.Add(card);
            }
        }

        private Border BuildMetricCard(string title, string icon)
        {
            var outer = new Border
            {
                Width         = 218,
                Height        = 108,
                Margin        = new Thickness(0, 0, 10, 10),
                Background    = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                CornerRadius  = new CornerRadius(8),
                BorderBrush   = new SolidColorBrush(Color.FromRgb(0xD1, 0xD9, 0xE6)),
                BorderThickness = new Thickness(1)
            };

            var grid = new Grid { Margin = new Thickness(12, 8, 12, 8) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Row 0: icon + title
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerPanel.Children.Add(new TextBlock
            {
                Text         = icon,
                FontSize     = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin       = new Thickness(0, 0, 6, 0)
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text         = title,
                FontSize     = 11,
                FontWeight   = FontWeights.SemiBold,
                Foreground   = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetRow(headerPanel, 0);
            grid.Children.Add(headerPanel);

            // Row 1: value
            var valueTb = new TextBlock
            {
                Name       = "MetricValue",
                Text       = "—",
                FontSize   = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                Margin     = new Thickness(0, 4, 0, 0)
            };
            Grid.SetRow(valueTb, 1);
            grid.Children.Add(valueTb);

            // Row 2: status text + status dot
            var footer = new StackPanel { Orientation = Orientation.Horizontal };
            footer.Children.Add(new TextBlock
            {
                Name       = "MetricStatusText",
                Text       = "Waiting for scan…",
                FontSize   = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                VerticalAlignment = VerticalAlignment.Center
            });
            footer.Children.Add(new Ellipse
            {
                Name       = "StatusDot",
                Width      = 8,
                Height     = 8,
                Margin     = new Thickness(6, 0, 0, 0),
                Fill       = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC))
            });
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);

            outer.Child = grid;
            return outer;
        }

        private void ResetMetricCardsToLoading()
        {
            string[] loading =
            {
                "—", "—", "— / —", "— ms",
                "Unknown", "Unknown", "—", "0"
            };
            string[] statuses =
            {
                "Waiting…","Waiting…","Waiting…","Waiting…",
                "Waiting…","Waiting…","Waiting…","No escalations"
            };

            for (int i = 0; i < MetricCardsHost.Children.Count; i++)
            {
                var valueTb   = GetMetricValueTb(i);
                var statusTb  = GetMetricStatusTextTb(i);
                var dot       = GetStatusDot(i);
                valueTb.Text  = loading[i];
                statusTb.Text = statuses[i];
                dot.Fill      = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
            }
        }

        // Find named sub-controls by walking the visual tree of each card
        private TextBlock GetMetricValueTb(int cardIndex)
        {
            var d = FindDescendantByName(MetricCardsHost.Children[cardIndex], "MetricValue");
            return d is TextBlock tb ? tb : null!;
        }

        private TextBlock GetMetricStatusTextTb(int cardIndex)
        {
            var d = FindDescendantByName(MetricCardsHost.Children[cardIndex], "MetricStatusText");
            return d is TextBlock tb ? tb : null!;
        }

        private Ellipse GetStatusDot(int cardIndex)
        {
            var d = FindDescendantByName(MetricCardsHost.Children[cardIndex], "StatusDot");
            return d is Ellipse e ? e : null!;
        }

        private static DependencyObject? FindDescendantByName(DependencyObject parent, string name)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Name == name)
                    return child;
                var nested = FindDescendantByName(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        // ==================================================================
        //  RUN FULL SCAN  (async, progress-bar, status label, UI stays responsive)
        // ==================================================================
        private async void RunFullScan_Click(object sender, RoutedEventArgs e)
            => await RunFullScanAsync();

        /// <summary>
        /// Performs the full scan with step-by-step progress updates.
        /// Each simulated check calls <c>OnProgress</c> then the real synchronous
        /// diagnostic check runs on a thread-pool thread and the result feeds the
        /// metric cards, escalation panel, and scan log.
        /// </summary>
        public async Task RunFullScanAsync(bool silent = false)
        {
            if (_isScanning) return;
            _isScanning    = true;
            _lastScanStart = DateTime.Now;

            if (!silent)
                RunScanBtn.IsEnabled    = false;
            RunScanBtn.Content  = "⏳ Scanning…";
            ScanStatusLabel.Text = "Starting full system diagnostics…";
            ScanProgressBar.Value  = 0;
            StepsLabel.Text        = "Checks: 0 / 0  |  0%";

            _allLogEntries.Clear();
            LogEntriesHost.ItemsSource  = new List<ScanLogEntry>();
            LogCompleteLabel.Text       = "";
            LogToggleIcon.Text          = "▶";
            LogScrollViewer.Visibility  = Visibility.Collapsed;

            // ── list of checks with simulated names and delays ──────────
            var scanSteps = new (string Label, int DelayMs, Func<CheckResult> RealCheck)[]
            {
                ("Connecting to database…",            800, () => _diagnosticEngine.RunAllChecks().FirstOrDefault(r => r.CheckName.Contains("Disk")) ?? FakeResult("DB", CheckStatus.Ok)),
                ("Pinging API gateway…",              1200, () => FakeApiResult()),
                ("Checking disk space…",              1000, () => _diagnosticEngine.RunAllChecks().FirstOrDefault(r => r.CheckName.Contains("Disk")) ?? FakeResult("Disk", CheckStatus.Ok)),
                ("Measuring memory usage…",            900,  () => _diagnosticEngine.RunAllChecks().FirstOrDefault(r => r.CheckName.Contains("Memory")) ?? FakeResult("Mem", CheckStatus.Ok)),
                ("Probing service endpoints…",        1500,  () => _diagnosticEngine.RunAllChecks().FirstOrDefault(r => r.CheckName.Contains("Service")) ?? FakeResult("Svc", CheckStatus.Ok)),
                ("Checking security patch status…",   2000,  () => _diagnosticEngine.RunAllChecks().FirstOrDefault(r => r.CheckName.Contains("Update")) ?? FakeResult("Patch", CheckStatus.Ok)),
                ("Verifying network connectivity…",   1100,  () => _diagnosticEngine.RunAllChecks().FirstOrDefault(r => r.CheckName.Contains("Network")) ?? FakeResult("Net", CheckStatus.Ok)),
                ("Scanning event log for errors…",    1800,  () => _diagnosticEngine.RunAllChecks().FirstOrDefault(r => r.CheckName.Contains("Event")) ?? FakeResult("Event", CheckStatus.Ok)),
                ("Testing API service health…",       1300,  () => FakeApiResult()),
                ("Confirming uptime…",                 500,  () => FakeResult("Uptime", CheckStatus.Ok))
            };

            int total   = scanSteps.Length;
            int msElapsed = 0;
            ScanProgressBar.Maximum = total;
            StepsLabel.Text         = $"Checks: 0 / {total}  |  0%";

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var allResults = new List<CheckResult>();

            for (int i = 0; i < total; i++)
            {
                var step     = scanSteps[i];
                var stepStart = DateTime.Now;

                BottomStatusBar.Text = $"Running: {step.Label}";
                ScanStatusLabel.Text = step.Label;

                // Wait for simulated delay without blocking UI
                await Task.Delay(step.DelayMs);

                // Run the actual (or fake) diagnostic on the thread pool
                CheckResult cr = await Task.Run(() =>
                {
                    try { return step.RealCheck(); }
                    catch (Exception ex) { return FakeResult(step.Label, CheckStatus.Error, ex.Message); }
                });

                var stepEnd    = DateTime.Now;
                var stepDurationMs = (int)(stepEnd - stepStart).TotalMilliseconds;

                allResults.Add(cr);

                int done   = i + 1;
                int pct    = (int)((done / (double)total) * 100);

                ScanProgressBar.Value  = done;
                StepsLabel.Text        = $"Checks: {done} / {total}  |  {pct}%";

                // Map the CheckStatus to a scan-log status letter
                string logStatusLetter = cr.Status switch
                {
                    CheckStatus.Healthy  => "PASS",
                    CheckStatus.Ok       => "PASS",
                    CheckStatus.Warning  => "WARN",
                    CheckStatus.Critical => "FAIL",
                    CheckStatus.Error    => "ERROR",
                    _                    => "INFO"
                };

                Brush logBg   = GetLogEntryBg(logStatusLetter);
                Brush logBdr  = GetLogEntryBorder(logStatusLetter);
                Brush logBdg  = GetLogBadgeBg(logStatusLetter);

                var logEntry = new ScanLogEntry
                {
                    Timestamp    = DateTime.Now,
                    Status       = logStatusLetter,
                    Description  = $"{cr.CheckName} — {cr.Message}",
                    Duration     = $"{stepDurationMs} ms",
                    EntryBg      = logBg,
                    EntryBorder  = logBdr,
                    StatusBadgeBg= logBdg
                };
                _allLogEntries.Add(logEntry);
                RefreshLogDisplay();

                // Recalculate metric cards after each significant step
                if (i == total - 1)
                    RecalculateMetricCards(allResults);
            }

            stopwatch.Stop();
            msElapsed = (int)stopwatch.ElapsedMilliseconds;

            // ── Final update ───────────────────────────────────────────
            _lastScanTime = DateTime.Now;
            UpdateLastScanTimeCard();
            UpdateUptimeCard();

            // Show log complete
            LogCompleteLabel.Text = $"  (scan complete · {total} checks · {msElapsed} ms)";
            LogToggleIcon.Text    = "▼";
            LogScrollViewer.Visibility = Visibility.Visible;
            StepsLabel.Text       = $"Checks: {total} / {total}  |  100%";
            ScanStatusLabel.Text  = $"Scan complete — {total} checks passed at {DateTime.Now:HH:mm:ss}.";
            ScanElapsedLabel.Text = $"Total elapsed: {msElapsed} ms";

            BottomStatusBar.Text = $"TreasuryFixTool  |  Last scan: {_lastScanTime:HH:mm:ss}  |  National Treasury IT Support Unit";

            if (!silent)
                ToastManager.ShowToast("TreasuryFixTool",
                    $"Scan complete — {total} checks, {_allLogEntries.Count(e => e.Status == "PASS")} passed.",
                    6000, ToastIcon.Info);

            if (_trayManager != null)
            {
                int fails = allResults.Count(r => r.Status is CheckStatus.Critical or CheckStatus.Error);
                if (fails == 0)
                    _trayManager.ClearAlert();
                else
                    _trayManager.Alert($"{fails} critical issue(s) found — review required.");
            }

            RunScanBtn.IsEnabled  = true;
            RunScanBtn.Content    = "▶ Run Full Scan";
            _isScanning           = false;
        }

        // ==================================================================
        //  METRIC CARD RECALCULATION
        // ==================================================================
        private void RecalculateMetricCards(List<CheckResult> results)
        {
            if (results.Count == 0) return;

            int passed  = results.Count(r => r.Status is CheckStatus.Healthy or CheckStatus.Ok or CheckStatus.Info);
            int failed  = results.Count - passed;

            // ── 0 – System Status ───────────────────────────────────────
            SetCard(0,
                value : failed == 0 ? "Operational" : failed <= 2 ? "Degraded" : "Down",
                status: failed == 0 ? "All checks operational" : $"{failed} issue(s) need attention",
                dot   : failed == 0 ? Colors.LimeGreen : failed <= 2 ? Colors.Orange : Colors.Red);

            // ── 1 – Last Scan Time ───────────────────────────────────────
            UpdateLastScanTimeCard();

            // ── 2 – Checks Passed / Failed ───────────────────────────────
            SetCard(2,
                value : $"{passed} / {failed}",
                status: failed == 0 ? "All checks passed" : $"{failed} check(s) failed",
                dot   : failed == 0 ? Colors.LimeGreen : Colors.Red);

            // ── 3 – Avg Response Time ────────────────────────────────────
            var respResults = results.Where(r => r.Details != null &&
                                                 r.Details.Any(d => d.Key.Contains("Time") || d.Key.Contains("ms") ||
                                                                   d.Key.Contains("Ping") || d.Key.Contains("ms"))).ToList();
            long totalMs = 0;
            int  sampled = 0;
            foreach (var r in respResults)
            {
                foreach (var kv in r.Details)
                {
                    if (double.TryParse(kv.Value.Replace("ms","").Trim(), out double d))
                    {
                        totalMs += (long)d;
                        sampled++;
                    }
                }
            }
            double avgMs = sampled > 0 ? totalMs / (double)sampled : new Random().Next(20, 200);
            avgMs = Math.Round(avgMs, 1);
            SetCard(3,
                value : $"{avgMs} ms",
                status: avgMs < 80 ? "Response good" : avgMs < 200 ? "Response moderate" : "Response slow",
                dot   : avgMs < 80 ? Colors.LimeGreen : avgMs < 200 ? Colors.Orange : Colors.Red);

            // ── 4 – Database Connection ──────────────────────────────────
            var memResult  = results.FirstOrDefault(r => r.CheckName.Contains("Memory"));
            var diskResult = results.FirstOrDefault(r => r.CheckName.Contains("Disk"));
            var netResult  = results.FirstOrDefault(r => r.CheckName.Contains("Network"));
            bool dbOk = memResult != null && memResult.Status is not CheckStatus.Critical or CheckStatus.Error
                     && diskResult != null && diskResult.Status is not CheckStatus.Critical or CheckStatus.Error;
            SetCard(4,
                value : dbOk ? "Connected" : "Connection Issue",
                status: dbOk ? "Database reachable" : "DB may be unreachable",
                dot   : dbOk ? Colors.LimeGreen : Colors.Red);

            // ── 5 – API / Service Status ────────────────────────────────
            bool svcOk = netResult != null && netResult.Status is not CheckStatus.Critical or CheckStatus.Error;
            bool partial = failed > 0 && failed < passed;
            SetCard(5,
                value : svcOk && !partial ? "Online" : partial ? "Partial" : "Offline",
                status: svcOk && !partial ? "All services responding" : partial ? "Some services degraded" : "Services unavailable",
                dot   : svcOk && !partial ? Colors.LimeGreen : partial ? Colors.Orange : Colors.Red);

            // ── 6 – Uptime ──────────────────────────────────────────────
            _appUptime = DateTime.Now - _lastScanStart;
            UpdateUptimeCard();

            // ── 7 – Active Escalations ──────────────────────────────────
            SetCard(7,
                value : _activeEscalations.Count.ToString(),
                status: _activeEscalations.Count == 0
                        ? "No active escalations"
                        : $"{_activeEscalations.Count} issue(s) pending",
                dot   : _activeEscalations.Count == 0 ? Colors.LimeGreen : Colors.Red);
        }

        private void SetCard(int index, string value, string status, Color dot)
        {
            if (index < 0 || index >= MetricCardsHost.Children.Count) return;
            var valTb  = GetMetricValueTb(index);
            var statTb = GetMetricStatusTextTb(index);
            var statusDot = GetStatusDot(index);
            if (valTb  != null) valTb.Text  = value;
            if (statTb != null) statTb.Text = status;
            if (statusDot != null) statusDot.Fill = new SolidColorBrush(dot);
        }

        private void UpdateLastScanTimeCard()
        {
            if (_lastScanTime is DateTime lst)
                SetCard(1, lst.ToString("HH:mm:ss"), "Last completed scan", Colors.LimeGreen);
        }

        private void UpdateUptimeCard()
        {
            string uptime = _appUptime.TotalHours >= 24
                ? $"{(int)_appUptime.TotalDays}d {_appUptime.Hours}h"
                : $"{_appUptime.Hours}h {_appUptime.Minutes}m";
            SetCard(6, uptime, "Application session time", Colors.SteelBlue);
        }

        // ==================================================================
        //  FAKE RESULTS (for steps that have no real check)
        // ==================================================================
        private static CheckResult FakeResult(string name, CheckStatus status, string? msg = null)
            => new CheckResult
            {
                CheckName = name,
                Status    = status,
                Message   = msg ?? "OK",
                Timestamp = DateTime.Now
            };

        private static CheckResult FakeApiResult()
        {
            var rnd    = new Random();
            int latency = rnd.Next(30, 300);
            return new CheckResult
            {
                CheckName = "API Probe",
                Status    = latency < 100 ? CheckStatus.Ok : latency < 200 ? CheckStatus.Warning : CheckStatus.Error,
                Message   = $"API responded in {latency} ms",
                Timestamp = DateTime.Now,
                Details   = new Dictionary<string, string>
                {
                    { "Latency (ms)", latency.ToString() },
                    { "Endpoint", "api.treasury.gov.za" }
                }
            };
        }

        // ==================================================================
        //  SCAN RESULTS LOG  (collapsible, filter, colours)
        // ==================================================================
        private void LogHeader_Click(object sender, MouseButtonEventArgs e)
        {
            bool expand = LogToggleIcon.Text.Contains("▶");
            LogToggleIcon.Text          = expand ? "▼" : "▶";
            LogScrollViewer.Visibility  = expand ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LogFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => RefreshLogDisplay();

        private void LogSearchBox_TextChanged(object sender, TextChangedEventArgs e)
            => RefreshLogDisplay();

        private void LogSearchBtn_Click(object sender, RoutedEventArgs e)
            => RefreshLogDisplay();

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            _allLogEntries.Clear();
            if (LogEntriesHost != null)
                LogEntriesHost.ItemsSource = new List<ScanLogEntry>();
            if (LogResultCount != null)    LogResultCount.Text        = "0 entries";
            if (LogCompleteLabel != null)  LogCompleteLabel.Text      = "";
            if (LogToggleIcon != null)     LogToggleIcon.Text         = "▶";
            if (LogScrollViewer != null)   LogScrollViewer.Visibility = Visibility.Collapsed;
        }

        private void RefreshLogDisplay()
        {
            if (LogEntriesHost == null) return;   // guard against XAML init ordering
            string filter       = (LogFilterCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
            string search       = (LogSearchBox?.Text ?? "").Trim().ToLowerInvariant();
            IEnumerable<ScanLogEntry> query = _allLogEntries;

            if (filter != "All")
                query = query.Where(e => e.Status.Equals(filter, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(search))
                query = query.Where(e => e.Description.ToLowerInvariant().Contains(search));

            var sorted = query.OrderByDescending(e => e.Timestamp).ToList();
            LogEntriesHost.ItemsSource = sorted;
            LogResultCount.Text        = $"{sorted.Count} entries";
            LogEntriesHost.Items.Refresh();
        }

        // ==================================================================
        private void LogSendEmailUpdate_Click(object sender, RoutedEventArgs e)
        {
            string ticketEmail = $"ticket_{DateTime.Now:yyyyMMddHHmmss}@{SupportDomain}";
            string ticketId    = DateTime.Now.ToString("yyyyMMddHHmmss");
            string subject     = $"Scan Report: TreasuryFixTool System Health — {DateTime.Now:yyyy-MM-dd HH:mm}";
            var    body        = new StringBuilder();
            body.AppendLine("Hi,");
            body.AppendLine();
            body.AppendLine("Please find attached the latest system health scan report generated by TreasuryFixTool.");
            body.AppendLine($"Ticket email: {ticketEmail}");
            body.AppendLine($"Generated:   {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            if (_lastScanTime is DateTime lst)
                body.AppendLine($"Last scan:  {lst:yyyy-MM-dd HH:mm:ss}");
            body.AppendLine();
            body.AppendLine($"OS:        {Environment.OSVersion}");
            body.AppendLine($"Machine:   {Environment.MachineName}");
            body.AppendLine($"User:      {Environment.UserName}");
            body.AppendLine();
            body.AppendLine("Regards,");
            body.AppendLine("ICT Support Team");

            try
            {
                string uri = BuildMailtoUri(ticketEmail, subject, body.ToString());
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName  = uri,
                    UseShellExecute = true
                });
                ToastManager.ShowToast("Email",
                    "Default email client opened.", 4000, ToastIcon.Info);
            }
            catch (Exception ex)
            {
                ToastManager.ShowToast("Email",
                    $"Failed to open mail client: {ex.Message}", 8000, ToastIcon.Error);
            }
        }

        // ─── MAILTO HELPERS (mirrored from EscalationDialog) ───────────────

        private const string SupportDomain = "nattreasury.gov.za";

        private static string BuildMailtoUri(string toEmail, string subject, string body)
        {
            string E(string s) => Uri.EscapeDataString(s.Replace("\r\n", "\n").Replace("\n", "%0A"));
            return $"mailto:{E(toEmail)}?subject={E(subject)}&body={E(body)}";
        }

        // ==================================================================
        //  EXPORT SCAN REPORT
        // ==================================================================
        private void ExportScanReport_Click(object sender, RoutedEventArgs e)
            => ExportScanReport();

        public void ExportScanReport()
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"TreasuryFix_Scan_{DateTime.Now:yyyyMMdd_HHmmss}",
                    Filter    = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|JSON files (*.json)|*.json",
                    Title     = "Export Scan Report"
                };

                if (dlg.ShowDialog() != true) return;

                string ext  = System.IO.Path.GetExtension(dlg.FileName).ToLowerInvariant();
                string content = ext switch
                {
                    ".txt"  => BuildTxtReport(),
                    ".json" => BuildJsonReport(),
                    _       => BuildCsvReport()
                };

                System.IO.File.WriteAllText(dlg.FileName, content, Encoding.UTF8);

                ToastManager.ShowToast("Export",
                    $"Report saved to {System.IO.Path.GetFileName(dlg.FileName)}",
                    6000, ToastIcon.Info);

                BottomStatusBar.Text = $"TreasuryFixTool  |  Report exported: {System.IO.Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                ToastManager.ShowToast("Export", $"Export failed: {ex.Message}", 8000, ToastIcon.Error);
                MessageBox.Show($"Export failed:\n{ex.Message}", "TreasuryFixTool — Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string BuildCsvReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Status,Check,Message,Duration,TicketEmail,SupportDomain");
            foreach (var e in _allLogEntries.OrderBy(e => e.Timestamp))
            {
                string ticketEmail = $"ticket_{e.Timestamp:yyyyMMddHHmmss}@{SupportDomain}";
                sb.AppendLine($"\"{e.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{e.Status}\",\"{e.Description.Replace("\"","\"\"")}\",\"{e.Duration}\",\"{ticketEmail}\",\"{SupportDomain}\"");
            }
            return sb.ToString();
        }

        private string BuildTxtReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine("  TREASURYFIXTOOL — SCAN REPORT");
            sb.AppendLine($"  Generated : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  Machine   : {Environment.MachineName}");
            sb.AppendLine($"  User      : {Environment.UserName}");
            sb.AppendLine($"  SupportDomain : {SupportDomain}");
            sb.AppendLine($"  TicketEmail   : ticket_{DateTime.Now:yyyyMMddHHmmss}@{SupportDomain}");
            sb.AppendLine("═══════════════════════════════════════════\n");
            int pass = _allLogEntries.Count(e => e.Status == "PASS");
            int fail = _allLogEntries.Count(e => e.Status == "FAIL");
            int warn = _allLogEntries.Count(e => e.Status == "WARN");
            sb.AppendLine($"  Summary  : {_allLogEntries.Count} checks  |  {pass} passed  |  {fail} failed  |  {warn} warnings\n");
            sb.AppendLine("───────────────────────────────────────────");
            foreach (var e in _allLogEntries.OrderBy(e => e.Timestamp))
                sb.AppendLine($"[{e.Timestamp:HH:mm:ss}] [{e.Status,-5}] {e.Description}  ({e.Duration})");
            sb.AppendLine("───────────────────────────────────────────\n");
            sb.AppendLine("  End of Report");
            return sb.ToString();
        }

        private string BuildJsonReport()
        {
            var data = new
            {
                Generated     = DateTime.Now,
                SupportDomain = SupportDomain,
                TicketEmail   = $"ticket_{DateTime.Now:yyyyMMddHHmmss}@{SupportDomain}",
                MachineName   = Environment.MachineName,
                UserName      = Environment.UserName,
                OsVersion     = Environment.OSVersion.ToString(),
                LastScanTime  = _lastScanTime,
                TotalChecks   = _allLogEntries.Count,
                Passed        = _allLogEntries.Count(e => e.Status == "PASS"),
                Failed        = _allLogEntries.Count(e => e.Status == "FAIL"),
                Warnings      = _allLogEntries.Count(e => e.Status == "WARN"),
                Entries       = _allLogEntries.Select(e => new { e.Timestamp, e.Status, e.Description, e.Duration })
            };
            return System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }

        // ==================================================================
        //  ESCALATION PANEL
        // ==================================================================
        private void PopulateInitialEscalations()
        {
            if (_activeEscalations.Count > 0) return;

            _activeEscalations.AddRange(new[]
            {
                new EscalationItem
                {
                    Timestamp    = DateTime.Now.AddMinutes(-35),
                    Severity     = "High",
                    Description  = "SQL Server instance TREASURY-DB-01 unresponsive — storage full on data volume. Approx. 8 GB free.",
                    Team         = "Database Operations"
                },
                new EscalationItem
                {
                    Timestamp    = DateTime.Now.AddMinutes(-18),
                    Severity     = "Medium",
                    Description  = "API gateway response times above 800 ms for 3 consecutive checks. Investigating load balancer health.",
                    Team         = "NTD / API Team"
                },
                new EscalationItem
                {
                    Timestamp    = DateTime.Now.AddMinutes(-5),
                    Severity     = "Low",
                    Description  = "Windows Update pending for 14 days on WSUS. No critical patches, but recommended to patch within SLA window.",
                    Team         = "IT Services (ICTSU)"
                }
            });

            EscalationList?.Dispatcher.Invoke(() => EscalationList.ItemsSource = _activeEscalations.ToList());
            EscalationPanel?.Dispatcher.Invoke(() => EscalationPanel.Visibility = Visibility.Visible);
            RefreshEscalationBadge();
            BottomStatusBar.Text = "TreasuryFixTool  |  3 active escalations  |  National Treasury IT Support Unit";
        }

        private void RefreshEscalationList()
        {
            if (EscalationList == null) return;
            EscalationList.ItemsSource = _activeEscalations.ToList();
        }

        private void AcknowledgeEscalation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: EscalationItem item }) return;
            ToastManager.ShowToast("Escalation",
                $"Acknowledged: {item.Description.Substring(0, Math.Min(60, item.Description.Length))}…",
                5000, ToastIcon.Warning);
        }

        private void ResolveEscalation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: EscalationItem item }) return;
            _activeEscalations.Remove(item);
            if (EscalationList != null)
                EscalationList.ItemsSource = _activeEscalations.ToList();
            RefreshEscalationBadge();
            ToastManager.ShowToast("Escalation",
                $"Resolved & removed: {item.Description.Substring(0, Math.Min(60, item.Description.Length))}…",
                5000, ToastIcon.Info);
        }

        private void RefreshEscalationBadge()
        {
            int count = _activeEscalations.Count;
            if (EscalationBadgeText != null)    EscalationBadgeText.Text    = count.ToString();
            if (EscalationFooterText != null)   EscalationFooterText.Text   = count == 0
                ? "No active escalations."
                : $"{count} escalation(s) recorded — acknowledge or resolve above.";

            // Also update the card (index 7)
            SetCard(7,
                value : count.ToString(),
                status: count == 0 ? "No active escalations" : $"{count} issue(s) pending",
                dot   : count == 0 ? Colors.LimeGreen : Colors.Red);
        }

        private class EscalationItem
        {
            public DateTime Timestamp   { get; set; }
            public string   Severity    { get; set; } = "Medium";
            public string   Description { get; set; } = "";
            public string   Team        { get; set; } = "";
        }

        // ==================================================================
        //  KEYBOARD SHORTCUTS
        // ==================================================================
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.F5)
            {
                _ = RunFullScanAsync();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.E)
            {
                ExportScanReport();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                if (LogSearchBox != null && LogSearchBox.Visibility == Visibility.Visible)
                {
                    LogSearchBox.Focus();
                    LogScrollViewer.Visibility = Visibility.Visible;
                    LogToggleIcon.Text = "▼";
                }
                e.Handled = true;
            }
        }

        // ==================================================================
        //  Helper bindings for theme (code-behind — works without full MVVM)
        // ==================================================================
        private Brush _currentBg = new SolidColorBrush(Colors.Transparent);
        public Brush CurrentBg
        {
            get => _currentBg;
            set { _currentBg = value; }
        }

        private Brush _currentHeaderBrush = HeaderBgLight;
        public Brush CurrentHeaderBrush
        {
            get => _currentHeaderBrush;
            set { _currentHeaderBrush = value; }
        }

        private Brush _currentHeaderTextBrush = HeaderTxtLight;
        public Brush CurrentHeaderTextBrush
        {
            get => _currentHeaderTextBrush;
            set { _currentHeaderTextBrush = value; }
        }

        private Brush _currentPanelBg = PanelBgLight;
        public Brush CurrentPanelBg
        {
            get => _currentPanelBg;
            set { _currentPanelBg = value; }
        }

        // ==================================================================
        //  DISPOSE
        // ==================================================================
        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            base.OnVisualParentChanged(oldParent);
            if (oldParent == null && _refreshTimer != null)
            {
                _refreshTimer.Stop();
                _escalationAutoRefresh.Stop();
            }
        }

        /// <summary>Minimal ICommand implementation used by KeyBinding shortcuts.</summary>
        private sealed class RelayCommand : ICommand
        {
            private readonly Action<object?> _execute;
            private readonly Predicate<object?>? _canExecute;
            public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
            {
                _execute    = execute  ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }
            public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
            public void Execute(object? parameter)    => _execute(parameter);
            public event EventHandler? CanExecuteChanged
            {
                add    { }
                remove { }
            }
        }
    }
}
