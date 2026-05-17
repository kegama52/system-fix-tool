using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TreasuryFixTool.Notifications;
using TreasuryFixTool.Infrastructure.Storage;

namespace TreasuryFixTool.Views
{
    public partial class DiagnosticsView : UserControl
    {
        private readonly List<LogEntry> _eventRecords = new();

        public class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public string Source { get; set; } = "";
            public string Message { get; set; } = "";
        }

        public DiagnosticsView()
        {
            InitializeComponent();
            LoadEventLogs();
        }

        private void LoadEventLogs()
        {
            try
            {
                _eventRecords.Clear();
                string[] logs = { "Application", "System" };
                foreach (string logName in logs)
                {
                    using var log = new EventLog(logName);
                    var entries = log.Entries.Cast<System.Diagnostics.EventLogEntry>()
                        .Where(e => e.TimeGenerated > DateTime.Now.AddDays(-7))
                        .Take(25);

                    foreach (var entry in entries)
                    {
                        _eventRecords.Add(new LogEntry
                        {
                            Timestamp = entry.TimeGenerated,
                            Source = $"{logName} - {entry.EntryType}",
                            Message = entry.Message.Length > 200 ? entry.Message.Substring(0, 200) + "..." : entry.Message
                        });
                    }
                }

                EventRecords.ItemsSource = _eventRecords.OrderByDescending(e => e.Timestamp).ToList();
            }
            catch (Exception ex)
            {
                ToastManager.ShowToast("Diagnostics", $"Failed to load event logs: {ex.Message}", 8000, ToastIcon.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadEventLogs();

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|CSV files (*.csv)|*.csv",
                    FileName = $"Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (saveDlg.ShowDialog() == true)
                {
                    string output = saveDlg.FileName.EndsWith(".csv") ? ExportCsv() : ExportJson();
                    System.IO.File.WriteAllText(saveDlg.FileName, output);
                    ToastManager.ShowToast("Export", $"Diagnostics exported to {System.IO.Path.GetFileName(saveDlg.FileName)}", 6000, ToastIcon.Info);
                }
            }
            catch (Exception ex)
            {
                ToastManager.ShowToast("Export", $"Export failed: {ex.Message}", 8000, ToastIcon.Error);
            }
        }

        private string ExportJson()
        {
            var export = new { Exported = DateTime.Now, Records = _eventRecords };
            return System.Text.Json.JsonSerializer.Serialize(export, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }

        private string ExportCsv()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Timestamp,Source,Message");
            foreach (var r in _eventRecords)
                sb.AppendLine($"\"{r.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{r.Source}\",\"{r.Message.Replace("\"", "\"\"")}\"");
            return sb.ToString();
        }
    }
}