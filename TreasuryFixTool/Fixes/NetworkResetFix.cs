using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TreasuryFixTool.PowerShell;

namespace TreasuryFixTool.Fixes
{
    /// <summary>
    /// Fix action to reset network adapters using PowerShell script.
    /// </summary>
    public class NetworkResetFix : IFixAction
    {
        public string Name => "Network Reset";
        public string Description => "Resets network adapters and TCP/IP stack.";

        public async Task<FixResult> ExecuteAsync()
        {
            try
            {
                string scriptPath = @"C:\TreasurySoftware\Deploy\Scripts\network_reset.ps1";
                if (!System.IO.File.Exists(scriptPath))
                {
                    scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"PowerShell\Scripts\network_reset.ps1");
                }

                string output = PowerShellRunner.ExecuteScriptFile(scriptPath);
                return new FixResult
                {
                    Success = true,
                    Message = "Network reset completed successfully.",
                    Details = output
                };
            }
            catch (Exception ex)
            {
                return new FixResult
                {
                    Success = false,
                    Message = $"Network reset failed: {ex.Message}",
                    Details = ex.ToString()
                };
            }
        }
    }
}