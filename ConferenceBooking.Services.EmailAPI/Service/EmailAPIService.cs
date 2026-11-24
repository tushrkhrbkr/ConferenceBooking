using ConferenceBooking.Services.EmailAPI.Models;
using ConferenceBooking.Services.EmailAPI.Models.Dto;
using ConferenceBooking.Services.EmailAPI.Service.IService;
using ConferenceBooking.Services.EmailAPI.Data;
using ConferenceBooking.Services.EmailAPI.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Data;

namespace ConferenceBooking.Services.EmailAPI.Service
{
    public class EmailAPIService : IEmailAPIService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailAPIService> _logger;
        private readonly ApplicationDbContext _db;

        public EmailAPIService(
            IOptions<EmailSettings> emailSettings,
            ILogger<EmailAPIService> logger,
            ApplicationDbContext db)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
            _db = db;
        }

        public async Task SendPasswordResetOtpAsync(
            PasswordResetOtpEmailDto request)
        {
            if (string.IsNullOrWhiteSpace(request.ToEmail))
            {
                throw new ArgumentException(
                    "Recipient email address is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Otp))
            {
                throw new ArgumentException(
                    "OTP is required.");
            }

            if (request.ValidityMinutes <= 0)
            {
                throw new ArgumentException(
                    "OTP validity must be greater than zero.");
            }

            ValidateConfiguration();

            var subject = "CONVENE - Password Reset OTP";

            var htmlBody = BuildHtmlBody(
                request.Otp,
                request.ValidityMinutes);

            var plainTextBody = BuildPlainTextBody(
                request.Otp,
                request.ValidityMinutes);

            using var message = new MailMessage
            {
                From = new MailAddress(
                    _emailSettings.FromEmail,
                    _emailSettings.FromName),

                Subject = subject,

                Body = htmlBody,

                IsBodyHtml = true
            };

            message.To.Add(
                new MailAddress(request.ToEmail));

            // Explicit plain-text MIME part.
            message.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    plainTextBody,
                    System.Text.Encoding.UTF8,
                    "text/plain"));

            // Explicit HTML MIME part.
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

            if (!string.IsNullOrWhiteSpace(
                    _emailSettings.SmtpUsername))
            {
                smtpClient.Credentials =
                    new NetworkCredential(
                        _emailSettings.SmtpUsername,
                        _emailSettings.SmtpPassword);
            }
            else
            {
                smtpClient.UseDefaultCredentials = true;
            }

            try
            {
                await smtpClient.SendMailAsync(message);

                _logger.LogInformation(
                    "Password reset OTP email sent successfully to {Email}.",
                    MaskEmail(request.ToEmail));
            }
            catch (Exception ex)
            {
                // Never log the OTP.
                _logger.LogError(
                    ex,
                    "Failed to send password reset OTP email to {Email}.",
                    MaskEmail(request.ToEmail));

                throw;
            }
        }


        public async Task<EmailOutboxHealthDto> GetOutboxHealthAsync(
            CancellationToken cancellationToken = default)
        {
            var outbox = _db.EmailNotificationOutbox.AsNoTracking();

            var result = new EmailOutboxHealthDto
            {
                GeneratedAtUtc = DateTime.UtcNow,

                PendingCount = await outbox.CountAsync(
                    x => x.Status == EmailNotificationStatus.Pending,
                    cancellationToken),

                ProcessingCount = await outbox.CountAsync(
                    x => x.Status == EmailNotificationStatus.Processing,
                    cancellationToken),

                SentCount = await outbox.CountAsync(
                    x => x.Status == EmailNotificationStatus.Sent,
                    cancellationToken),

                FailedCount = await outbox.CountAsync(
                    x => x.Status == EmailNotificationStatus.Failed,
                    cancellationToken),

                DeadLetterCount = await outbox.CountAsync(
                    x => x.Status == EmailNotificationStatus.DeadLetter,
                    cancellationToken),

                OldestPendingCreatedAtUtc = await outbox
                    .Where(x => x.Status == EmailNotificationStatus.Pending)
                    .Select(x => (DateTime?)x.CreatedAtUtc)
                    .MinAsync(cancellationToken),

                OldestProcessingLockedAtUtc = await outbox
                    .Where(x => x.Status == EmailNotificationStatus.Processing &&
                                x.LockedAtUtc != null)
                    .Select(x => x.LockedAtUtc)
                    .MinAsync(cancellationToken)
            };

            return result;
        }


        public async Task<List<DeadLetterNotificationDto>>
            GetDeadLetterNotificationsAsync(
                CancellationToken cancellationToken = default)
        {
            return await _db.EmailNotificationOutbox
                .AsNoTracking()
                .Where(x => x.Status == EmailNotificationStatus.DeadLetter)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(100)
                .Select(x => new DeadLetterNotificationDto
                {
                    EmailNotificationOutboxId =
                        x.EmailNotificationOutboxId,

                    EventType = x.EventType,
                    CreatedAtUtc = x.CreatedAtUtc,
                    AttemptCount = x.AttemptCount,
                    MaxAttempts = x.MaxAttempts,
                    LastAttemptAtUtc = x.LastAttemptAtUtc,
                    LastError = x.LastError
                })
                .ToListAsync(cancellationToken);
        }


        public async Task<bool> RetryDeadLetterNotificationAsync(
            long notificationId,
            RetryDeadLetterNotificationDto request,
            CancellationToken cancellationToken = default)
        {
            if (notificationId <= 0 ||
                request == null ||
                !request.ConfirmRetry ||
                string.IsNullOrWhiteSpace(request.Reason) ||
                request.Reason.Trim().Length < 10 ||
                request.Reason.Trim().Length > 500 ||
                string.IsNullOrWhiteSpace(request.OperatorUserId) ||
                string.IsNullOrWhiteSpace(request.OperatorName))
            {
                return false;
            }

            var allowedEventTypes = new[]
            {
        "BookingSubmitted",
        "BookingApproved",
        "BookingRejected",
        "BookingCancelled",
        "VCMeetingCancelled",
        "ModificationSubmitted",
        "ModificationWithdrawn",
        "ModificationApproved",
        "ModificationRejected",
        "ModificationExpired"
    };

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

            var notification = await _db.EmailNotificationOutbox
                .FromSqlInterpolated($@"
            SELECT *
            FROM dbo.Tbl_EmailNotificationOutbox
                WITH (UPDLOCK, ROWLOCK)
            WHERE EmailNotificationOutboxId = {notificationId}")
                .FirstOrDefaultAsync(cancellationToken);

            if (notification == null ||
                notification.Status != EmailNotificationStatus.DeadLetter ||
                !allowedEventTypes.Contains(
                    notification.EventType,
                    StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            var now = DateTime.UtcNow;

            // Capture the previous retry state before resetting it.
            var audit = new EmailOutboxRetryAudit
            {
                EmailNotificationOutboxId =
                    notification.EmailNotificationOutboxId,

                EventType = notification.EventType,

                OperatorUserId = request.OperatorUserId.Trim(),

                OperatorName = request.OperatorName.Trim(),

                RetryReason = request.Reason.Trim(),

                PreviousAttemptCount = notification.AttemptCount,

                PreviousMaxAttempts = notification.MaxAttempts,

                RetriedAtUtc = now
            };

            // Requeue the notification.
            notification.Status = EmailNotificationStatus.Pending;

            notification.AttemptCount = 0;

            notification.NextAttemptAtUtc = now;

            notification.LockedBy = null;
            notification.LockedAtUtc = null;
            notification.LastAttemptAtUtc = null;
            notification.SentAtUtc = null;

            notification.LastError =
                "Manually requeued. Reason: " + request.Reason.Trim();

            notification.UpdatedAtUtc = now;

            // Both changes are persisted inside the same transaction.
            _db.EmailOutboxRetryAudits.Add(audit);

            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogWarning(
                "Dead-letter notification manually requeued. " +
                "OutboxId={OutboxId}, EventType={EventType}, " +
                "OperatorUserId={OperatorUserId}, RetryAuditId={RetryAuditId}",
                notification.EmailNotificationOutboxId,
                notification.EventType,
                audit.OperatorUserId,
                audit.RetryAuditId);

            return true;
        }

        public async Task<PagedEmailOutboxRetryAuditDto>
    GetRetryAuditHistoryAsync(
        int page,
        int pageSize,
        long? notificationId,
        CancellationToken cancellationToken = default)
        {
            // Enforce safe pagination limits at service level as well.
            if (page < 1)
                page = 1;

            if (pageSize < 1)
                pageSize = 25;

            if (pageSize > 100)
                pageSize = 100;

            if (notificationId.HasValue && notificationId.Value <= 0)
            {
                throw new ArgumentException(
                    "Notification ID must be greater than zero.");
            }

            var query = _db.EmailOutboxRetryAudits
                .AsNoTracking();

            if (notificationId.HasValue)
            {
                query = query.Where(x =>
                    x.EmailNotificationOutboxId == notificationId.Value);
            }

            var totalRecords = await query.CountAsync(
                cancellationToken);

            var totalPages = totalRecords == 0
                ? 0
                : (int)Math.Ceiling(
                    totalRecords / (double)pageSize);

            var items = await query
                .OrderByDescending(x => x.RetriedAtUtc)
                .ThenByDescending(x => x.RetryAuditId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new EmailOutboxRetryAuditDto
                {
                    RetryAuditId = x.RetryAuditId,

                    EmailNotificationOutboxId =
                        x.EmailNotificationOutboxId,

                    EventType = x.EventType,

                    OperatorUserId = x.OperatorUserId,

                    OperatorName = x.OperatorName,

                    RetryReason = x.RetryReason,

                    PreviousAttemptCount = x.PreviousAttemptCount,

                    PreviousMaxAttempts = x.PreviousMaxAttempts,

                    RetriedAtUtc = x.RetriedAtUtc
                })
                .ToListAsync(cancellationToken);

            return new PagedEmailOutboxRetryAuditDto
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = totalPages,
                Items = items
            };
        }

        public async Task<EmailDeliveryMetricsDto> GetDeliveryMetricsAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default)
        {
            fromUtc = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);
            toUtc = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc);

            if (toUtc <= fromUtc)
                throw new ArgumentException("Invalid reporting date range.");

            var query = _db.EmailNotificationOutbox
                .AsNoTracking()
                .Where(x =>
                    x.CreatedAtUtc >= fromUtc &&
                    x.CreatedAtUtc < toUtc);

            var result = new EmailDeliveryMetricsDto
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,

                TotalCount = await query.CountAsync(cancellationToken),

                PendingCount = await query.CountAsync(
                    x => x.Status == EmailNotificationStatus.Pending,
                    cancellationToken),

                ProcessingCount = await query.CountAsync(
                    x => x.Status == EmailNotificationStatus.Processing,
                    cancellationToken),

                SentCount = await query.CountAsync(
                    x => x.Status == EmailNotificationStatus.Sent,
                    cancellationToken),

                FailedCount = await query.CountAsync(
                    x => x.Status == EmailNotificationStatus.Failed,
                    cancellationToken),

                DeadLetterCount = await query.CountAsync(
                    x => x.Status == EmailNotificationStatus.DeadLetter,
                    cancellationToken)
            };

            result.DailyMetrics = await query
                .Where(x =>
                    x.Status == EmailNotificationStatus.Sent ||
                    x.Status == EmailNotificationStatus.Failed ||
                    x.Status == EmailNotificationStatus.DeadLetter)
                .GroupBy(x => x.CreatedAtUtc.Date)
                .Select(g => new DailyEmailDeliveryMetricDto
                {
                    DateUtc = g.Key,

                    SentCount = g.Count(
                        x => x.Status == EmailNotificationStatus.Sent),

                    FailedCount = g.Count(
                        x => x.Status == EmailNotificationStatus.Failed),

                    DeadLetterCount = g.Count(
                        x => x.Status == EmailNotificationStatus.DeadLetter)
                })
                .OrderBy(x => x.DateUtc)
                .ToListAsync(cancellationToken);

            result.EventMetrics = await query
                .GroupBy(x => x.EventType)
                .Select(g => new EventEmailDeliveryMetricDto
                {
                    EventType = g.Key,

                    TotalCount = g.Count(),

                    SentCount = g.Count(
                        x => x.Status == EmailNotificationStatus.Sent),

                    FailedCount = g.Count(
                        x => x.Status == EmailNotificationStatus.Failed),

                    DeadLetterCount = g.Count(
                        x => x.Status == EmailNotificationStatus.DeadLetter)
                })
                .OrderByDescending(x => x.TotalCount)
                .ToListAsync(cancellationToken);

            return result;
        }

        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(
                    _emailSettings.SmtpHost))
            {
                throw new InvalidOperationException(
                    "Email SMTP host is not configured.");
            }

            if (_emailSettings.SmtpPort <= 0)
            {
                throw new InvalidOperationException(
                    "Email SMTP port is not configured correctly.");
            }

            if (string.IsNullOrWhiteSpace(
                    _emailSettings.FromEmail))
            {
                throw new InvalidOperationException(
                    "Email sender address is not configured.");
            }
        }

        private static string BuildHtmlBody(
    string otp,
    int validityMinutes)
        {
            var encodedOtp =
                WebUtility.HtmlEncode(otp);

            return
                "<!DOCTYPE html>" +
                "<html>" +
                "<head>" +
                "<meta charset=\"utf-8\" />" +
                "<title>CONVENE Password Reset</title>" +
                "</head>" +
                "<body style=\"font-family:Arial,Helvetica,sans-serif;" +
                "background-color:#f5f6f8;padding:30px;\">" +

                "<div style=\"max-width:600px;margin:auto;" +
                "background:#ffffff;padding:30px;" +
                "border-radius:8px;\">" +

                "<h2 style=\"margin-top:0;\">CONVENE</h2>" +

                "<p>" +
                "You requested to reset your " +
                "CONVENE account password." +
                "</p>" +

                "<p>" +
                "Your One-Time Password (OTP) is:" +
                "</p>" +

                "<div style=\"font-size:32px;font-weight:bold;" +
                "letter-spacing:8px;text-align:center;" +
                "padding:20px;background:#f1f3f5;" +
                "margin:20px 0;\">" +

                encodedOtp +

                "</div>" +

                "<p>" +
                "This OTP is valid for " +
                "<strong>" +
                validityMinutes +
                " minutes</strong>." +
                "</p>" +

                "<p>" +
                "If you did not request a password reset, " +
                "please ignore this email." +
                "</p>" +

                "<hr />" +

                "<p style=\"font-size:12px;color:#777;\">" +
                "This is an automated message from " +
                "CONVENE - Conference &amp; Venue " +
                "Management System." +
                "</p>" +

                "</div>" +
                "</body>" +
                "</html>";
        }

        private static string BuildPlainTextBody(
     string otp,
     int validityMinutes)
        {
            return
                "CONVENE - Password Reset" +
                Environment.NewLine +
                Environment.NewLine +

                "You requested to reset your " +
                "CONVENE account password." +
                Environment.NewLine +
                Environment.NewLine +

                "Your One-Time Password (OTP) is:" +
                Environment.NewLine +
                Environment.NewLine +

                otp +
                Environment.NewLine +
                Environment.NewLine +

                "This OTP is valid for " +
                validityMinutes +
                " minutes." +
                Environment.NewLine +
                Environment.NewLine +

                "If you did not request a password reset, " +
                "please ignore this email." +
                Environment.NewLine +
                Environment.NewLine +

                "CONVENE" +
                Environment.NewLine +
                "Conference & Venue Management System";
        }

        private static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return "***";
            }

            var atIndex = email.IndexOf('@');

            if (atIndex <= 0)
            {
                return "***";
            }

            var localPart = email[..atIndex];

            if (localPart.Length == 1)
            {
                return $"*@{email[(atIndex + 1)..]}";
            }

            if (localPart.Length == 2)
            {
                return $"{localPart[0]}*@{email[(atIndex + 1)..]}";
            }

            return
                $"{localPart[0]}***{localPart[^1]}@" +
                email[(atIndex + 1)..];
        }
    }
}