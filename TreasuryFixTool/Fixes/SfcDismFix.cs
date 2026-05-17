using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TreasuryFixTool.PowerShell;

namespace TreasuryFixTool.Fixes
{
    /// <summary>
    /// Fix action to run System File Checker (SFC) and Deployment Image Servicing and Management (DISM).
    /// </summary>
    public class SfcDismFix : IFixAction
    {
        public string Name => "SFC/DISM Repair";
        public string Description => "Runs System File Checker and DISM to repair system files.";

        public async Task<FixResult> ExecuteAsync()
        {
            try
            {
                // We'll run SFC /scannow and then DISM /Online /Cleanup-Image /RestoreHealth
                // Since these require admin and might take time, we run them sequentially.
                // We'll use PowerShell to run these commands and capture output.

                string sfcCommand = "sfc /scannow";
                string dismCommand = "DISM /Online /Cleanup-Image /RestoreHealth";

                string sfcOutput = PowerShellRunner.ExecuteScript(sfcCommand);
                string dismOutput = PowerShellRunner.ExecuteScript(dismCommand);

                // Check if the commands succeeded (we can look for certain strings in the output)
                bool sfcSucceeded = sfcOutput.Contains("Windows Resource Protection did not find any integrity violations") ||
                                    sfcOutput.Contains("Windows Resource Protection found corrupt files and successfully repaired them");
                bool dismSucceeded = dismOutput.Contains("The operation completed successfully.");

                string message = "";
                if (sfcSucceeded && dismSucceeded)
                {
                    message = "SFC and DISM completed successfully.";
                }
                else
                {
                    message = "SFC or DISM encountered issues. Please review the details.";
                }

                return new FixResult
                {
                    Success = sfcSucceeded && dismSucceeded,
                    Message = message,
                    Details = $"SFC Output: {sfcOutput}{Environment.NewLine}DISM Output: {dismOutput}"
                };
            }
            catch (Exception ex)
            {
                return new FixResult
                {
                    Success = false,
                    Message = $"SFC/DISM repair failed: {ex.Message}",
                    Details = ex.ToString()
                };
            }
        }
    }
}