using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Threading.Tasks;
using TreasuryFixTool.Notifications;
using TreasuryFixTool.Infrastructure.Storage;
using TreasuryFixTool.Updates;
using TreasuryFixTool.Infrastructure.Logging;

namespace TreasuryFixTool.Views;

/// <summary>
/// Update Centre — manages online release checks (GitHub API) and offline
/// USB recipe imports for TreasuryFixTool.
/// </summary>
public partial class UpdateView : UserControl
{
    private readonly UpdateChecker   _updateChecker;
    private readonly UsbUpdateHandler _usbHandler;
    private readonly FileLogger      _logger;
    private               DispatcherTimer _autoUsbTimer;

    // Current-version is read from the file version of the running assembly
    private readonly string _currentVersion;

    private int  _autoUsbIntervalMinutes = 1;
    private bool _scanningUsb;

    public UpdateView()
    {
        // Pre-initialise everything that event handlers may read during
        // XAML construction (SelectionChanged fires inside InitializeComponent)
        _currentVersion = GetAssemblyVersion();

        _updateChecker = new UpdateChecker();
        _logger        = new FileLogger(
                            System.IO.Path.Combine(DataPaths.LogsDirectory,
                                                   "Updates.log"));
        try { _usbHandler = new UsbUpdateHandler(logger: _logger); }
        catch { _usbHandler = new UsbUpdateHandler(); }

        _autoUsbTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(_autoUsbIntervalMinutes)
        };
        _autoUsbTimer.Tick += (_, _) => { _ = Task.Run(() => PerformUsbScan()); };

        InitializeComponent();

        CurrentVersionText.Text = _currentVersion;

        // Enumerate USB drives at load
        PopulateUsbDrives();
    }

    // ─── Assembly version helper ─────────────────────────────────────────────
    private static string GetAssemblyVersion()
    {
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version;
            return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
        }
        catch { return "1.0.0"; }
    }

    // ─── Update log ──────────────────────────────────────────────────────────
    private void AddLogEntry(string statusLetter, string description,
                             System.Windows.Media.SolidColorBrush? statusBg,
                             System.Windows.Media.SolidColorBrush? entryBorder,
                             System.Windows.Media.SolidColorBrush? entryBg)
    {
        var entry = new UpdateLogEntry
        {
            Timestamp      = DateTime.Now,
            StatusLetter   = statusLetter,
            Description    = description,
            StatusBadgeBg  = statusBg  ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC,0xCC,0xCC)),
            EntryBorder    = entryBorder ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEE,0xEE,0xEE)),
            EntryBg        = entryBg   ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White)
        };

        Dispatcher.Invoke(() =>
        {
            LogEntriesHost.Items?.Add(entry);
            LogScrollViewer?.ScrollToBottom();
        });
    }

    public class UpdateLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string   StatusLetter { get; set; } = "";
        public System.Windows.Media.SolidColorBrush StatusBadgeBg { get; set; } = null!;
        public System.Windows.Media.SolidColorBrush EntryBorder { get; set; } = null!;
        public System.Windows.Media.SolidColorBrush EntryBg   { get; set; } = null!;
        public string Description { get; set; } = "";
    }

    // ─── Log panel menu ──────────────────────────────────────────────────────
    private void LogHeader_Click(object sender, RoutedEventArgs e)
    {
        // Nothing special required – expanded by default
    }

    private async void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        await Dispatcher.InvokeAsync(() => LogEntriesHost.Items?.Clear());
        await Dispatcher.InvokeAsync(() => LogFooter.Text = "Log cleared.");
    }

    // ─── Open release notes in browser ───────────────────────────────────────
    private void ReleaseNotesLink_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ReleaseNotesLink.Tag is string url && !string.IsNullOrWhiteSpace(url))
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { /* ignore – network may be unavailable */ }
        }
    }

    // ─── Online update check ─────────────────────────────────────────────────
    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateBtn.IsEnabled = false;
        CheckUpdateBtn.Content   = "⏳  Checking…";
        UpdateStatusText.Text    = "Contacting GitHub releases…";
        AddLogEntry("INFO", $"Checking for updates (current: {_currentVersion})…",
            new(System.Windows.Media.Color.FromRgb(0x55,0x8B,0x2F)),
            new(System.Windows.Media.Color.FromRgb(0xDD,0xEE,0xDD)),
            new(System.Windows.Media.Color.FromRgb(0xF6,0xFF,0xF0)));

        try
        {
            var info = await _updateChecker.CheckForUpdateAsync(_currentVersion);

            if (info == null)
            {
                UpdateStatusText.Text = "Already on the latest version.";
                UpdateInfoCard.Visibility = Visibility.Collapsed;
                ReleaseNotesLink.Visibility = Visibility.Collapsed;
                AddLogEntry("OK", "Already on the latest version.",
                    new(System.Windows.Media.Color.FromRgb(0x27,0xAE,0x60)),
                    new(System.Windows.Media.Color.FromRgb(0xD5,0xF5,0xE3)),
                    new(System.Windows.Media.Color.FromRgb(0xEA,0xFA,0xF1)));
                ToastManager.ShowToast("Update", "You are on the latest version.", 5000, ToastIcon.Info);
            }
            else
            {
                UpdateNewVersionLabel.Text = "New version available:";
                UpdateNewVersion.Text      = info.Version;
                UpdateChangelog.Text       = string.IsNullOrWhiteSpace(info.Changelog)
                                             ? "(no changelog provided)" : info.Changelog;
                UpdatePublished.Text       = $"Released: {info.Published:yyyy-MM-dd HH:mm}";
                UpdateInfoCard.Visibility  = Visibility.Visible;

                ReleaseNotesLink.Text  = $"Open release notes on GitHub ↗";
                ReleaseNotesLink.Tag   = info.DownloadUrl;
                ReleaseNotesLink.Visibility = Visibility.Visible;

                UpdateUrgency.Text     = string.IsNullOrWhiteSpace(info.Changelog)
                                         ? "" : "See changelog for breaking changes before upgrading.";

                UpdateStatusText.Text = $"Update available: {info.Version}";
                _logger.Info($"Update available: {info.Version} — {info.DownloadUrl}");

                AddLogEntry("UP", $"Update available: v{info.Version} (released {info.Published:yyyy-MM-dd})",
                    new(System.Windows.Media.Color.FromRgb(0xE6,0x51,0x00)),
                    new(System.Windows.Media.Color.FromRgb(0xFB,0xEB,0xE8)),
                    new(System.Windows.Media.Color.FromRgb(0xFF,0xF3,0xF0)));

                ToastManager.ShowToast(
                    "Update Available",
                    $"TreasuryFixTool v{info.Version} is available.\nChangelog: {info.DownloadUrl}",
                    12000, ToastIcon.Info);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Online update check failed", ex);
            UpdateStatusText.Text = "Check failed. Verify internet connection.";
            AddLogEntry("ERR", $"Update check failed: {ex.Message}",
                new(System.Windows.Media.Color.FromRgb(0xC0,0x39,0x2B)),
                new(System.Windows.Media.Color.FromRgb(0xFD,0xED,0xEC)),
                new(System.Windows.Media.Color.FromRgb(0xFF,0xF5,0xF5)));
            ToastManager.ShowToast("Update Error", ex.Message, 6000, ToastIcon.Error);
        }
        finally
        {
            CheckUpdateBtn.IsEnabled = true;
            CheckUpdateBtn.Content   = "🔄  Check for Update";
        }
    }

    // ─── USB scan ────────────────────────────────────────────────────────────
    private void ScanUsb_Click(object sender, RoutedEventArgs e)
    {
        if (_scanningUsb) return;
        _ = Task.Run(() => PerformUsbScan());
    }

    private async Task PerformUsbScan()
    {
        _scanningUsb = true;
        AddLogEntry("USB", "Scanning removable drives for update package…",
            new(System.Windows.Media.Color.FromRgb(0x6A,0x4F,0xB6)),
            new(System.Windows.Media.Color.FromRgb(0xEF,0xEB,0xF9)),
            new(System.Windows.Media.Color.FromRgb(0xF8,0xF5,0xFF)));

        try
        {
            await Dispatcher.InvokeAsync(() =>
            {
                ScanUsbBtn.Content   = "⏳  Scanning…";
                ScanUsbBtn.IsEnabled = false;
            }, System.Windows.Threading.DispatcherPriority.Normal);

            // ScanAndImport is synchronous void — run it off the UI thread
            _usbHandler.ScanAndImport();

            // Re-enumerate and update UI on dispatch
            await Dispatcher.InvokeAsync(() =>
            {
                PopulateUsbDrives();
                ScanUsbBtn.Content   = "🔍  Scan USB for Updates";
                ScanUsbBtn.IsEnabled = true;
                UsbLastScanBorder.Visibility = Visibility.Visible;
                UsbLastScanTime.Text = $"Scanned at {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                UsbLastScanResult.Text = "Scan complete. Destination folder checked for imported recipes.";
                UsbLastScanResult.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1B,0x5E,0x20));

                AddLogEntry("USB", "USB scan complete.",
                    new(System.Windows.Media.Color.FromRgb(0x27,0xAE,0x60)),
                    new(System.Windows.Media.Color.FromRgb(0xD5,0xF5,0xE3)),
                    new(System.Windows.Media.Color.FromRgb(0xEA,0xFA,0xF1)));
                ToastManager.ShowToast("USB Update", "USB scan complete.", 5000, ToastIcon.Info);
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }
        catch (Exception ex)
        {
            _logger.Error("USB scan failed", ex);
            await Dispatcher.InvokeAsync(() =>
            {
                ScanUsbBtn.Content   = "🔍  Scan USB for Updates";
                ScanUsbBtn.IsEnabled = true;
                UsbLastScanBorder.Visibility = Visibility.Visible;
                UsbLastScanResult.Text = $"Scan failed: {ex.Message}";
                UsbLastScanResult.Foreground = System.Windows.Media.Brushes.DarkRed;
                UsbLastScanTime.Text = $"Failed at {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                AddLogEntry("ERR", $"USB scan failed: {ex.Message}",
                    new(System.Windows.Media.Color.FromRgb(0xC0,0x39,0x2B)),
                    new(System.Windows.Media.Color.FromRgb(0xFD,0xED,0xEC)),
                    new(System.Windows.Media.Color.FromRgb(0xFF,0xF5,0xF5)));
                ToastManager.ShowToast("USB Error", ex.Message, 6000, ToastIcon.Error);
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }
        finally
        {
            _scanningUsb = false;
        }
    }

    private void PopulateUsbDrives()
    {
        UsbDrivesList.Items?.Clear();
        NoUsbDrivesText.Visibility = Visibility.Visible;

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Removable) continue;
            NoUsbDrivesText.Visibility = Visibility.Collapsed;
            string label = $"{drive.Name.TrimEnd('\\')} — {drive.VolumeLabel}" +
                           (drive.IsReady ? $"  ({drive.TotalSize / _gb:##,##0} GB)" : "");
            UsbDrivesList.Items?.Add(label);

            // Show on directory where "treasury_update.sig" is found
            string sigPath = System.IO.Path.Combine(drive.Name.TrimEnd('\\'), "treasury_update.sig");
            if (File.Exists(sigPath))
            {
                NoUsbDrivesText.Visibility = Visibility.Collapsed;
                UsbDrivesList.Items?.Add($"   ✓  Signed update package detected");
            }
        }
    }
    private const long _gb = 1024L * 1024L * 1024L;

    // ─── Auto USB scan toggle ─────────────────────────────────────────────────
    private void AutoUsbScanToggle_Checked(object sender, RoutedEventArgs e)
    {
        _autoUsbTimer.Interval = TimeSpan.FromMinutes(_autoUsbIntervalMinutes);
        _autoUsbTimer.Start();
        ((ToggleButton)sender).Content = "ON";
        ((ToggleButton)sender).Background =
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x27, 0xAE, 0x60));
        AutoScanStatus?.Text = $"Auto USB scan: every {_autoUsbIntervalMinutes} min";
    }

    private void AutoUsbScanToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _autoUsbTimer.Stop();
        ((ToggleButton)sender).Content = "OFF";
        ((ToggleButton)sender).Background =
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0xCC, 0xCC));
        AutoScanStatus?.Text = "Auto USB scan: Off";
    }

    private void UsbScanIntervalCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Guard: event fires during XAML load before fields/controls are initialised
        if (((ComboBox)sender).SelectedItem is ComboBoxItem item && item.Content is string text
            && _autoUsbTimer != null)
        {
            if (int.TryParse(text.Replace("min", "").Trim(), out int mins))
            {
                _autoUsbIntervalMinutes = mins;
                _autoUsbTimer.Interval = TimeSpan.FromMinutes(mins);
                if (AutoScanStatus != null)
                    AutoScanStatus.Text = $"Auto USB scan: every {mins} min";
            }
        }
    }

    // ─── Cleanup ─────────────────────────────────────────────────────────────
    protected override void OnVisualParentChanged(System.Windows.DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        if (VisualParent == null)
        {
            _autoUsbTimer.Stop();
            _autoUsbTimer.Tick -= (_, _) => { _ = Task.Run(() => PerformUsbScan()); };
        }
    }
}
