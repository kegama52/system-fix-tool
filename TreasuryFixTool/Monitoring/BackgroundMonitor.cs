using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;
using TreasuryFixTool.Diagnostics;
using TreasuryFixTool.Notifications;
using TreasuryFixTool.SystemTray;

namespace TreasuryFixTool.Monitoring;

public class BackgroundMonitor : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly DiagnosticEngine _diagnosticEngine;
    private readonly TrayManager? _trayManager;
    private readonly Action? _onUsbScan;
    private readonly int _usbScanIntervalCycles;
    private int _elapsedCycles;
    private bool _disposed;

    /// <summary>
    /// Creates a new background monitor.
    /// </summary>
    /// <param name="diagnosticEngine">Used for running diagnostic checks.</param>
    /// <param name="trayManager">Optional tray icon manager for balloon alerts.</param>
    /// <param name="onUsbScan">Optional callback invoked every <paramref name="usbScanIntervalCycles"/> monitoring pulses to scan for USB removable-drive updates.</param>
    /// <param name="usbScanIntervalCycles">How many monitoring cycles must elapse before <paramref name="onUsbScan"/> fires.</param>
    public int IntervalSeconds { get; set; } = 30;

    public BackgroundMonitor(DiagnosticEngine diagnosticEngine, TrayManager? trayManager = null, Action? onUsbScan = null, int usbScanIntervalCycles = 4)
    {
        _diagnosticEngine   = diagnosticEngine   ?? throw new ArgumentNullException(nameof(diagnosticEngine));
        _trayManager        = trayManager;
        _onUsbScan          = onUsbScan;
        _usbScanIntervalCycles = usbScanIntervalCycles == 0 ? 1 : usbScanIntervalCycles;
        _elapsedCycles = 0;
        _timer = new DispatcherTimer(DispatcherPriority.Background);
        _timer.Tick += async (_, _) => await CheckAndAlertAsync();
    }

    public void Start()
    {
        _timer.Interval = TimeSpan.FromSeconds(IntervalSeconds);
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private async Task CheckAndAlertAsync()
    {
        try
        {
            // ── Periodic USB removable-drive scan ──────────────────────────
            _elapsedCycles++;
            if (_elapsedCycles >= _usbScanIntervalCycles)
            {
                _elapsedCycles = 0;
                try { _onUsbScan?.Invoke(); } catch { /* USB scan errors are non-fatal */ }
            }

            // ── Diagnostic checks ──────────────────────────────────────────
            var results = _diagnosticEngine.RunAllChecks();
            var criticalOrWarn = results.FindAll(r
                => r.Status == CheckStatus.Critical || r.Status == CheckStatus.Warning);

            if (criticalOrWarn.Count == 0)
            {
                _trayManager?.ClearAlert();
                return;
            }

            string summary = string.Join(", ",
                criticalOrWarn.Select(r => $"{r.CheckName}: {r.Status}"));
            _trayManager?.Alert(summary);

            foreach (var r in criticalOrWarn)
                ToastManager.ShowToast(
                    "TreasuryFixTool",
                    $"{r.CheckName}: {r.Message}",
                    10000,
                    r.Status == CheckStatus.Critical ? ToastIcon.Error : ToastIcon.Warning);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BackgroundMonitor error: {ex}");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing) _timer.Stop();
        _disposed = true;
    }
}
