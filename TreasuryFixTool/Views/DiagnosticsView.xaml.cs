using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using System.Globalization;
using TreasuryFixTool.Notifications;
using TreasuryFixTool.Infrastructure.Storage;

namespace TreasuryFixTool.Views;

public class EventLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string level ? level.ToLowerInvariant() switch
        {
            "error"       => new SolidColorBrush(Color.FromRgb(0xE8, 0x28, 0x28)),
            "warning"     => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
            "information" => new SolidColorBrush(Color.FromRgb(0xD4, 0xED, 0xDA)),
            _             => new SolidColorBrush(Color.FromRgb(0xF0, 0xF4, 0xF8))
        } : new SolidColorBrush(Color.FromRgb(0xF0, 0xF4, 0xF8));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class EventLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string level ? level.ToLowerInvariant() switch
        {
            "error"       => new SolidColorBrush(Color.FromRgb(0x99, 0x28, 0x28)),
            "warning"     => new SolidColorBrush(Color.FromRgb(0x78, 0x44, 0x00)),
            "information" => new SolidColorBrush(Color.FromRgb(0x16, 0x3E, 0x1A)),
            _             => new SolidColorBrush(Color.FromRgb(0x47, 0x57, 0x73))
        } : new SolidColorBrush(Color.FromRgb(0x47, 0x57, 0x73));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public partial class DiagnosticsView : UserControl
{
    private readonly ObservableCollection<LogEntry> _eventRecords = new();
    public ObservableCollection<LogEntry> DiagnosticLogs => _eventRecords;

    private DispatcherTimer? _autoRefreshTimer;
    private string _currentFilter = "All";
    private string? _searchText;

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string   Source    { get; set; } = "";
        public string   Message   { get; set; } = "";
        public string   Level     { get; set; } = "";
    }

    public DiagnosticsView()
    {
        InitializeComponent();
        DataContext = this;
        LoadEventLogs();
    }

    private async void LoadEventLogs()
    {
        try
        {
            await Task.Run(() => {
                List<LogEntry> collected = new();
                string[] logs = { "Application", "System" };
                foreach (string logName in logs)
                {
                    using EventLog log = new EventLog(logName);
                    var entries = log.Entries.Cast<EventLogEntry>()
                        .Where(e => e.TimeGenerated > DateTime.Now.AddDays(-7))
                        .Take(50);
                    foreach (EventLogEntry entry in entries)
                    {
                        collected.Add(new LogEntry {
                            Timestamp = entry.TimeGenerated,
                            Source    = logName,
                            Level     = entry.EntryType.ToString(),
                            Message   = entry.Message.Length > 200 ? entry.Message[..200] + "..." : entry.Message
                        });
                    }
                }
                Dispatcher.Invoke(() => {
                    _eventRecords.Clear();
                    foreach (var item in collected.OrderByDescending(e => e.Timestamp))
                        _eventRecords.Add(item);
                });
            });
        }
        catch (Exception ex)
        {
            ToastManager.ShowToast("Diagnostics", $"Failed to load event logs: {ex.Message}", 8000, ToastIcon.Error);
        }
    }

    private void LevelFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cb || cb.SelectedItem is not ComboBoxItem item || item.Content is not string content)
            return;
        _currentFilter = content;
        RefreshFilteredView();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = (SearchBox?.Text ?? "").Trim().ToLowerInvariant();
        RefreshFilteredView();
    }

    private void RefreshFilteredView()
    {
        if (_eventRecords == null) return;

        var query = _eventRecords.AsEnumerable();
        if (_currentFilter != "All" && _currentFilter != "All Levels")
            query = query.Where(e => e.Level.Equals(_currentFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(_searchText))
            query = query.Where(e => e.Message.ToLowerInvariant().Contains(_searchText) ||
                                    e.Source.ToLowerInvariant().Contains(_searchText));
        var sorted = query.OrderByDescending(e => e.Timestamp).ToList();

        Dispatcher.Invoke(() => {
            _eventRecords.Clear();
            foreach (var item in sorted) _eventRecords.Add(item);
            if (EntryCountLabel != null)
                EntryCountLabel.Text = $"{sorted.Count} entries";
        });
    }

    private void AutoRefreshCheck_Checked(object sender, RoutedEventArgs e)
    {
        _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _autoRefreshTimer.Tick += (s, args) => LoadEventLogs();
        _autoRefreshTimer.Start();
    }

    private void AutoRefreshCheck_Unchecked(object sender, RoutedEventArgs e)
    {
        _autoRefreshTimer?.Stop();
        _autoRefreshTimer = null;
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
                File.WriteAllText(saveDlg.FileName, output);
                ToastManager.ShowToast("Export",
                    $"Diagnostics exported to {Path.GetFileName(saveDlg.FileName)}", 6000, ToastIcon.Info);
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
        return System.Text.Json.JsonSerializer.Serialize(
            export, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private string ExportCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Source,Level,Message");
        foreach (LogEntry r in _eventRecords)
            sb.AppendLine(
                $"\"{r.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{r.Source}\",\"{r.Level}\",\"{r.Message.Replace("\"", "\"\"")}\"");
        return sb.ToString();
    }
}