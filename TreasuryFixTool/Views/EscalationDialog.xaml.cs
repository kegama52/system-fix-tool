using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TreasuryFixTool.Notifications;
using TreasuryFixTool.Infrastructure.Logging;
using TreasuryFixTool.Infrastructure.Escalation;
using TreasuryFixTool.Infrastructure.Storage;

namespace TreasuryFixTool.Views
{
    public partial class EscalationDialog : UserControl
    {
        private static readonly string[] Departments =
        {
            "Debt Recording Unit",
            "Procurement",
            "Finance & Accounts",
            "Human Resources",
            "IT Services (ICTSU)",
            "Legal & Compliance",
            "Operations",
            "Audit",
            "Treasury Operations",
            "Other"
        };

        private readonly FileLogger _logger;
        private readonly EmailNotifier _emailNotifier;
        private DispatcherTimer? _usbWatchTimer;
        private bool _deptSelected;

        private class LogFileEntry
        {
            public string File { get; set; } = "";
            public string Content { get; set; } = "";
        }

        public EscalationDialog()
        {
            InitializeComponent();
            _logger = new FileLogger(Path.Combine(DataPaths.LogsDirectory, "Escalation.log"));
            _emailNotifier = new EmailNotifier();

            DeptCombo.ItemsSource = Departments;
            DeptCombo.SelectedIndex = -1;

            RefreshHistory();
            StartUsbWatch();
        }

        private void DeptCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _deptSelected = DeptCombo.SelectedItem is not null;
        }

        private async void Generate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_deptSelected)
                {
                    ToastManager.ShowToast("Escalation",
                        "Please select your department before generating the ticket.",
                        6000, ToastIcon.Warning);
                    return;
                }

                var result = BuildEscalationJson();
                string path = WriteEscalation(result);
                ShowSuccess($"Ticket generated: {Path.GetFileName(path)}", result.TicketEmail);
                ToastManager.ShowToast("TreasuryFixTool",
                        "ICTSU ticket saved.",
                        10000, ToastIcon.Info);

                bool emailSent = await _emailNotifier.SendEscalationNotificationAsync(
                    result.Department, result.MachineName, path);

                if (emailSent)
                    ToastManager.ShowToast("TreasuryFixTool", "Escalation email sent to ICTSU.", 8000, ToastIcon.Info);

                RefreshHistory();
            }
            catch (Exception ex)
            {
                ToastManager.ShowToast("Escalation",
                    $"Failed to generate ticket: {ex.Message}",
                   9000, ToastIcon.Error);
                    _logger.Error("Escalation generation failed.", ex);
            }
        }

        private async void ViewJson_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string filePath }) return;
            if (!File.Exists(filePath))
            {
                ToastManager.ShowToast("ICTSU",
                            "File no longer exists.",
                            5000, ToastIcon.Warning);
                return;
            }

            // Extract TicketEmail from the JSON so we can show the mailto row alongside the file
            string ticketEmail = "";
            try
            {
                string json = File.ReadAllText(filePath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("TicketEmail", out var te))
                    ticketEmail = te.ToString() ?? "";
            }
            catch { /* ignore — we still open the file */ }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = filePath,
                    UseShellExecute = true
                });
            });

            if (!string.IsNullOrEmpty(ticketEmail))
                ShowSuccess($"Viewing: {Path.GetFileName(filePath)}", ticketEmail);
        }

        // ─── MAILTO HELPERS ─────────────────────────────────────────────

        /// <summary>
        /// Builds a RFC-3986 compliant mailto: URI for the banner MailtoBtn.
        /// </summary>
        private string BuildMailtoUri(string toEmail, string subject, string body)
        {
            string Encode(string s) =>
                Uri.EscapeDataString(s.Replace("\r\n", "\n").Replace("\n", "%0A"));

            return $"mailto:{Encode(toEmail)}?subject={Encode(subject)}&body={Encode(body)}";
        }

        /// <summary>
        /// Default body template used when the user clicks "Send Email Update".
        /// </summary>
        private static string DefaultEmailBody(string ticketEmail, string ticketId, string? issues = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Hi,");
            sb.AppendLine();
            sb.AppendLine($"This is an update regarding support ticket {ticketId}.");
            sb.AppendLine($"Ticket tracking address: {ticketEmail}");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(issues))
            {
                sb.AppendLine("Issue description:");
                sb.AppendLine(issues);
                sb.AppendLine();
            }
            sb.AppendLine("Please reply to this thread with any additional information or questions.");
            sb.AppendLine();
            sb.AppendLine("Thank you,");
            sb.AppendLine("ICT Support Team");
            return sb.ToString();
        }

        /// <summary>
        /// Opens the user's default email client (via the OS shell) with
        /// the ticket email address, subject line, and body template pre-filled.
        /// </summary>
        private void OpenMailto(string toEmail, string subject, string body)
        {
            try
            {
                string uri = BuildMailtoUri(toEmail, subject, body);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName  = uri,
                    UseShellExecute = true
                });
                ToastManager.ShowToast("Email",
                    "Default email client opened.", 4000, ToastIcon.Info);
            }
            catch (Exception ex)
            {
                ToastManager.ShowToast("Email",
                    $"Failed to open mail client: {ex.Message}", 8000, ToastIcon.Error);
                _logger.Error("Failed to open mailto link.", ex);
            }
        }

        /// <summary>
        /// Click handler for the "Send Email Update" button in the success banner.
        /// Reads the current TicketEmail label and fires the mailto URI.
        /// </summary>
        private void SendEmailUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (TicketEmailLabel == null || string.IsNullOrEmpty(TicketEmailLabel.Text))
            {
                ToastManager.ShowToast("Escalation",
                    "No ticket email available. Generate a ticket first.", 6000, ToastIcon.Warning);
                return;
            }

            string ticketEmail = TicketEmailLabel.Text.Trim();
            string ticketId    = ticketEmail.Replace($"@{SupportDomain}", "").Replace("ticket_", "");
            string subject     = $"Update to Ticket #{ticketId}: TreasuryFixTool Escalation";
            string body        = DefaultEmailBody(ticketEmail, ticketId, null);

            OpenMailto(ticketEmail, subject, body);
        }

        /// <summary>
        /// Click handler for the "Send Update" button on each history row.
        /// </summary>
        private void HistoryMailto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string ticketEmail } || string.IsNullOrEmpty(ticketEmail))
            {
                ToastManager.ShowToast("Escalation",
                    "No ticket email associated with this entry.", 6000, ToastIcon.Warning);
                return;
            }

            string ticketId = ticketEmail.Replace($"@{SupportDomain}", "").Replace("ticket_", "");
            string subject  = $"Update to Ticket #{ticketId}: TreasuryFixTool Escalation";
            string body     = DefaultEmailBody(ticketEmail, ticketId, null);

            OpenMailto(ticketEmail, subject, body);
        }

        private void ExportLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dir = DataPaths.LogsDirectory;
                if (!Directory.Exists(dir))
                {
                    ToastManager.ShowToast("Export", "No logs to export.", 5000, ToastIcon.Warning);
                    return;
                }

                var saveDlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|CSV files (*.csv)|*.csv",
                    FileName = $"TreasuryFixTool_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (saveDlg.ShowDialog() == true)
                {
                    var logs = Directory.GetFiles(dir, "*.log")
                        .Select(f => new LogFileEntry { File = f, Content = File.ReadAllText(f) })
                        .ToList();

                    string output = saveDlg.FileName.EndsWith(".csv")
                        ? ExportAsCsv(logs)
                        : ExportAsJson(logs);

                    File.WriteAllText(saveDlg.FileName, output);
                    ToastManager.ShowToast("Export", $"Logs exported to {Path.GetFileName(saveDlg.FileName)}", 6000, ToastIcon.Info);
                }
            }
            catch (Exception ex)
            {
                ToastManager.ShowToast("Export", $"Export failed: {ex.Message}", 8000, ToastIcon.Error);
                _logger.Error("Log export failed.", ex);
            }
        }

        private string ExportAsJson(List<LogFileEntry> logs)
        {
            var export = new {
                Exported       = DateTime.Now,
                SupportDomain  = SupportDomain,
                Logs           = logs.Select(l => new { l.File, l.Content })
            };
            return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
        }

        private string ExportAsCsv(List<LogFileEntry> logs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("File,Timestamp,SupportDomain,TicketEmail");
            foreach (var l in logs)
            {
                sb.AppendLine($"\"{l.File}\",\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",\"{SupportDomain}\",\"ticket_placeholder@{SupportDomain}\"");
            }
            return sb.ToString();
        }

        private EscalationPayload BuildEscalationJson()
        {
            string department = DeptCombo.SelectedItem?.ToString() ?? "Unknown";

            var issues = new List<string>();
            if (!string.IsNullOrEmpty(DetectedIssuesText.Text))
                issues.Add(DetectedIssuesText.Text.Trim());

            return new EscalationPayload
            {
                Department   = department,
                MachineName  = Environment.MachineName,
                UserName     = Environment.UserName,
                OsVersion    = Environment.OSVersion.ToString(),
                Timestamp    = DateTime.Now,
                Issues       = issues,
                RequestedFix = true,
                TicketEmail  = GenerateTicketEmail(DateTime.Now.ToString("yyyyMMddHHmmss"))
            };
        }

        private string WriteEscalation(EscalationPayload payload)
        {
            string dir = DataPaths.EscalationsDirectory;
            Directory.CreateDirectory(dir);

            // File name embeds the TicketEmail handle for traceability
            string safeEmail = payload.TicketEmail.Replace('@', '_').Replace('.', '_');
            string fileName  = $"ICTSU_Support_{safeEmail}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string filePath  = Path.Combine(dir, fileName);

            string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            _logger.Info($"Escalation written: {filePath}");
            return filePath;
        }

        private void ShowSuccess(string msg, string? ticketEmail = null)
        {
            SuccessText.Text         = msg;
            SuccessBanner.Visibility = Visibility.Visible;

            if (!string.IsNullOrEmpty(ticketEmail) && TicketEmailRow != null && TicketEmailLabel != null)
            {
                TicketEmailLabel.Text   = ticketEmail;
                TicketEmailRow.Visibility = Visibility.Visible;
            }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                SuccessBanner.Visibility = Visibility.Collapsed;
                if (TicketEmailRow != null) TicketEmailRow.Visibility = Visibility.Collapsed;
            };
            timer.Start();
        }

        private void RefreshHistory()
        {
            try
            {
                var dir = DataPaths.EscalationsDirectory;
                if (!Directory.Exists(dir)) return;

                var files = Directory.GetFiles(dir, "*.json")
                                   .OrderByDescending(f => f)
                                   .Take(20)
                                   .ToList();

                EscalationsHistory.ItemsSource = files.Select(f =>
                {
                    try
                    {
                        var json = File.ReadAllText(f);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        string dept    = root.TryGetProperty("Department", out var d)   ? d.ToString() : "?";
                        string ts      = root.TryGetProperty("Timestamp", out var t)   ? t.ToString() : Path.GetFileNameWithoutExtension(f);
                        string status  = root.TryGetProperty("RequestedFix", out var r) && r.GetBoolean() ? "Fix Required" : "Escalation";
                        string tktEmail= root.TryGetProperty("TicketEmail", out var te)  ? te.ToString() : "";
                        return new EscalationHistoryItem
                        {
                            FilePath     = f,
                            Department   = dept,
                            Timestamp    = DateTime.TryParse(ts, out var dt) ? dt : DateTime.MinValue,
                            CheckName    = dept,
                            Status       = status,
                            TicketEmail  = tktEmail
                        };
                    }
                    catch { return null; }
                })
                .Where(x => x != null)
                .ToList();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to refresh escalation history.", ex);
            }
        }

        private void StartUsbWatch()
        {
            _usbWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _usbWatchTimer.Tick += (_, _) => DetectUsbUpdate();
            _usbWatchTimer.Start();
        }

        private void DetectUsbUpdate()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Removable) continue;
                string marker = Path.Combine(drive.Name, "treasury_update.sig");
                if (File.Exists(marker))
                {
                    ToastManager.ShowToast("TreasuryFixTool",
                         $"USB update detected on {drive.Name}",
                         8000, ToastIcon.Info);
                    _logger.Info($"USB update package found on {drive.Name}.");
                }
            }
        }

        /// <summary>
        /// Unique inbound ticket-email domain already set to "nattreasury.gov.za".
        /// Format for every ticket: ticket_{TICKET_ID}@nattreasury.gov.za
        /// </summary>
        public const string SupportDomain = "nattreasury.gov.za";

        /// <summary>
        /// Generates the unique, trackable email address for a given ticket id.
        /// Format: ticket_{ticketId}@nattreasury.gov.za
        /// </summary>
        public static string GenerateTicketEmail(string ticketId)
            => $"ticket_{ticketId}@{SupportDomain}";

        private class EscalationPayload
        {
            public string Department   { get; set; } = string.Empty;
            public string MachineName  { get; set; } = string.Empty;
            public string UserName     { get; set; } = string.Empty;
            public string OsVersion    { get; set; } = string.Empty;
            public DateTime Timestamp  { get; set; }
            public List<string> Issues { get; set; } = new();
            public bool RequestedFix   { get; set; }

            /// <summary>Unique inbound address generated for this ticket: ticket_{id}@nattreasury.gov.za</summary>
            public string TicketEmail  { get; set; } = string.Empty;
        }

        private class EscalationHistoryItem
        {
            public string      FilePath    { get; set; } = string.Empty;
            public string      Department  { get; set; } = string.Empty;
            public DateTime    Timestamp   { get; set; }
            public string      CheckName   { get; set; } = string.Empty;
            public string      Status      { get; set; } = string.Empty;
            /// <summary>Mailto address assigned to this ticket (for replay / follow-up).</summary>
            public string      TicketEmail { get; set; } = string.Empty;
        }
    }
}