using System;
using System.Windows.Media;

namespace TreasuryFixTool.Models
{
    public enum StatusLevel
    {
        Ok,
        Warning,
        Critical,
        Unknown
    }

    public class MetricCard
    {
        public string Title { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public StatusLevel Status { get; set; }
        public Brush StatusBrush => Status switch
        {
            StatusLevel.Ok => Brushes.LimeGreen,
            StatusLevel.Warning => Brushes.Orange,
            StatusLevel.Critical => Brushes.Red,
            _ => Brushes.Gray
        };
    }
}