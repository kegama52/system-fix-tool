using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using TreasuryFixTool.Infrastructure.Logging;
using TreasuryFixTool.Infrastructure.Storage;

namespace TreasuryFixTool.Infrastructure.Escalation
{
    public class EmailNotificationSettings
    {
        public string SmtpServer { get; set; } = "smtp.nattreasury.gov.za";
        public int SmtpPort { get; set; } = 587;
        public string FromAddress { get; set; } = "ictsu@nattreasury.gov.za";
        public string FromName { get; set; } = "ICTSU TreasuryFixTool";
        public string ToAddress { get; set; } = "ictsupport@nattreasury.gov.za";
        public string SubjectPrefix { get; set; } = "[ICTSU Support]";
        public bool EnableSsl { get; set; } = true;
        public string? Username { get; set; }
        public string? Password { get; set; }

        public static EmailNotificationSettings CreateGmailSettings(string email, string appPassword, string recipient, string fromName = "TreasuryFixTool")
        {
            return new EmailNotificationSettings
            {
                SmtpServer = "smtp.gmail.com",
                SmtpPort = 587,
                FromAddress = email,
                FromName = fromName,
                ToAddress = recipient,
                Username = email,
                Password = appPassword,
                EnableSsl = true
            };
        }
    }

    public class EmailNotifier
    {
        private readonly EmailNotificationSettings _settings;
        private readonly FileLogger _logger;

        public EmailNotifier(EmailNotificationSettings? settings = null)
        {
            _settings = settings ?? new EmailNotificationSettings();
            _logger = new FileLogger(Path.Combine(DataPaths.LogsDirectory, "Email.log"));
        }

        public async Task<bool> SendEscalationNotificationAsync(string department, string machineName, string jsonPath)
        {
            try
            {
                string jsonContent = await File.ReadAllTextAsync(jsonPath);
                string subject = $"{_settings.SubjectPrefix} Escalation - {department} - {machineName}";
                string body = $@"
ICTSU Support Escalation

Department: {department}
Machine: {machineName}
Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

Issue Details:
{jsonContent}

--
TreasuryFixTool Automated Escalation";

                using var client = new SmtpClient(_settings.SmtpServer, _settings.SmtpPort)
                {
                    EnableSsl = _settings.EnableSsl,
                    Credentials = _settings.Username != null && _settings.Password != null
                        ? new NetworkCredential(_settings.Username, _settings.Password)
                        : CredentialCache.DefaultNetworkCredentials
                };

                var mail = new MailMessage
                {
                    From = new MailAddress(_settings.FromAddress, _settings.FromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };
                mail.To.Add(_settings.ToAddress);

                await client.SendMailAsync(mail);
                _logger.Info($"Escalation email sent for {department} / {machineName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to send escalation email.", ex);
                return false;
            }
        }
    }
}