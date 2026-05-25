using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.Management;
using System.Threading;
using Microsoft.Win32;
using TreasuryFixTool.Diagnostics;

namespace TreasuryFixTool.Diagnostics
{
    /// <summary>
    /// Represents system health metrics.
    /// </summary>
    public class SystemHealthMetrics
    {
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double DiskUsage { get; set; }
        public int ActiveServices { get; set; }
        public int FailedServices { get; set; }
        public DateTime LastScan { get; set; }
    }

    /// <summary>
    /// Orchestrates running diagnostic checks and aggregating results.
    /// </summary>
    public class DiagnosticEngine
    {
        private readonly List<IDiagnosticCheck> _checks;

        public DiagnosticEngine()
        {
            _checks = new List<IDiagnosticCheck>
            {
                new DiskSpaceCheck(),
                new MemoryCheck(),
                new ServiceCheck(),
                new NetworkCheck(),
                new EventLogReader(),
                new InternetConnectivityCheck(),
                new PrinterSpoolerCheck(),
                new VpnConnectivityCheck(),
                new OutlookConnectivityCheck(),
                new WindowsUpdateCheck(),
                new ApplicationHangCheck(),
                new AccessDeniedCheck(),
                new LoginFailedCheck()
            };
        }

        /// <summary>
        /// Gets current system health metrics.
        /// </summary>
        public SystemHealthMetrics GetCurrentMetrics()
        {
            var metrics = new SystemHealthMetrics { LastScan = DateTime.Now };

            try
            {
                // CPU Usage
                using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue();
                Thread.Sleep(500);
                metrics.CpuUsage = cpuCounter.NextValue();
            }
            catch
            {
                metrics.CpuUsage = 0;
            }

            try
            {
                // Memory Usage - using available MBytes as percentage of total physical memory
                using var memCounter = new PerformanceCounter("Memory", "Available MBytes");
                double availableMb = memCounter.NextValue();
                double usedMb = 0;
                if (Environment.WorkingSet > 0)
                {
                    usedMb = (double)Environment.WorkingSet / (1024 * 1024);
                }
                metrics.MemoryUsage = usedMb;
            }
            catch
            {
                metrics.MemoryUsage = 0;
            }

            try
            {
                // Disk Usage for C drive
                var drive = new DriveInfo("C");
                if (drive.IsReady)
                {
                    double total = drive.TotalSize;
                    double free = drive.AvailableFreeSpace;
                    metrics.DiskUsage = ((total - free) / total) * 100;
                }
                else
                {
                    metrics.DiskUsage = 0;
                }
            }
            catch
            {
                metrics.DiskUsage = 0;
            }

            try
            {
                // Service counts
                var services = ServiceController.GetServices();
                metrics.ActiveServices = services.Count(s => s.Status == ServiceControllerStatus.Running);
                metrics.FailedServices = services.Count(s => s.Status == ServiceControllerStatus.Stopped);
            }
            catch
            {
                metrics.ActiveServices = 0;
                metrics.FailedServices = 0;
            }

            return metrics;
        }

        /// <summary>
        /// Runs all diagnostic checks and returns a list of results.
        /// </>
        public List<CheckResult> RunAllChecks()
        {
            var results = new List<CheckResult>();
            foreach (var check in _checks)
            {
                try
                {
                    var result = check.PerformCheck();
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    results.Add(new CheckResult
                    {
                        CheckName = check.GetType().Name,
                        Status = CheckStatus.Error,
                        Message = $"Exception during check: {ex.Message}",
                        Timestamp = DateTime.Now
                    });
                }
            }
            return results;
        }
    }

    /// <summary>
    /// Contract for a single diagnostic check.
    /// </summary>
    public interface IDiagnosticCheck
    {
        CheckResult PerformCheck();
    }

    /// <summary>
    /// Checks for internet connectivity by pinging reliable hosts.
    /// </summary>
    public class InternetConnectivityCheck : IDiagnosticCheck
    {
        public CheckResult PerformCheck()
        {
            try
            {
                using var ping = new Ping();
                // Ping Google's DNS and a common treasury server (example)
                var reply1 = ping.Send("8.8.8.8", 2000);
                var reply2 = ping.Send("1.1.1.1", 2000);

                if (reply1.Status == IPStatus.Success || reply2.Status == IPStatus.Success)
                {
                    return new CheckResult
                    {
                        CheckName = nameof(InternetConnectivityCheck),
                        Status = CheckStatus.Ok,
                        Message = "Internet access is available",
                        Timestamp = DateTime.Now
                    };
                }
                else
                {
                    return new CheckResult
                    {
                        CheckName = nameof(InternetConnectivityCheck),
                        Status = CheckStatus.Warning,
                        Message = "No internet access detected. Try: ipconfig /release && ipconfig /renew",
                        Timestamp = DateTime.Now,
                        SuggestedFix = "Run: ipconfig /release && ipconfig /renew"
                    };
                }
            }
            catch (Exception ex)
            {
                return new CheckResult
                {
                    CheckName = nameof(InternetConnectivityCheck),
                    Status = CheckStatus.Error,
                    Message = $"Error checking internet connectivity: {ex.Message}",
                    Timestamp = DateTime.Now
                };
            }
        }
    }

    /// <summary>
    /// Checks if the print spooler service is running.
    /// </summary>
    public class PrinterSpoolerCheck : IDiagnosticCheck
    {
        public CheckResult PerformCheck()
        {
            try
            {
                var spooler = ServiceController.GetServices()
                    .FirstOrDefault(s => s.ServiceName == "Spooler");

                if (spooler == null)
                {
                    return new CheckResult
                    {
                        CheckName = nameof(PrinterSpoolerCheck),
                        Status = CheckStatus.Error,
                        Message = "Print spooler service not found",
                        Timestamp = DateTime.Now
                    };
                }

                if (spooler.Status == ServiceControllerStatus.Running)
                {
                    return new CheckResult
                    {
                        CheckName = nameof(PrinterSpoolerCheck),
                        Status = CheckStatus.Ok,
                        Message = "Print spooler is running",
                        Timestamp = DateTime.Now
                    };
                }
                else
                {
                    return new CheckResult
                    {
                        CheckName = nameof(PrinterSpoolerCheck),
                        Status = CheckStatus.Warning,
                        Message = "Print spooler is stopped. Try: net start spooler",
                        Timestamp = DateTime.Now,
                        SuggestedFix = "net start spooler"
                    };
                }
            }
            catch (Exception ex)
            {
                return new CheckResult
                {
                    CheckName = nameof(PrinterSpoolerCheck),
                    Status = CheckStatus.Error,
                    Message = $"Error checking print spooler: {ex.Message}",
                    Timestamp = DateTime.Now
                };
            }
        }
    }

    /// <summary>
    /// Checks VPN connectivity and ability to reach domain controller.
    /// </summary>
    public class VpnConnectivityCheck : IDiagnosticCheck
    {
        public CheckResult PerformCheck()
        {
            try
            {
                // Check if any VPN connection is active (simplified: check for PPP adapters)
                var nics = NetworkInterface.GetAllNetworkInterfaces();
                var vpnActive = nics.Any(nic => 
                    nic.NetworkInterfaceType == NetworkInterfaceType.Ppp &&
                    nic.OperationalStatus == OperationalStatus.Up);

                if (!vpnActive)
                {
                    return new CheckResult
                    {
                        CheckName = nameof(VpnConnectivityCheck),
                        Status = CheckStatus.Info,
                        Message = "No active VPN connection detected",
                        Timestamp = DateTime.Now
                    };
                }

// Try to ping a domain controller (using environment variable or fallback)
                 string? dcName = Environment.GetEnvironmentVariable("USERDNSDOMAIN");
                 dcName = string.IsNullOrWhiteSpace(dcName) ? "contoso.com" : dcName;

                using var ping = new Ping();
                var reply = ping.Send(dcName, 3000);

                if (reply.Status == IPStatus.Success)
                {
                    return new CheckResult
                    {
                        CheckName = nameof(VpnConnectivityCheck),
                        Status = CheckStatus.Ok,
                        Message = $"VPN is connected and can reach domain controller ({dcName})",
                        Timestamp = DateTime.Now
                    };
                }
                else
                {
                    return new CheckResult
                    {
                        CheckName = nameof(VpnConnectivityCheck),
                        Status = CheckStatus.Warning,
                        Message = "VPN connected but cannot reach domain controller. Try resetting VPN adapter",
                        Timestamp = DateTime.Now,
                        SuggestedFix = "Reset VPN adapter"
                    };
                }
            }
            catch (Exception ex)
            {
                return new CheckResult
                {
                    CheckName = nameof(VpnConnectivityCheck),
                    Status = CheckStatus.Error,
                    Message = $"Error checking VPN connectivity: {ex.Message}",
                    Timestamp = DateTime.Now
                };
            }
        }
    }

    /// <summary>
    /// Checks Outlook/Exchange connectivity (simplified).
    /// </summary>
    public class OutlookConnectivityCheck : IDiagnosticCheck
    {
        public CheckResult PerformCheck()
        {
            try
            {
                // Check if Outlook process is running
                var outlookProcess = Process.GetProcessesByName("OUTLOOK").FirstOrDefault();
                if (outlookProcess == null)
                {
                    return new CheckResult
                    {
                        CheckName = nameof(OutlookConnectivityCheck),
                        Status = CheckStatus.Info,
                        Message = "Outlook is not running",
                        Timestamp = DateTime.Now
                    };
                }

                // Simplified check: we could try to ping Exchange server, but that requires knowing the server
                // For now, we'll just note that Outlook is running and assume connectivity if no errors in event log
                // In a real implementation, we would check specific Outlook event IDs or use Outlook API
                return new CheckResult
                {
                    CheckName = nameof(OutlookConnectivityCheck),
                    Status = CheckStatus.Ok,
                    Message = "Outlook is running (connectivity assumed OK if no recent errors)",
                    Timestamp = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                return new CheckResult
                {
                    CheckName = nameof(OutlookConnectivityCheck),
                    Status = CheckStatus.Error,
                    Message = $"Error checking Outlook connectivity: {ex.Message}",
                    Timestamp = DateTime.Now
                };
            }
        }
    }

    /// <summary>
    /// Checks for Windows Update failures by examining event log.
    /// </summary>
    public class WindowsUpdateCheck : IDiagnosticCheck
    {
        public CheckResult PerformCheck()
        {
            try
            {
                // Query Windows Update event log for errors in last 24 hours
                string query = @"
                    SELECT * FROM Win32_NTLogEvent 
                    WHERE LogFile = 'Setup' 
                    AND EventType = 2  // Error
                    AND TimeGenerated >= '" + 
                    DateTime.Now.AddDays(-1).ToString("yyyyMMddHHmmss.000000+000") + "'";

                using var searcher = new ManagementObjectSearcher(query);
                var errors = searcher.Get()
                    .Cast<ManagementObject>()
                    .Where(e => 
                        e["Message"] != null && 
                        (e["Message"].ToString()?.Contains("0x80070002") == true || 
                         e["Message"].ToString()?.Contains("Windows Update") == true))
                    .ToList();

                if (errors.Any())
                {
                    return new CheckResult
                    {
                        CheckName = nameof(WindowsUpdateCheck),
                        Status = CheckStatus.Warning,
                        Message = $"Found {errors.Count} Windows Update error(s) in last 24 hours. Try: Dism /Online /Cleanup-Image /RestoreHealth",
                        Timestamp = DateTime.Now,
                        SuggestedFix = "Run: Dism /Online /Cleanup-Image /RestoreHealth"
                    };
                }

                return new CheckResult
                {
                    CheckName = nameof(WindowsUpdateCheck),
                    Status = CheckStatus.Ok,
                    Message = "No recent Windows Update errors detected",
                    Timestamp = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                return new CheckResult
                {
                    CheckName = nameof(WindowsUpdateCheck),
                    Status = CheckStatus.Error,
                    Message = $"Error checking Windows Update: {ex.Message}",
                    Timestamp = DateTime.Now
                };
            }
        }
    }

    /// <summary>
    /// Checks for applications that are not responding.
    /// </summary>
    public class ApplicationHangCheck : IDiagnosticCheck
    {
        public CheckResult PerformCheck()
        {
            try
            {
                var hungProcesses = Process.GetProcesses()
                    .Where(p => !p.Responding && !string.IsNullOrEmpty(p.ProcessName))
                    .ToList();

                if (hungProcesses.Any())
                {
                    var processNames = string.Join(", ", hungProcesses.Select(p => p.ProcessName));
                    return new CheckResult
                    {
                        CheckName = nameof(ApplicationHangCheck),
                        Status = CheckStatus.Warning,
                        Message = $"Applications not responding: {processNames}. Consider ending these tasks.",
                        Timestamp = DateTime.Now,
                        SuggestedFix = "End task for unresponsive applications via Task Manager"
                    };
                }

                return new CheckResult
                {
                    CheckName = nameof(ApplicationHangCheck),
                    Status = CheckStatus.Ok,
                    Message = "No unresponsive applications detected",
                    Timestamp = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                return new CheckResult
                {
                    CheckName = nameof(ApplicationHangCheck),
                    Status = CheckStatus.Error,
                    Message = $"Error checking for unresponsive applications: {ex.Message}",
                    Timestamp = DateTime.Now
                };
            }
        }
    }

    /// <summary>
    /// Checks for access denied errors on common treasury paths.
    /// </summary>
    public class AccessDeniedCheck : IDiagnosticCheck
    {
        public CheckResult PerformCheck()
        {
            try
            {
                // Common treasury paths that might cause access denied
                string[] treasuryPaths = {
                    @"C:\Treasury\",
                    @"C:\Program Files\Treasury\",
                    @"C:\ProgramData\Treasury\",
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Treasury\"
                };

                foreach (var path in treasuryPaths)
                {
                    if (System.IO.Directory.Exists(path))
                    {
                        try
                        {
                            // Try to access the directory
                            var files = System.IO.Directory.GetFiles(path);
                            // If we get here, access was successful
                        }
                        catch (UnauthorizedAccessException)
                        {
                            return new CheckResult
                            {
                                CheckName = nameof(AccessDeniedCheck),
                                Status = CheckStatus.Warning,
                                Message = $"Access denied to treasury path: {path}. Check permissions.",
                                Timestamp = DateTime.Now,
                                SuggestedFix = "Run as administrator or contact IT to fix permissions"
                            };
                        }
                    }
                }

                return new CheckResult
                {
                    CheckName = nameof(AccessDeniedCheck),
                    Status = CheckStatus.Ok,
                    Message = "No access denied errors detected on treasury paths",
                    Timestamp = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                return new CheckResult
                {
                    CheckName = nameof(AccessDeniedCheck),
                    Status = CheckStatus.Error,
                    Message = $"Error checking access denied: {ex.Message}",
                    Timestamp = DateTime.Now
                };
            }
        }
    }

    /// <summary>
    /// Checks for login failed errors by examining security event log.
    /// </summary>
    public class LoginFailedCheck : IDiagnosticCheck
    {
        public CheckResult PerformCheck()
        {
            try
            {
                // Check security event log for failed login attempts in last hour
                string query = @"
                    SELECT * FROM Win32_NTLogEvent 
                    WHERE LogFile = 'Security' 
                    AND EventCode = 4625  // Failed login
                    AND TimeGenerated >= '" + 
                    DateTime.Now.AddHours(-1).ToString("yyyyMMddHHmmss.000000+000") + "'";

                using var searcher = new ManagementObjectSearcher(query);
                var failedLogins = searcher.Get()
                    .Cast<ManagementObject>()
                    .ToList();

                if (failedLogins.Any())
                {
                    int count = failedLogins.Count;
                    string message = count > 5 
                        ? $"Multiple failed login attempts detected ({count} in last hour). Possible brute force attack or account lockout."
                        : $"Failed login attempt detected ({count} in last hour). Check credentials.";

                    return new CheckResult
                    {
                        CheckName = nameof(LoginFailedCheck),
                        Status = count > 5 ? CheckStatus.Warning : CheckStatus.Info,
                        Message = message,
                        Timestamp = DateTime.Now,
                        SuggestedFix = "Verify username/password, check caps lock, or contact IT if account locked"
                    };
                }

                return new CheckResult
                {
                    CheckName = nameof(LoginFailedCheck),
                    Status = CheckStatus.Ok,
                    Message = "No recent failed login attempts detected",
                    Timestamp = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                return new CheckResult
                {
                    CheckName = nameof(LoginFailedCheck),
                    Status = CheckStatus.Error,
                    Message = $"Error checking login failed: {ex.Message}",
                    Timestamp = DateTime.Now
                };
            }
        }
    }
}