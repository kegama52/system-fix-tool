using System;

namespace TreasuryFixTool.Infrastructure.Storage
{
    /// <summary>
    /// Contains static paths used by the application for storing logs, escalations, etc.
    /// </summary>
    public static class DataPaths
    {
        /// <summary>
        /// The base directory for TreasurySupport data.
        /// </summary>
        public static string BaseDirectory => @"C:\TreasurySupport";

        /// <summary>
        /// The directory for log files.
        /// </public>
        public static string LogsDirectory => System.IO.Path.Combine(BaseDirectory, "Logs");

        /// <summary>
        /// The directory for escalation reports.
        /// </summary>
        public static string EscalationsDirectory => System.IO.Path.Combine(BaseDirectory, "Escalations");

        /// <summary>
        /// The directory for deployment files.
        /// </summary>
        public static string DeployDirectory => @"C:\TreasurySoftware\Deploy";
    }
}