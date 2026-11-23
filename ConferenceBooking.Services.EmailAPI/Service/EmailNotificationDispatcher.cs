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
                "RegistrationRequested" => "REGISTRATION_REQUESTED",
                "RegistrationApproved" => "REGISTRATION_APPROVED",
                "RegistrationRejected" => "REGISTRATION_REJECTED",
                "PasswordResetSuccess" => "PASSWORD_RESET_SUCCESS",
                "PasswordChanged" => "PASSWORD_CHANGED",

                "BookingSubmitted" => "BOOKING_SUBMITTED",
                "BookingApproved" => "BOOKING_APPROVED",
                "BookingRejected" => "BOOKING_REJECTED",
                "BookingCancelled" => "BOOKING_CANCELLED",

                "ModificationSubmitted" => "MODIFICATION_SUBMITTED",
                "ModificationWithdrawn" => "MODIFICATION_WITHDRAWN",
                "ModificationApproved" => "MODIFICATION_APPROVED",
                "ModificationRejected" => "MODIFICATION_REJECTED",

                _ => throw new NotSupportedException(
                    $"Unsupported email event type: {eventType}")
            };
        }


        private static Dictionary<string, string> BuildReplacements(EmailNotificationOutbox notification)
        {
            var replacements = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            switch (notification.EventType)
            {
                case "PasswordResetOtp":
                    {
                        AddPasswordResetOtpReplacements(notification, replacements);
                        break;
                    }

                case "RegistrationRequested":
                    {
                        AddRegistrationRequestedReplacements(notification, replacements);
                        break;
                    }

                case "RegistrationApproved":
                    {
                        AddRegistrationApprovedReplacements(notification, replacements);
                        break;
                    }

                case "RegistrationRejected":
                    {
                        AddRegistrationRejectedReplacements(notification, replacements);
                        break;
                    }

                case "PasswordChanged":
                    {
                        AddPasswordChangedReplacements(notification, replacements);
                        break;
                    }

                case "PasswordResetSuccess":
                    {
                        AddPasswordResetSuccessReplacements(notification, replacements);
                        break;
                    }

                case "BookingSubmitted":
                case "BookingApproved":
                case "BookingRejected":
                case "BookingCancelled":
                case "ModificationSubmitted":
                case "ModificationWithdrawn":
                case "ModificationApproved":
                case "ModificationRejected":
                    {
                        AddBookingLifecycleReplacements(notification, replacements);
                        break;
                    }

                default:
                    throw new NotSupportedException(
                        $"Unsupported email event type: {notification.EventType}");
            }

            return replacements;
        }

        private static string ReplacePlaceholders(string content, Dictionary<string, string> replacements, bool encodeHtml)
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

        private async Task SendEmailAsync(EmailNotificationOutbox notification, string subject, string htmlBody, string? textBody, CancellationToken cancellationToken)
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
                await smtpClient.SendMailAsync(
                    message,
                    cancellationToken);

                _logger.LogInformation(
                    "Email notification {NotificationId} delivered. EventType: {EventType}",
                    notification.EmailNotificationOutboxId,
                    notification.EventType);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Email notification {NotificationId} delivery was cancelled during shutdown.",
                    notification.EmailNotificationOutboxId);

                throw;
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

        private static void AddAddresses(MailAddressCollection collection, string? addresses)
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

        // Registation and Password related Email Templates//
        private static void AddPasswordResetOtpReplacements(EmailNotificationOutbox notification, Dictionary<string, string> replacements)
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
            replacements["UserName"] = payload.UserName ?? "User";
        }

        private static void AddRegistrationRequestedReplacements(EmailNotificationOutbox notification, Dictionary<string, string> replacements)
        {
            var payload =
                            JsonSerializer.Deserialize<RegistrationRequestedEmailDto>(
                                notification.PayloadJson,
                                JsonOptions);

            if (payload == null ||
                string.IsNullOrWhiteSpace(payload.UserName) ||
                string.IsNullOrWhiteSpace(payload.ApplicationReference) ||
                string.IsNullOrWhiteSpace(payload.SubmittedOn))
            {
                throw new InvalidOperationException(
                    "Invalid registration requested email payload.");
            }

            replacements["UserName"] = payload.UserName;
            replacements["ApplicationReference"] =
                payload.ApplicationReference;
            replacements["SubmittedOn"] = payload.SubmittedOn;
        }

        private static void AddRegistrationRejectedReplacements(EmailNotificationOutbox notification, Dictionary<string, string> replacements)
        {
            var payload =
                JsonSerializer.Deserialize<RegistrationRejectedEmailDto>(
                    notification.PayloadJson,
                    JsonOptions);

            if (payload == null ||
                string.IsNullOrWhiteSpace(payload.UserName) ||
                string.IsNullOrWhiteSpace(payload.ApplicationReference) ||
                string.IsNullOrWhiteSpace(payload.ReviewedOn))
            {
                throw new InvalidOperationException(
                    "Invalid registration rejected email payload.");
            }

            replacements["UserName"] = payload.UserName;
            replacements["ApplicationReference"] =
                payload.ApplicationReference;
            replacements["ReviewedOn"] = payload.ReviewedOn;
            replacements["Remarks"] = payload.Remarks ?? string.Empty;

        }

        private static void AddRegistrationApprovedReplacements(EmailNotificationOutbox notification, Dictionary<string, string> replacements)
        {
            var payload =
                JsonSerializer.Deserialize<RegistrationApprovedEmailDto>(
                    notification.PayloadJson,
                    JsonOptions);

            if (payload == null ||
                string.IsNullOrWhiteSpace(payload.UserName) ||
                string.IsNullOrWhiteSpace(payload.ApplicationReference) ||
                string.IsNullOrWhiteSpace(payload.ReviewedOn) ||
                string.IsNullOrWhiteSpace(payload.DepartmentName) ||
                string.IsNullOrWhiteSpace(payload.RoleName))
            {
                throw new InvalidOperationException(
                    "Invalid registration approved email payload.");
            }

            replacements["UserName"] = payload.UserName;
            replacements["ApplicationReference"] =
                payload.ApplicationReference;
            replacements["ReviewedOn"] = payload.ReviewedOn;
            replacements["DepartmentName"] = payload.DepartmentName;
            replacements["RoleName"] = payload.RoleName;
            replacements["Remarks"] = payload.Remarks ?? string.Empty;


        }

        private static void AddPasswordChangedReplacements(EmailNotificationOutbox notification, Dictionary<string, string> replacements)
        {
            var payload =
                            JsonSerializer.Deserialize<PasswordChangedEmailDto>(
                                notification.PayloadJson,
                                JsonOptions);

            if (payload == null ||
                string.IsNullOrWhiteSpace(payload.UserName) ||
                string.IsNullOrWhiteSpace(payload.ChangedOn))
            {
                throw new InvalidOperationException(
                    "Invalid password changed email payload.");
            }

            replacements["UserName"] = payload.UserName;
            replacements["ChangedOn"] = payload.ChangedOn;
        }

        private static void AddPasswordResetSuccessReplacements(EmailNotificationOutbox notification, Dictionary<string, string> replacements)
        {
            var payload =
                            JsonSerializer.Deserialize<PasswordResetSuccessEmailDto>(
                                notification.PayloadJson,
                                JsonOptions);

            if (payload == null ||
                string.IsNullOrWhiteSpace(payload.UserName) ||
                string.IsNullOrWhiteSpace(payload.ResetOn))
            {
                throw new InvalidOperationException(
                    "Invalid password reset success email payload.");
            }

            replacements["UserName"] = payload.UserName;
            replacements["ResetOn"] = payload.ResetOn;
        }

        // Booking related Email Templates //

        private static void AddBookingLifecycleReplacements(EmailNotificationOutbox notification, Dictionary<string, string> replacements)
        {
            var payload =
                JsonSerializer.Deserialize<BookingLifecycleEmailDto>(
                    notification.PayloadJson,
                    JsonOptions);

            if (payload == null ||
                string.IsNullOrWhiteSpace(payload.RecipientName) ||
                string.IsNullOrWhiteSpace(payload.BookingReference))
            {
                throw new InvalidOperationException(
                    $"Invalid payload for email event '{notification.EventType}'.");
            }

            replacements["RecipientName"] = payload.RecipientName;
            replacements["RequesterName"] = payload.RequesterName;
            replacements["BookingReference"] = payload.BookingReference;
            replacements["MeetingSubject"] = payload.MeetingSubject;
            replacements["HallName"] = payload.HallName;
            replacements["MeetingStart"] = payload.MeetingStart;
            replacements["MeetingEnd"] = payload.MeetingEnd;
            replacements["ActionBy"] = payload.ActionBy;
            replacements["Remarks"] = payload.Remarks;
        }

    }
}