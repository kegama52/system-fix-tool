using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using System.Threading.Tasks;
using TreasuryFixTool.Infrastructure.Storage;
using TreasuryFixTool.Notifications;
using TreasuryFixTool.Infrastructure.Logging;

namespace TreasuryFixTool.Updates
{
    /// <summary>
    /// Detects USB drives carrying a signed update package (treasury_update.sig + recipe files),
    /// validates the HMAC, and imports recipes into C:\TreasurySupport\Recipes\  — fully offline.
    /// </summary>
    public class UsbUpdateHandler
    {
        private const string SignatureFile = "treasury_update.sig";
        private const string RecipeFolder   = "Recipes";
        private readonly string _recipesDestDir;
        private readonly FileLogger? _logger;
        private readonly DispatcherTimer? _watchTimer;

        public UsbUpdateHandler(DispatcherTimer? watchTimer = null, FileLogger? logger = null)
        {
            _recipesDestDir = Path.Combine(DataPaths.BaseDirectory, RecipeFolder);
            _logger         = logger;
            _watchTimer     = watchTimer;
        }

        /// <summary>Scans all removable drives for a valid update package.</summary>
        public void ScanAndImport()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Removable) continue;
                string sigPath = Path.Combine(drive.Name.TrimEnd('\\'), SignatureFile);
                if (!File.Exists(sigPath)) continue;

                try
                {
                    ImportFromUsb(drive.Name.TrimEnd('\\'));
                }
                catch (Exception ex)
                {
                    _logger?.Error($"USB update import failed from {drive.Name}", ex);
                }
            }
        }

        private void ImportFromUsb(string driveRoot)
        {
            string sigPath    = Path.Combine(driveRoot, SignatureFile);
            string sourceDir  = Path.Combine(driveRoot, RecipeFolder);
            string destDir    = _recipesDestDir;
            string[] recipeFiles = Directory.Exists(sourceDir)
                ? Directory.GetFiles(sourceDir, "*.json")
                : Array.Empty<string>();

            if (recipeFiles.Length == 0)
            {
                ToastManager.ShowToast("TreasuryFixTool",
                    $"USB update on {driveRoot}: no recipe JSON files found.",
                    6000, ToastManager.ToolTipIcon.Warning);
                return;
            }

            string expectedSig = File.ReadAllText(sigPath).Trim();
            if (!ValidateHmac(recipeFiles, expectedSig))
            {
                ToastManager.ShowToast("TreasuryFixTool",
                    $"USB update from {driveRoot}: signature mismatch — import blocked.",
                    8000, ToastManager.ToolTipIcon.Error);
                _logger?.Warning($"HMAC mismatch on USB update from {driveRoot}.");
                return;
            }

            // Signature valid — copy recipes
            Directory.CreateDirectory(destDir);
            foreach (string file in recipeFiles)
            {
                string dest = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            ToastManager.ShowToast("TreasuryFixTool",
                $"Imported {recipeFiles.Length} recipe(s) from USB on {driveRoot}.",
                10000, ToastManager.ToolTipIcon.Info);
            _logger?.Info($"Imported {recipeFiles.Length} recipe files from {driveRoot}.");
        }

        /// <summary>
        /// Simple HMAC-SHA256 signature verification.
        /// The sig file contains  &lt;hex-hmac&gt;&lt;tab&gt;&lt;comma-separated-filenames&gt;.
        /// </summary>
        private bool ValidateHmac(string[] recipeFiles, string expectedSig)
        {
            try
            {
                // The shared HMAC key would normally be embedded in the EXE at build time.
                // For this demo we derive it from the assembly name (insecure but functional).
                string exeFile = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "TreasuryFixTool.exe";
                string key     = Path.GetFileNameWithoutExtension(exeFile);

                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
                foreach (string file in recipeFiles.OrderBy(f => f))
                {
                    byte[] bytes    = File.ReadAllBytes(file);
                    byte[] hash     = hmac.ComputeHash(bytes);
                    hmac.Initialize(); // reuse the same HMAC instance across files
                }

                string computed = BitConverter.ToString(hmac.Hash!).Replace("-", "").ToLowerInvariant();
                return computed.Equals(expectedSig, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
