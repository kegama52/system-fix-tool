using System;
using System.Collections.Generic;
using System.IO;

namespace TreasuryFixTool.Diagnostics
{
    /// <summary>
    /// Checks the available disk space on the system drive.
    /// </summary>
    public class DiskSpaceCheck : IDiagnosticCheck
    {
        /// <summary>
        /// Performs the disk space check.
        /// </summary>
        /// <returns>A CheckResult indicating the status of the disk space.</returns>
        public CheckResult PerformCheck()
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
                double freeSpaceGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                double totalSpaceGB = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                double usedPercentage = (1 - (drive.AvailableFreeSpace / (double)drive.TotalSize)) * 100;

                var result = new CheckResult
                {
                    CheckName = "Disk Space Check",
                    Timestamp = DateTime.Now,
                    Details = new Dictionary<string, string>
                    {
                        { "Total Space (GB)", totalSpaceGB.ToString("F2") },
                        { "Free Space (GB)", freeSpaceGB.ToString("F2") },
                        { "Used Percentage", usedPercentage.ToString("F2") + "%" }
                    }
                };

                if (freeSpaceGB < 10) // Less than 10 GB free
                {
                    result.Status = CheckStatus.Critical;
                    result.Message = $"Critically low disk space: {freeSpaceGB:F2} GB free.";
                }
                else if (freeSpaceGB < 20) // Less than 20 GB free
                {
                    result.Status = CheckStatus.Warning;
                    result.Message = $"Low disk space: {freeSpaceGB:F2} GB free.";
                }
                else
                {
                    result.Status = CheckStatus.Healthy;
                    result.Message = $"Disk space healthy: {freeSpaceGB:F2} GB free.";
                }

                return result;
            }
            catch (Exception ex)
            {
                return new CheckResult
                {
                    CheckName = "Disk Space Check",
                    Status = CheckStatus.Error,
                    Message = $"Error checking disk space: {ex.Message}",
                    Timestamp = DateTime.Now
                };
            }
        }
    }
}