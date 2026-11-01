using ConferenceBooking.Services.EmailAPI.Data;
using ConferenceBooking.Services.EmailAPI.Models;
using ConferenceBooking.Services.EmailAPI.Models.Dto;
using ConferenceBooking.Services.EmailAPI.Service.IService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Text.Json;

namespace ConferenceBooking.Services.EmailAPI.Service
{
    public class EmailNotificationDispatcher : IEmailNotificationDispatcher
    {
        private readonly ApplicationDbContext _db;
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailNotificationDispatcher> _logger;

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        public EmailNotificationDispatcher(
            ApplicationDbContext db,
            IOptions<EmailSettings> emailSettings,
            ILogger<EmailNotificationDispatcher> logger)
        {
            _db = db;
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task DispatchAsync(
            EmailNotificationOutbox notification,
            CancellationToken cancellationToken = default)
        {
            if (notification == null)
                throw new ArgumentNullException(nameof(notification));

            if (string.IsNullOrWhiteSpace(notification.EventType))
                throw new InvalidOperationException("Email event type is missing.");

            var templateCode = ResolveTemplateCode(notification.EventType);

            var template = await _db.EmailTemplates
                .AsNoTracking()
                .Where(x =>
                    x.TemplateCode == templateCode &&
                    x.IsActive)
                .OrderByDescending(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken);

            if (template == null)
            {
                throw new InvalidOperationException(
                    $"No active email template found for event '{notification.EventType}'.");
            }

            var replacements = BuildReplacements(notification);

            var subject = ReplacePlaceholders(
                template.SubjectTemplate,
                replacements,
                encodeHtml: false);

            var htmlBody = ReplacePlaceholders(
                template.HtmlBody,
                replacements,
                encodeHtml: true);

            var textBody = string.IsNullOrWhiteSpace(template.TextBody)
                ? null
                : ReplacePlaceholders(
                    template.TextBody,
                    replacements,
                    encodeHtml: false);

            await SendEmailAsync(
                notification,
                subject,
                htmlBody,
                textBody,
                cancellationToken);
        }

        private static string ResolveTemplateCode(string eventType)
        {
            return eventType switch
            {
                "PasswordResetOtp" => "PASSWORD_RESET_OTP",

                _ => throw new NotSupportedException(
                    $"Unsupported email event type: {eventType}")
            };
        }

        private static Dictionary<string, string> BuildReplacements(
            EmailNotificationOutbox notification)
        {
            var replacements = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            switch (notification.EventType)
            {
                case "PasswordResetOtp":
                    {
                        var payload =
                            JsonSerializer.Deserialize<PasswordResetOtpEmailDto>(
                                notification.PayloadJson,
                                JsonOptions);

                        if (payload == null ||
                            string.IsNullOrWhiteSpace(payload.Otp) ||
                            payload.ValidityMinutes <= 0)
                        {
                            throw new InvalidOperationException(
                                "Invalid password reset OTP payload.");
                        }

                        replacements["Otp"] = payload.Otp;
                        replacements["ValidityMinutes"] =
                            payload.ValidityMinutes.ToString();

                        break;
                    }

                default:
                    throw new NotSupportedException(
                        $"Unsupported email event type: {notification.EventType}");
            }

            return replacements;
        }

        private static string ReplacePlaceholders(
            string content,
            Dictionary<string, string> replacements,
            bool encodeHtml)
        {
            foreach (var item in replacements)
            {
                var value = encodeHtml
                    ? WebUtility.HtmlEncode(item.Value)
                    : item.Value;

                content = content.Replace(
                    "{{" + item.Key + "}}",
                    value,
                    StringComparison.OrdinalIgnoreCase);
            }

            return content;
        }

        private async Task SendEmailAsync(
            EmailNotificationOutbox notification,
            string subject,
            string htmlBody,
            string? textBody,
            CancellationToken cancellationToken)
        {
            ValidateConfiguration();

            if (string.IsNullOrWhiteSpace(notification.ToEmail))
                throw new InvalidOperationException("Recipient email is missing.");

            using var message = new MailMessage
            {
                From = new MailAddress(
                    _emailSettings.FromEmail,
                    _emailSettings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            message.To.Add(new MailAddress(notification.ToEmail));

            AddAddresses(message.CC, notification.CcEmail);
            AddAddresses(message.Bcc, notification.BccEmail);

            if (!string.IsNullOrWhiteSpace(textBody))
            {
                message.AlternateViews.Add(
                    AlternateView.CreateAlternateViewFromString(
                        textBody,
                        System.Text.Encoding.UTF8,
                        "text/plain"));
            }

            message.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    htmlBody,
                    System.Text.Encoding.UTF8,
                    "text/html"));

            using var smtpClient = new SmtpClient(
                _emailSettings.SmtpHost,
                _emailSettings.SmtpPort)
            {
                EnableSsl = _emailSettings.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_emailSettings.SmtpUsername))
            {
                smtpClient.Credentials = new System.Net.NetworkCredential(
                    _emailSettings.SmtpUsername,
                    _emailSettings.SmtpPassword);
            }
            else
            {
                smtpClient.UseDefaultCredentials = true;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await smtpClient.SendMailAsync(message);

                _logger.LogInformation(
                    "Email notification {NotificationId} delivered. EventType: {EventType}",
                    notification.EmailNotificationOutboxId,
                    notification.EventType);
            }
            catch (Exception ex)
            {
                // Never log PayloadJson: it may contain an OTP or other sensitive data.
                _logger.LogError(
                    ex,
                    "Email notification {NotificationId} delivery failed. EventType: {EventType}",
                    notification.EmailNotificationOutboxId,
                    notification.EventType);

                throw;
            }
        }

        private static void AddAddresses(
            MailAddressCollection collection,
            string? addresses)
        {
            if (string.IsNullOrWhiteSpace(addresses))
                return;

            foreach (var address in addresses.Split(
                new[] { ';', ',' },
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries))
            {
                collection.Add(new MailAddress(address));
            }
        }

        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_emailSettings.SmtpHost))
                throw new InvalidOperationException("SMTP host is not configured.");

            if (_emailSettings.SmtpPort <= 0)
                throw new InvalidOperationException("SMTP port is invalid.");

            if (string.IsNullOrWhiteSpace(_emailSettings.FromEmail))
                throw new InvalidOperationException("Sender email is not configured.");
        }
    }
}