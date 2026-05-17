using System;

namespace TreasuryFixTool.Infrastructure.Config
{
    /// <summary>
    /// Stores user and runtime configuration for TreasuryFixTool.
    /// Persisted to C:\TreasurySupport\Config\user_dept.json.
    /// </summary>
    public class AppConfig
    {
        public string? Department        { get; set; }
        public string? UserDepartment    { get; set; }
        public int     AutoScanMinutes   { get; set; } = 60;
        public bool    StartWithWindows  { get; set; } = false;
        public bool    SilentStart       { get; set; } = false;
        public string  Version           { get; set; } = "1.0.0";
    }
}
