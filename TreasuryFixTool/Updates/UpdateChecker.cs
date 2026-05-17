using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using TreasuryFixTool.Infrastructure.Logging;

namespace TreasuryFixTool.Updates
{
    public class UpdateInfo
    {
        public string Version { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string Changelog { get; set; } = string.Empty;
        public DateTime Published { get; set; }
    }

    public class UpdateChecker
    {
        private readonly HttpClient _http;
        private readonly FileLogger _logger;
        private const string UpdateEndpoint = "https://api.github.com/repos/NationalTreasury/TreasuryFixTool/releases/latest";

        public UpdateChecker()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Add("User-Agent", "TreasuryFixTool");
            _logger = new FileLogger(System.IO.Path.Combine(
                Infrastructure.Storage.DataPaths.LogsDirectory, "Updates.log"));
        }

        public async Task<UpdateInfo?> CheckForUpdateAsync(string currentVersion)
        {
            try
            {
                string json = await _http.GetStringAsync(UpdateEndpoint);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string latestVersion = root.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "";
                if (IsNewerVersion(latestVersion, currentVersion))
                {
                    return new UpdateInfo
                    {
                        Version = latestVersion,
                        DownloadUrl = root.GetProperty("html_url").GetString() ?? "",
                        Changelog = root.GetProperty("body").GetString() ?? "",
                        Published = root.GetProperty("published_at").GetDateTime()
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Update check failed.", ex);
            }
            return null;
        }

        private static bool IsNewerVersion(string latest, string current)
        {
            if (string.IsNullOrEmpty(latest) || string.IsNullOrEmpty(current))
                return false;
            var latestParts = latest.Split('.');
            var currentParts = current.Split('.');
            for (int i = 0; i < Math.Max(latestParts.Length, currentParts.Length); i++)
            {
                int latestNum = i < latestParts.Length && int.TryParse(latestParts[i], out int l) ? l : 0;
                int currentNum = i < currentParts.Length && int.TryParse(currentParts[i], out int c) ? c : 0;
                if (latestNum > currentNum) return true;
                if (latestNum < currentNum) return false;
            }
            return false;
        }
    }
}