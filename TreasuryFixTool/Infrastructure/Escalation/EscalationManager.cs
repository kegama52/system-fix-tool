using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TreasuryFixTool.Diagnostics;
using TreasuryFixTool.Infrastructure.Logging;

namespace TreasuryFixTool.Infrastructure.Escalation
{
    /// <summary>
    /// Creates and stores escalation reports on disk after a fix has failed.
    /// </summary>
    public class EscalationManager
    {
        private readonly string              _escalationsDirectory;
        private readonly FileLogger          _logger;

        public EscalationManager(string escalationsDirectory, FileLogger logger)
        {
            _escalationsDirectory = escalationsDirectory ?? throw new ArgumentNullException(nameof(escalationsDirectory));
            _logger              = logger              ?? throw new ArgumentNullException(nameof(logger));

            if (!Directory.Exists(_escalationsDirectory))
                Directory.CreateDirectory(_escalationsDirectory);
        }

        /// <summary>
        /// Creates a JSON escalation report from failed diagnostic results.
        /// </summary>
        /// <param name="results">All diagnostic results for this run.</param>
        /// <param name="failedFixNames">Names of any fixes that could not be applied.</param>
        /// <returns>Full path of the written escalation file.</returns>
        public string CreateEscalation(List<CheckResult> results, List<string>? failedFixNames = null)
        {
            try
            {
                var data = new
                {
                    Timestamp       = DateTime.Now,
                    MachineName     = Environment.MachineName,
                    UserName        = Environment.UserName,
                    OsVersion       = Environment.OSVersion.ToString(),
                    Results         = results,
                    FailedFixes     = failedFixNames ?? new List<string>()
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

                string fileName = $"TreasuryFix_Escalation_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string filePath = Path.Combine(_escalationsDirectory, fileName);
                File.WriteAllText(filePath, json);

                _logger.Info($"Escalation report created: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to create escalation report.", ex);
                throw;
            }
        }
    }
}
