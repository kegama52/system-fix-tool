using System;
using System.IO;
using System.Threading.Tasks;
using TreasuryFixTool.PowerShell;

namespace TreasuryFixTool.Fixes
{
    /// <summary>
    /// Fix action to clean temporary files using PowerShell script.
    /// </summary>
    public class TempFilesFix : IFixAction
    {
        public string Name => "Temp Files Cleanup";
        public string Description => "Clears user and system temporary files.";

        public async Task<FixResult> ExecuteAsync()
        {
            try
            {
                string scriptPath = @"C:\TreasurySoftware\Deploy\Scripts\temp_cleanup.ps1";
                if (!System.IO.File.Exists(scriptPath))
                {
                    scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"PowerShell\Scripts\temp_cleanup.ps1");
                }

                string output = PowerShellRunner.ExecuteScriptFile(scriptPath);
                return new FixResult
                {
                    Success = true,
                    Message = "Temporary files cleanup completed successfully.",
                    Details = output
                };
            }
            catch (Exception ex)
            {
                return new FixResult
                {
                    Success = false,
                    Message = $"Temp files cleanup failed: {ex.Message}",
                    Details = ex.ToString()
                };
            }
        }
    }
}