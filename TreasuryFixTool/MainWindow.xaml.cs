using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using TreasuryFixTool.Data;
using TreasuryFixTool.Diagnostics;
using TreasuryFixTool.Fixes;
using TreasuryFixTool.Infrastructure;
using TreasuryFixTool.Infrastructure.Config;
using TreasuryFixTool.Infrastructure.Escalation;
using TreasuryFixTool.Infrastructure.Logging;
using TreasuryFixTool.Infrastructure.Storage;
using TreasuryFixTool.Monitoring;
using TreasuryFixTool.Notifications;
using TreasuryFixTool.SystemTray;
using TreasuryFixTool.Views;
using TreasuryFixTool.Updates;
using TreasuryFixTool.Models;

namespace TreasuryFixTool;

public partial class MainWindow : Window
    {
        private readonly FileLogger          _logger;
        private readonly SystemMetricsMonitor? _metricsMonitor;
     private readonly AppConfig           _config;
     private readonly UpdateManager       _updateMgr;
     private readonly UsbUpdateHandler?   _usbHandler;
     private SelfTestService?    _selfTest;
    private List<string>       _attachedFiles    = new();
    private DiagnosticEngine?  _diagnosticEngine;

    private readonly TicketRepository _ticketRepo;
    private readonly IConfiguration _dbConfig;

    private sealed record ProcessMemoryInfo(string ProcessName, int Id, double RamUsageMB, string Status);

    public MainWindow()
    {
        InitializeComponent();

        // Initialize database configuration
        _dbConfig = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        string connStr = _dbConfig.GetConnectionString("TiisgsDb") 
            ?? throw new InvalidOperationException("Database connection string missing in appsettings.json");
        _ticketRepo = new TicketRepository(connStr);

        // Initialize database
        _ = InitializeDatabaseAsync();

        _logger    = new(System.IO.Path.Combine(DataPaths.LogsDirectory,
                          $"TreasuryFix_{DateTime.Now:yyyyMMdd_HHmmss}.log"));
        _config    = new AppConfig();
        _updateMgr = new UpdateManager();
        _usbHandler= new UsbUpdateHandler();
        _selfTest  = new SelfTestService(_logger);

        // Initialize system performance monitoring if UI elements exist
        _metricsMonitor = CpuCanvas != null && RamCanvas != null && CpuText != null && RamText != null
            ? new SystemMetricsMonitor(CpuCanvas, RamCanvas, CpuText, RamText)
            : null;

        AppStatusBar.Text = $"TreasuryFixTool v1.0  |  {Environment.MachineName}  |  Offline Mode — National Treasury";

        InitializeEscalationTab();
    }

    private async Task InitializeDatabaseAsync()
    {
        try
        {
            await _ticketRepo.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to initialize database", ex);
            // Don't throw here - allow app to continue in case DB is not available yet
        }
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl?.SelectedContent is HealthDashboard d)
            await d.RunFullScanAsync();
    }

    private void Escalate_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl is TabControl tc)
        {
            tc.SelectedIndex = 1;
            ToastManager.ShowToast("TreasuryFixTool", "Describe your issue, then click Generate Ticket.", 6000, ToastIcon.Info);
        }
    }

    private async void RunSelfTests_Click(object sender, RoutedEventArgs e)
    {
        AppStatusBar.Text = "Running self-diagnostics...";

        var outputBox = FindName("TestOutputBox") as TextBox;
        if (outputBox == null) return;

        try
        {
            await Task.Run(async () =>
            {
                await _selfTest!.RunSelfTestsAsync(msg =>
                    Dispatcher.Invoke(() =>
                    {
                        outputBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
                        outputBox.ScrollToEnd();
                    }));
            });

            AppStatusBar.Text = "Self-diagnostics complete.";
            ToastManager.ShowToast("TreasuryFixTool", "Self-tests finished. Check logs for details.", 4000, ToastIcon.Info);
        }
        catch (Exception ex)
        {
            _logger.Error("Self-test pipeline failed", ex);
            ToastManager.ShowToast("TreasuryFixTool", $"Test failed: {ex.Message}", 5000, ToastIcon.Error);
        }
    }

    private async void RunFix_Click(object sender, RoutedEventArgs e)
    {
        await RunAutoResolveInternal("netsh winsock reset", sender);
    }

    private async Task RunAutoResolveInternal(string command, object sender)
    {
        AppStatusBar.Text = $"Applying fix: {command}...";

        var outputBox = FindName("TestOutputBox") as TextBox;
        if (outputBox == null) return;

        await Task.Run(async () =>
        {
            await _selfTest!.RunAutoResolveAsync(command, msg =>
                Dispatcher.Invoke(() =>
                {
                    outputBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
                    outputBox.ScrollToEnd();
                }));
        });

        AppStatusBar.Text = "Fix execution complete.";
    }



    // ─── Escalation tab ────────────────────────────────────────────────────
    private void InitializeEscalationTab()
    {
        LoadSystemInformation();
        _diagnosticEngine = new DiagnosticEngine();
    }

    // ─── System Information ───────────────────────────────────────────────────
    private void LoadSystemInformation()
    {
        try
        {
            SysMachineName.Text = Environment.MachineName;
            SysOSVersion.Text   = Environment.OSVersion.ToString();
            SysIPAddress.Text   = GetLocalIpAddress() ?? "Unknown";
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to load system information", ex);
            SysMachineName.Text = "—";
            SysOSVersion.Text   = "—";
            SysIPAddress.Text   = "—";
        }
    }

    private static string? GetLocalIpAddress()
    {
        try
        {
            var host  = Dns.GetHostAddressesAsync(Dns.GetHostName()).Result;
            var ipv4  = host.FirstOrDefault(h => h.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            return ipv4?.ToString();
        }
        catch { return null; }
    }

    // ─── Escalation quick actions ─────────────────────────────────────────────

    private void ExportLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string exportDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop");
            string zipPath   = Path.Combine(exportDir, $"TreasuryLogs_{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

            if (!Directory.Exists(DataPaths.LogsDirectory))
            {
                ToastManager.ShowToast("Export Failed", "Logs directory not found.", 4000, ToastIcon.Error);
                return;
            }

            var files = Directory.GetFiles(DataPaths.LogsDirectory, "*.*",
                            SearchOption.TopDirectoryOnly)
                        .Where(f => !f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        .ToArray();

            if (files.Length == 0)
            {
                ToastManager.ShowToast("No Logs", "No log files found to export.", 4000, ToastIcon.Warning);
                return;
            }

            ZipFile.CreateFromDirectory(DataPaths.LogsDirectory, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

            ToastManager.ShowToast("Logs Exported",
                $"{files.Length} file(s) saved to:\n{zipPath}", 6000, ToastIcon.Info);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to export logs", ex);
            ToastManager.ShowToast("Export Error", ex.Message, 4000, ToastIcon.Error);
        }
    }

    private async void GenerateTicket_Click(object sender, RoutedEventArgs e)
    {
        var escMgr = new EscalationManager(DataPaths.EscalationsDirectory, _logger);
        var checks = new ObservableCollection<CheckResult>
        {
            new CheckResult
            {
                CheckName = "All Issues",
                Status    = CheckStatus.Info,
                Message   = "Manually escalated",
                Timestamp = DateTime.Now
            }
        };

        try
        {
            string jsonPath = escMgr.CreateEscalation(checks.ToList());

            var ticket = new Ticket
            {
                TicketId      = $"TKT-{DateTime.Now:yyyyMMdd-HHmmss}",
                Department    = DepartmentComboBox.SelectedItem is ComboBoxItem cbi
                                    ? (cbi.Content.ToString() ?? "—") : "—",
                Priority      = PriorityMedium.IsChecked == true ? "Medium"
                           : PriorityLow.IsChecked      == true ? "Low"
                           : PriorityHigh.IsChecked     == true ? "High"
                           : PriorityCritical.IsChecked == true ? "Critical"
                           : "Medium",
                Category      = CategoryComboBox.SelectedItem is ComboBoxItem cbi2
                                    ? (cbi2.Content.ToString() ?? "—") : "—",
                Description   = DescriptionTextBox.Text,
                StepsTaken    = StepsTextBox.Text,
                ContactName   = ContactNameTextBox.Text,
                ContactPhone  = ContactPhoneTextBox.Text,
                MachineName   = Environment.MachineName,
                OSVersion     = Environment.OSVersion.ToString(),
                DetectedIssues = "Manually escalated — no automated checks run.",
                CreatedAt     = DateTime.UtcNow
            };
            await _ticketRepo.InsertTicketAsync(ticket);

            ToastManager.ShowToast("Ticket Created",
                $"Escalation #{ticket.TicketId} generated and saved.", 6000, ToastIcon.Info);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to generate escalation ticket", ex);
            ToastManager.ShowToast("Error", ex.Message, 6000, ToastIcon.Error);
        }
    }

    private void CallAssistant_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName   = "tel:+271101234567",
                UseShellExecute = true
            });
            ToastManager.ShowToast("Calling Support", "Opening dialer…", 4000, ToastIcon.Info);
        }
        catch (Exception ex)
        {
            ToastManager.ShowToast("Error", $"Cannot start dialer: {ex.Message}", 4000, ToastIcon.Error);
        }
    }

    private void LiveChat_Click(object sender, RoutedEventArgs e)
    {
        const string chatUrl = "https://nattreasury.gov.za/support/live-chat";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = chatUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ToastManager.ShowToast("Error", $"Cannot open browser: {ex.Message}", 4000, ToastIcon.Error);
        }
    }

    private void ViewProcessesBtn_Click(object sender, RoutedEventArgs e)
    {
        var processes = GetRunningProcessesByMemory();
        var window = new Window
        {
            Title = "Task Manager - Top Processes by RAM",
            Width = 820,
            Height = 540,
            Content = CreateProcessManagerView(processes),
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        window.Show();
    }

    private static List<ProcessMemoryInfo> GetRunningProcessesByMemory()
    {
        var results = new List<ProcessMemoryInfo>();

        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.HasExited)
                        continue;

                    double memoryMb = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 1);
                    string status = process.HasExited ? "Exited"
                        : process.Responding ? "Running"
                        : "Not Responding";

                    results.Add(new ProcessMemoryInfo(
                        ProcessName: string.IsNullOrWhiteSpace(process.ProcessName) ? "Unknown" : process.ProcessName,
                        Id: process.Id,
                        RamUsageMB: memoryMb,
                        Status: status));
                }
                catch
                {
                    continue;
                }
            }
        }

        return results
            .OrderByDescending(p => p.RamUsageMB)
            .ThenBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static UIElement CreateProcessManagerView(IEnumerable<ProcessMemoryInfo> processes)
    {
        var grid = new DataGrid
        {
            IsReadOnly = true,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            Margin = new Thickness(8),
            ItemsSource = processes,
            SelectionMode = DataGridSelectionMode.Single
        };

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Process Name",
            Binding = new System.Windows.Data.Binding("ProcessName"),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "PID",
            Binding = new System.Windows.Data.Binding("Id"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Auto)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "RAM (MB)",
            Binding = new System.Windows.Data.Binding("RamUsageMB"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Status",
            Binding = new System.Windows.Data.Binding("Status"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        return new ScrollViewer
        {
            Content = grid,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private void PrintIssuesReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var headerLines = new List<string>
            {
                "╔══════════════════════════════════════════════════╗",
                "║     TreasuryFixTool — Escalation Report           ║",
                "╠══════════════════════════════════════════════════╣",
            };

            var issueLines = new List<string>();
            foreach (var child in IssuesList.Children)
            {
                if (child is Border b && b.Child is StackPanel sp)
                {
                    foreach (var inner in sp.Children)
                        if (inner is TextBlock tb)
                            issueLines.Add($"  • {tb.Text.Trim()}");
                }
            }

            if (!issueLines.Any())
                issueLines.Add("  No active issues detected.");

            var footerLines = new List<string>
            {
                "╠══════════════════════════════════════════════════╣",
                $"  Machine  : {Environment.MachineName}",
                $"  User     : {Environment.UserName}",
                $"  OS       : {Environment.OSVersion}",
                $"  Printed  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                "╚══════════════════════════════════════════════════╝",
            };

            var allLines = headerLines.Concat(issueLines).Concat(footerLines).ToList();
            string report = string.Join(Environment.NewLine, allLines);

            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true) return;

            var doc = new FlowDocument
            {
                PagePadding    = new Thickness(60),
                ColumnWidth    = double.PositiveInfinity,
                FontFamily     = new FontFamily("Consolas"),
                FontSize       = 12,
                Background     = Brushes.White,
            };

            doc.Blocks.Add(new Paragraph(new Run(report)));
            printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator,
                "TreasuryFixTool — Issues Report");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to print issues report", ex);
            ToastManager.ShowToast("Print Error",
                ex is System.ComponentModel.Win32Exception
                    ? "No printer configured. Set up a printer first."
                    : ex.Message, 5000, ToastIcon.Error);
        }
    }

    private async void EmailSupport_Click(object sender, RoutedEventArgs e)
    {
        if (DepartmentComboBox.SelectedItem is not ComboBoxItem)
        {
            ToastManager.ShowToast("Validation Error",
                "Please select your department first.", 4000, ToastIcon.Warning);
            return;
        }

        var issueLines = new List<string>();
        foreach (var child in IssuesList.Children)
        {
            if (child is Border b && b.Child is StackPanel sp)
            {
                foreach (var inner in sp.Children)
                    if (inner is TextBlock tb)
                        issueLines.Add(tb.Text.Trim());
            }
        }

        var escMgr = new EscalationManager(DataPaths.EscalationsDirectory, _logger);
        var checks = new ObservableCollection<CheckResult>
        {
            new CheckResult
            {
                CheckName = "Issues Escalated",
                Status    = CheckStatus.Warning,
                Message   = string.Join(Environment.NewLine, issueLines.DefaultIfEmpty("No issues listed.")),
                Timestamp = DateTime.Now
            }
        };

        try
        {
            string jsonPath = escMgr.CreateEscalation(checks.ToList());
            var notifier   = new EmailNotifier();

            string dept = DepartmentComboBox.SelectedItem is ComboBoxItem cbi
                                ? (cbi.Content.ToString() ?? "Support") : "Support";

            bool sent = await notifier.SendEscalationNotificationAsync(
                            dept, Environment.MachineName, jsonPath);

            if (sent)
                ToastManager.ShowToast("Email Sent",
                    $"Escalation report emailed for {dept}.", 6000, ToastIcon.Info);
            else
                ToastManager.ShowToast("Email Failed",
                    "SMTP relay rejected the message. Try again or contact ICTSU directly.", 6000, ToastIcon.Error);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to send escalation email", ex);
            ToastManager.ShowToast("Email Error", ex.Message, 5000, ToastIcon.Error);
        }
    }

    private void RemoteSession_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = "mstsc.exe",
                UseShellExecute = true
            });
            ToastManager.ShowToast("Remote Session",
                "Connecting to Remote Desktop…", 4000, ToastIcon.Info);
        }
        catch (Exception ex)
        {
            ToastManager.ShowToast("Error",
                $"Cannot start Remote Desktop: {ex.Message}", 4000, ToastIcon.Error);
        }
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Title       = "Add Attachment Files",
                Filter      = "All Files (*.*)|*.*|Images (*.png;*.jpg;*.gif;*.bmp)|*.png;*.jpg;*.gif;*.bmp|Documents (*.pdf;*.doc;*.docx;*.txt)|*.pdf;*.doc;*.docx;*.txt",
            };

            if (dlg.ShowDialog() == true)
            {
                foreach (string file in dlg.FileNames)
                    if (!_attachedFiles.Contains(file))
                        _attachedFiles.Add(file);

                ToastManager.ShowToast("Files Added",
                    $"{dlg.FileNames.Length} file(s) attached to this ticket.", 4000, ToastIcon.Info);
            }
        }
        catch (Exception ex)
        {
            ToastManager.ShowToast("Error",
                $"Cannot open file dialog: {ex.Message}", 4000, ToastIcon.Error);
        }
    }

    private async void SubmitTicket_Click(object sender, RoutedEventArgs e)
    {
        // ── Validate ──────────────────────────────────────────────────
        if (DepartmentComboBox.SelectedItem is null)
        {
            ToastManager.ShowToast("Validation Error", "Please select your department", 4000, ToastIcon.Warning);
            return;
        }
        if (CategoryComboBox.SelectedItem is null)
        {
            ToastManager.ShowToast("Validation Error", "Please select an issue category", 4000, ToastIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
        {
            ToastManager.ShowToast("Validation Error", "Please provide a detailed description", 4000, ToastIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(ContactNameTextBox.Text) || string.IsNullOrWhiteSpace(ContactPhoneTextBox.Text))
        {
            ToastManager.ShowToast("Validation Error", "Please provide contact information", 4000, ToastIcon.Warning);
            return;
        }

        // ── Determine priority ─────────────────────────────────────────
        string priority = PriorityMedium.IsChecked == true ? "Medium"
                    : PriorityLow.IsChecked      == true ? "Low"
                    : PriorityHigh.IsChecked     == true ? "High"
                    : PriorityCritical.IsChecked == true ? "Critical"
                    : "Medium";

        // ── Collect detected issues from the right panel ──────────────
        var issueLines = new List<string>();
        foreach (var child in IssuesList.Children)
        {
            if (child is Border b && b.Child is StackPanel sp)
            {
                foreach (var inner in sp.Children)
                    if (inner is TextBlock tb) issueLines.Add(tb.Text.Trim());
            }
        }

        string issuesText = string.Join(Environment.NewLine, issueLines);

        var ticket = new Ticket
        {
            TicketId = $"TKT-{DateTime.Now:yyyyMMdd-HHmmss}",
            Department = ((ComboBoxItem)DepartmentComboBox.SelectedItem).Content.ToString()!,
            Priority = priority,
            Category = ((ComboBoxItem)CategoryComboBox.SelectedItem).Content.ToString()!,
            Description = DescriptionTextBox.Text,
            StepsTaken = StepsTextBox.Text,
            ContactName = ContactNameTextBox.Text,
            ContactPhone = ContactPhoneTextBox.Text,
            MachineName = Environment.MachineName,
            OSVersion = Environment.OSVersion.ToString(),
            DetectedIssues = issuesText,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _ticketRepo.InsertTicketAsync(ticket);
            
            ToastManager.ShowToast("Ticket Created", 
                $"Ticket #{ticket.TicketId} saved to database.", 6000, ToastIcon.Info);

            DescriptionTextBox.Clear();
            StepsTextBox.Clear();
            _attachedFiles.Clear();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to save ticket to PostgreSQL", ex);
            ToastManager.ShowToast("Database Error", 
                $"Failed to create ticket: {ex.Message}", 6000, ToastIcon.Error);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _metricsMonitor?.Dispose();
        base.OnClosed(e);
    }
}
