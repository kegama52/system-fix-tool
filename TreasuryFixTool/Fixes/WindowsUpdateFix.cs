using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TreasuryFixTool.PowerShell;

namespace TreasuryFixTool.Fixes
{
    /// <summary>
    /// Fix action to reset Windows Update components.
    /// </summary>
    public class WindowsUpdateFix : IFixAction
    {
        public string Name => "Windows Update Fix";
        public string Description => "Resets Windows Update components and restarts related services.";

        public async Task<FixResult> ExecuteAsync()
        {
            try
            {
                string scriptPath = @"C:\TreasurySoftware\Deploy\Scripts\wufix.ps1";
                if (!System.IO.File.Exists(scriptPath))
                {
                    // We don't have a specific script for Windows Update Fix in the provided list, 
                    // but we can create a generic one or use a combination of commands.
                    // Since the requirement only lists specific scripts, we'll create a simple one here.
                    // Alternatively, we can note that the script is missing and return an error.
                    // However, to meet the requirement, we'll create a placeholder script that does some basic reset.
                    // But note: the user did not ask for a wufix.ps1 in the Scripts folder, so we must not create it.
                    // Instead, we can use the existing scripts or note that we are missing the script.
                    // Since the requirement says to write all source files for the features, and the Scripts folder only lists:
                    //   disk_cleanup.ps1, network_reset.ps1, temp_cleanup.psm1, winsock_reset.ps1
                    // We don't have a Windows Update script. Therefore, we should not rely on a script that doesn't exist.
                    // We'll instead implement the fix using built-in commands via PowerShellRunner.ExecuteScript.
                    // We'll reset the Windows Update services and delete the temporary update files.

                    // We'll stop the services, rename the SoftwareDistribution and Catroot2 folders, then restart services.
                    string commands = @"
# Stop Windows Update related services
net stop wuauserv
net stop cryptSvc
net stop bits
net stop msiserver

# Rename the SoftwareDistribution and Catroot2 folders
ren C:\Windows\SoftwareDistribution SoftwareDistribution.bak
ren C:\Windows\System32\catroot2 Catroot2.bak

# Restart the services
net start wuauserv
net start cryptSvc
net start bits
net start msiserver
";
                    string output = PowerShellRunner.ExecuteScript(commands);
                    return new FixResult
                    {
                        Success = true,
                        Message = "Windows Update components reset successfully.",
                        Details = output
                    };
                }
                else
                {
                    string output = PowerShellRunner.ExecuteScriptFile(scriptPath);
                    return new FixResult
                    {
                        Success = true,
                        Message = "Windows Update fix completed successfully.",
                        Details = output
                    };
                }
            }
            catch (Exception ex)
            {
                return new FixResult
                {
                    Success = false,
                    Message = $"Windows Update fix failed: {ex.Message}",
                    Details = ex.ToString()
                };
            }
        }
    }
}