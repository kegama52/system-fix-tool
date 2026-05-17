using System;
using System.Diagnostics;
using Microsoft.Win32;
using TreasuryFixTool.Infrastructure.Config;
using TreasuryFixTool.Infrastructure.Storage;

namespace TreasuryFixTool.Monitoring
{
    /// <summary>
    /// Registers TreasuryFixTool to start with Windows (HKCU Run key).
    /// Also manages a simple Windows Scheduled Task for the hourly health check.
    /// </summary>
    public static class ScheduledTaskManager
    {
        private const string  RegKeyPath     = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string  RegValueName   = "TreasuryFixTool";
        private const string  TaskName       = "TreasuryFixTool Hourly Scan";
        private static readonly string ExePath = System.IO.Path.Combine(
            DataPaths.DeployDirectory, "TreasuryFixTool.exe");

        /// <summary>
        /// Enables or disables the "start with Windows" registry entry.
        /// </summary>
        public static void SetStartWithWindows(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, true)
                            ?? Registry.CurrentUser.CreateSubKey(RegKeyPath);
                if (enable && System.IO.File.Exists(ExePath))
                    key?.SetValue(RegValueName, $"\"{ExePath}\" /silent-start");
                else
                    key?.DeleteValue(RegValueName, throwOnMissingValue: false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ScheduledTaskManager.SetStartWithWindows error: {ex}");
            }
        }

        /// <summary>
        /// Returns true if the registry entry is present.
        /// </summary>
        public static bool IsStartWithWindowsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath);
                return key?.GetValue(RegValueName) is not null;
            }
            catch { return false; }
        }

        /// <summary>
        /// Creates a Windows Scheduled Task (if schtasks.exe is available)
        /// to run the hourly health check silently.
        /// </summary>
        public static bool CreateHourlyScanTask()
        {
            try
            {
                if (!System.IO.File.Exists(ExePath)) return false;

                var psi = new ProcessStartInfo
                {
                    FileName               = "schtasks.exe",
                    Arguments              = $"/Create /TN \"{TaskName}\" /TR \"\\\"{ExePath}\\\" /silent-start\" /SC HOURLY /RL LIMITED /F",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit();
                return proc?.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ScheduledTaskManager.CreateHourlyScanTask error: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Removes the hourly scan scheduled task.
        /// </summary>
        public static bool RemoveHourlyScanTask()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "schtasks.exe",
                    Arguments              = $"/Delete /TN \"{TaskName}\" /F",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit();
                return proc?.ExitCode == 0;
            }
            catch { return false; }
        }
    }
}
