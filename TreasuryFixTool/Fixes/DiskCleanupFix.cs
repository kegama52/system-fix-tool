using System;
using System.Threading.Tasks;
using TreasuryFixTool.PowerShell;

namespace TreasuryFixTool.Fixes
{
    /// <summary>
    /// Fix action to perform disk cleanup using PowerShell script.
    /// </summary>
    public class DiskCleanupFix : IFixAction
    {
        public string Name => "Disk Cleanup";
        public string Description => "Cleans temporary files and runs Disk Cleanup utility.";

        public async Task<FixResult> ExecuteAsync()
        {
            try
            {
                string scriptPath = @"C:\TreasurySoftware\Deploy\Scripts\disk_cleanup.ps1";
                // If the script doesn't exist in the deploy folder, use the one in the project
                if (!System.IO.File.Exists(scriptPath))
                {
                    scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"PowerShell\Scripts\disk_cleanup.ps1");
                }

                string output = PowerShellRunner.ExecuteScriptFile(scriptPath);
                return new FixResult
                {
                    Success = true,
                    Message = "Disk cleanup completed successfully.",
                    Details = output
                };
            }
            catch (Exception ex)
            {
                return new FixResult
                {
                    Success = false,
                    Message = $"Disk cleanup failed: {ex.Message}",
                    Details = ex.ToString()
                };
            }
        }
    }
}