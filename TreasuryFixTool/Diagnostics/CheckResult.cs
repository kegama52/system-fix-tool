using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TreasuryFixTool.Diagnostics
{
    /// <summary>
    /// Represents the status of a diagnostic check.
    /// </summary>
    public enum CheckStatus
    {
        Healthy,
        Ok,
        Info,
        Warning,
        Critical,
        Error
    }

    /// <summary>
    /// Result of a diagnostic check.
    /// </summary>
    public class CheckResult
    {
        public string CheckName { get; set; } = string.Empty;
        public CheckStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, string> Details { get; set; } = new Dictionary<string, string>();
        public string? SuggestedFix { get; set; }
    }
}
