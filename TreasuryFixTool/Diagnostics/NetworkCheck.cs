using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace TreasuryFixTool.Diagnostics
{
    /// <summary>
    /// Checks the network connectivity and adapter status.
    /// </summary>
    public class NetworkCheck : IDiagnosticCheck
    {
        /// <summary>
        /// Performs the network check.
        /// </summary>
        /// <returns>A CheckResult indicating the status of network connectivity.</returns>
        public CheckResult PerformCheck()
        {
            try
            {
                bool isNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();
                var adapters = NetworkInterface.GetAllNetworkInterfaces();
                int upAdapters = 0;
                int totalAdapters = adapters.Length;
                var adapterDetails = new Dictionary<string, string>();

                foreach (var adapter in adapters)
                {
                    // Only consider non-virtual adapters that are operational
                    if (adapter.OperationalStatus == OperationalStatus.Up &&
                        adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        adapter.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    {
                        upAdapters++;
                        adapterDetails[adapter.Name] = $"Speed: {adapter.Speed / 1_000_000} Mbps, Status: {adapter.OperationalStatus}";
                    }
                }

                var result = new CheckResult
                {
                    CheckName = "Network Check",
                    Timestamp = DateTime.Now,
                    Details = adapterDetails
                };

                if (!isNetworkAvailable)
                {
                    result.Status = CheckStatus.Critical;
                    result.Message = "No network connectivity detected.";
                }
                else if (upAdapters == 0)
                {
                    result.Status = CheckStatus.Critical;
                    result.Message = "Network adapters are present but none are operational.";
                }
                else if (upAdapters < totalAdapters)
                {
                    result.Status = CheckStatus.Warning;
                    result.Message = $"Some network adapters are down ({upAdapters} of {totalAdapters} operational).";
                }
                else
                {
                    result.Status = CheckStatus.Healthy;
                    result.Message = $"Network is healthy ({upAdapters} adapters operational).";
                }

                return result;
            }
            catch (Exception ex)
            {
                return new CheckResult
                {
                    CheckName = "Network Check",
                    Status = CheckStatus.Error,
                    Message = $"Error checking network: {ex.Message}",
                    Timestamp = DateTime.Now
                };
            }
        }
    }
}