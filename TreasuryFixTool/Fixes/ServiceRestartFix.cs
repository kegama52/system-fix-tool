using System;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace TreasuryFixTool.Fixes
{
    /// <summary>
    /// Fix action to restart critical Windows services.
    /// </summary>
    public class ServiceRestartFix : IFixAction
    {
        public string Name => "Service Restart";
        public string Description => "Restarts critical Windows services that may be stuck or not responding.";

        public async Task<FixResult> ExecuteAsync()
        {
            try
            {
                // Define critical services to restart (example: Windows Update, DHCP, DNS Client)
                string[] servicesToRestart = { "wuauserv", "dhcp", "dnsclients" };
                var restartedServices = new System.Collections.Generic.List<string>();
                var failedServices = new System.Collections.Generic.List<string>();

                foreach (string serviceName in servicesToRestart)
                {
                    ServiceController sc = new ServiceController(serviceName);
                    try
                    {
                        sc.Refresh();
                        if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 0, 30));
                        }
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 30));
                        restartedServices.Add(serviceName);
                    }
                    catch (InvalidOperationException)
                    {
                        failedServices.Add($"{serviceName}: Service not found.");
                    }
                    catch (Exception ex)
                    {
                        failedServices.Add($"{serviceName}: {ex.Message}");
                    }
                }

                string message = "";
                if (restartedServices.Count > 0)
                {
                    message += $"Successfully restarted services: {string.Join(", ", restartedServices)}. ";
                }
                if (failedServices.Count > 0)
                {
                    message += $"Failed to restart services: {string.Join(", ", failedServices)}.";
                }
                if (string.IsNullOrEmpty(message))
                {
                    message = "No services were processed.";
                }

                return new FixResult
                {
                    Success = failedServices.Count == 0,
                    Message = message.Trim(),
                    Details = $"Restarted: {string.Join(", ", restartedServices)}; Failed: {string.Join(", ", failedServices)}"
                };
            }
            catch (Exception ex)
            {
                return new FixResult
                {
                    Success = false,
                    Message = $"Service restart failed: {ex.Message}",
                    Details = ex.ToString()
                };
            }
        }
    }
}