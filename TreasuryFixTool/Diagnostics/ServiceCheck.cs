using System;
using System.ServiceProcess;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TreasuryFixTool.Diagnostics
{
    /// <summary>
    /// Checks the status of critical Windows services.
    /// </summary>
    public class ServiceCheck : IDiagnosticCheck
    {
        private static readonly string[] CriticalServices = {
            "wuauserv",    // Windows Update
            "Spooler",     // Print Spooler
            "Dnscache",    // DNS Client
            "DHCP",        // DHCP Client
            "BITS",        // Background Intelligent Transfer Service
            "LanmanServer"// Server
        };

        public CheckResult PerformCheck()
        {
            try
            {
                var details = new Dictionary<string, string>();
                int runningCount = 0;
                int stoppedCount = 0;
                int unknownCount = 0;
                var stoppedServices = new List<string>();
                int totalChecked = 0;

                foreach (string serviceName in CriticalServices)
                {
                    try
                    {
                        using var sc = new ServiceController(serviceName);
                        sc.Refresh();
                        details[serviceName] = sc.Status.ToString();
                        totalChecked++;
                        if (sc.Status == ServiceControllerStatus.Running)
                        {
                            runningCount++;
                        }
                        else if (sc.Status == ServiceControllerStatus.Stopped ||
                                 sc.Status == ServiceControllerStatus.StopPending)
                        {
                            stoppedCount++;
                            stoppedServices.Add(serviceName);
                        }
                        else
                        {
                            unknownCount++;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        details[serviceName] = "Not Found / No Access";
                        totalChecked++;
                        unknownCount++;
                    }
                    catch (Exception ex)
                    {
                        details[serviceName] = $"Error: {ex.Message}";
                        totalChecked++;
                        unknownCount++;
                    }
                }

                var result = new CheckResult
                {
                    CheckName = "Service Check",
                    Timestamp = DateTime.Now,
                    Details = details
                };

                if (stoppedCount > 0)
                {
                    result.Status = CheckStatus.Critical;
                    result.Message = $"{stoppedCount} critical service(s) are stopped: {string.Join(", ", stoppedServices)}.";
                }
                else if (unknownCount > 0)
                {
                    result.Status = CheckStatus.Warning;
                    result.Message = $"{unknownCount} service(s) could not be checked. {runningCount} running out of {totalChecked}.";
                }
                else
                {
                    result.Status = CheckStatus.Healthy;
                    result.Message = $"All {runningCount} critical services are running.";
                }

                return result;
            }
            catch (Exception ex)
            {
                return new CheckResult
                {
                    CheckName = "Service Check",
                    Status = CheckStatus.Error,
                    Message = $"Error checking services: {ex.Message}",
                    Timestamp = DateTime.Now
                };
            }
        }
    }
}
