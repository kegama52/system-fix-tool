using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace TreasuryFixTool.Diagnostics
{
    /// <summary>
    /// Reads recent entries from the Windows Event Log (System and Application) for errors and warnings.
    /// </summary>
    public class EventLogReader : IDiagnosticCheck
    {
        /// <summary>
        /// Performs the event log check.
        /// </summary>
        /// <returns>A CheckResult indicating the status based on recent event log entries.</returns>
        public CheckResult PerformCheck()
        {
            try
            {
                const int hoursToLookBack = 24;
                DateTime startTime = DateTime.Now.AddHours(-hoursToLookBack);
                int errorCount = 0;
                int warningCount = 0;
                var details = new Dictionary<string, string>();

                // Check System and Application logs
                string[] logNames = { "System", "Application" };
                foreach (string logName in logNames)
                {
                    using EventLog log = new EventLog(logName);
                    // We'll use a query to get recent entries, but for simplicity we'll iterate backwards.
                    // Note: For large logs, this might be inefficient. We limit to 10000 entries or use a better method.
                    // However, for the sake of this example, we'll read backwards until we go past the startTime.
                    for (int i = log.Entries.Count - 1; i >= 0; i--)
                    {
                        EventLogEntry entry = log.Entries[i];
                        if (entry.TimeWritten < startTime)
                            break;

                        if (entry.EntryType == EventLogEntryType.Error)
                        {
                            errorCount++;
                        }
                        else if (entry.EntryType == EventLogEntryType.Warning)
                        {
                            warningCount++;
                        }
                    }
                }

                var result = new CheckResult
                {
                    CheckName = "Event Log Check",
                    Timestamp = DateTime.Now,
                    Details = new Dictionary<string, string>
                    {
                        { "Errors (last 24h)", errorCount.ToString() },
                        { "Warnings (last 24h)", warningCount.ToString() }
                    }
                };

                if (errorCount > 10)
                {
                    result.Status = CheckStatus.Critical;
                    result.Message = $"High number of errors in event log: {errorCount} errors in the last {hoursToLookBack} hours.";
                }
                else if (errorCount > 0)
                {
                    result.Status = CheckStatus.Warning;
                    result.Message = $"Some errors found in event log: {errorCount} errors in the last {hoursToLookBack} hours.";
                }
                else if (warningCount > 50)
                {
                    result.Status = CheckStatus.Warning;
                    result.Message = $"High number of warnings in event log: {warningCount} warnings in the last {hoursToLookBack} hours.";
                }
                else
                {
                    result.Status = CheckStatus.Healthy;
                    result.Message = $"Event log looks healthy: {errorCount} errors and {warningCount} warnings in the last {hoursToLookBack} hours.";
                }

                return result;
            }
            catch (Exception ex)
            {
                return new CheckResult
                {
                    CheckName = "Event Log Check",
                    Status = CheckStatus.Error,
                    Message = $"Error reading event log: {ex.Message}",
                    Timestamp = DateTime.Now
                };
            }
        }
    }
}