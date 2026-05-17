using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.ObjectModel;

namespace TreasuryFixTool.PowerShell
{
    /// <summary>
    /// Executes PowerShell scripts and returns the output.
    /// Uses the System.Management.Automation assembly bundled with Windows PowerShell.
    /// </summary>
    public static class PowerShellRunner
    {
        private static readonly string _powershellExe = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell\\v1.0\\powershell.exe"
        );

        /// <summary>
        /// Executes a PowerShell script and returns the output as a string.
        /// </summary>
        /// <param name="scriptContent">The PowerShell script content to execute.</param>
        /// <param name="requiresAdmin">Set to true to run the command elevated via runas verb.</param>
        /// <returns>The output of the script.</returns>
        public static string ExecuteScript(string scriptContent, bool requiresAdmin = false)
        {
            if (string.IsNullOrWhiteSpace(scriptContent))
                throw new ArgumentException("Script content cannot be null or empty.", nameof(scriptContent));

            try
            {
                // Use the Native PowerShell runner via ProcessStartInfo for maximum Windows compatibility
                var psi = new ProcessStartInfo
                {
                    FileName = _powershellExe,
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{scriptContent.Replace("\"", "`\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("Failed to start PowerShell.");
                string output = proc.StandardOutput.ReadToEnd();
                string errors = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(output))
                    sb.AppendLine(output);
                if (!string.IsNullOrWhiteSpace(errors))
                    sb.AppendLine("STDERR: ").Append(errors);

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Executes a PowerShell script from a file and returns the output as a string.
        /// </summary>
        /// <param name="scriptPath">The full path to the PowerShell script file.</param>
        /// <param name="requiresAdmin">Set to true to run the command elevated via runas verb.</param>
        /// <returns>The output of the script.</returns>
        public static string ExecuteScriptFile(string scriptPath, bool requiresAdmin = false)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
                throw new ArgumentException("Script path cannot be null or empty.", nameof(scriptPath));

            if (!System.IO.File.Exists(scriptPath))
                throw new FileNotFoundException("Script file not found.", scriptPath);

            string scriptContent = System.IO.File.ReadAllText(scriptPath);
            return ExecuteScript(scriptContent, requiresAdmin);
        }

        /// <summary>
        /// Executes an elevated PowerShell command (with UAC prompt) and returns the output.
        /// </summary>
        /// <param name="scriptContent">The PowerShell script to run elevated.</param>
        /// <returns>The output of the script.</returns>
        public static string ExecuteElevated(string scriptContent)
        {
            if (string.IsNullOrWhiteSpace(scriptContent))
                throw new ArgumentException("Script content cannot be null or empty.", nameof(scriptContent));

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _powershellExe,
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{scriptContent.Replace("\"", "`\"")}\"",
                    Verb = "runas",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                using var proc = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("Failed to start elevated PowerShell.");
                string output = proc.StandardOutput.ReadToEnd();
                string errors = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(output))
                    sb.AppendLine(output);
                if (!string.IsNullOrWhiteSpace(errors))
                    sb.AppendLine("STDERR: ").Append(errors);

                return sb.ToString();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // User cancelled UAC prompt
                return $"UAC prompt was dismissed or denied: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
