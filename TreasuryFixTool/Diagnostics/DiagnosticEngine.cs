using System;
using System.Diagnostics;
using System.Collections.Generic;
using TreasuryFixTool.Diagnostics;

namespace TreasuryFixTool.Diagnostics
{
    /// <summary>
    /// Orchestrates running diagnostic checks and aggregating results.
    /// </summary>
    public class DiagnosticEngine
    {
        private readonly List<IDiagnosticCheck> _checks;

        public DiagnosticEngine()
        {
            _checks = new List<IDiagnosticCheck>
            {
                new DiskSpaceCheck(),
                new MemoryCheck(),
                new ServiceCheck(),
                new NetworkCheck(),
                new EventLogReader()
            };
        }

        /// <summary>
        /// Runs all diagnostic checks and returns a list of results.
        /// </summary>
        public List<CheckResult> RunAllChecks()
        {
            var results = new List<CheckResult>();
            foreach (var check in _checks)
            {
                try
                {
                    var result = check.PerformCheck();
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    results.Add(new CheckResult
                    {
                        CheckName = check.GetType().Name,
                        Status = CheckStatus.Error,
                        Message = $"Exception during check: {ex.Message}",
                        Timestamp = DateTime.Now
                    });
                }
            }
            return results;
        }
    }

    /// <summary>
    /// Contract for a single diagnostic check.
    /// </summary>
    public interface IDiagnosticCheck
    {
        CheckResult PerformCheck();
    }
}
