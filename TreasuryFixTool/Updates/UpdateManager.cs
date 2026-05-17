using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Diagnostics;
using System.Linq;
using TreasuryFixTool.Infrastructure.Config;
using TreasuryFixTool.Infrastructure.Storage;
using TreasuryFixTool.Monitoring;


namespace TreasuryFixTool.Updates
{
    /// <summary>
    /// Manages application-level settings: persistence, registry start-up entry,
    /// and versioning for TreasuryFixTool.
    /// </summary>
    public class UpdateManager
    {
        private readonly string _configPath;

        public UpdateManager(string? configPath = null)
        {
            _configPath = configPath ?? Path.Combine(DataPaths.BaseDirectory, "Config", "user_config.json");
        }

        /// <summary>Loads the persisted AppConfig from disk.</summary>
        public AppConfig LoadConfig()
        {
            try
            {
                if (!File.Exists(_configPath)) return new AppConfig();
                string json = File.ReadAllText(_configPath);
                return System.Text.Json.JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch { return new AppConfig(); }
        }

        /// <summary>Persists the AppConfig to disk.</summary>
        public void SaveConfig(AppConfig config)
        {
            try
            {
                string dir = Path.GetDirectoryName(_configPath)!;
                Directory.CreateDirectory(dir);
                string json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateManager.SaveConfig error: {ex}");
            }
        }

        /// <summary>Registers the current EXE to start with Windows (HKCU Run key).</summary>
        public void EnableAutoStart()
        {
            ScheduledTaskManager.SetStartWithWindows(true);
        }

        /// <summary>Removes the auto-start registry entry.</summary>
        public void DisableAutoStart()
        {
            ScheduledTaskManager.SetStartWithWindows(false);
        }
    }
}
