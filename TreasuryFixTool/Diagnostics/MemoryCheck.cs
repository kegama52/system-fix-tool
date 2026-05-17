using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace TreasuryFixTool.Diagnostics
{
    /// <summary>
    /// Reads Windows memory stats using the native GlobalMemoryStatusEx API.
    /// No System.Management / WMI required.
    /// </summary>
    public class MemoryCheck : IDiagnosticCheck
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        public CheckResult PerformCheck()
        {
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                if (!GlobalMemoryStatusEx(memStatus))
                    throw new InvalidOperationException("GlobalMemoryStatusEx failed.");

                double totalGB = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                double availGB = memStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
                double usedPct = ((double)(memStatus.ullTotalPhys - memStatus.ullAvailPhys) / memStatus.ullTotalPhys) * 100.0;

                var result = new CheckResult
                {
                    CheckName = "Memory Check",
                    Timestamp = DateTime.Now,
                    Details   = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "Total RAM (GB)",   totalGB.ToString("F2") },
                        { "Available RAM (GB)", availGB.ToString("F2") },
                        { "Memory Used (%)",  usedPct.ToString("F2") }
                    }
                };

                if (usedPct > 90)
                {
                    result.Status = CheckStatus.Critical;
                    result.Message = $"Critically high memory usage: {usedPct:F1}% ({availGB:F2} GB free of {totalGB:F2} GB).";
                }
                else if (usedPct > 80)
                {
                    result.Status = CheckStatus.Warning;
                    result.Message = $"High memory usage: {usedPct:F1}% ({availGB:F2} GB free of {totalGB:F2} GB).";
                }
                else
                {
                    result.Status = CheckStatus.Healthy;
                    result.Message = $"Memory healthy: {usedPct:F1}% used, {availGB:F2} GB free.";
                }

                return result;
            }
            catch (Exception ex)
            {
                return new CheckResult
                {
                    CheckName  = "Memory Check",
                    Status     = CheckStatus.Error,
                    Message    = $"Error reading memory: {ex.Message}",
                    Timestamp  = DateTime.Now
                };
            }
        }
    }
}
