using ConferenceBooking.Services.AuthAPI.Data;
using ConferenceBooking.Services.AuthAPI.Enums;
using ConferenceBooking.Services.AuthAPI.Models;
using ConferenceBooking.Services.AuthAPI.Service.IService;
using System.Text.Json;

namespace ConferenceBooking.Services.AuthAPI.Service
{
    
    public class EmailNotificationService : IEmailNotificationService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<EmailNotificationService> _logger;

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

                WriteIndented = false
            };

        public EmailNotificationService(
            ApplicationDbContext db,
            ILogger<EmailNotificationService> logger)
        {
            _db = db;
            _logger = logger;
        }


        public async Task<EmailNotificationOutbox> EnqueueAsync<TPayload>(
            string eventType,
            string toEmail,
            TPayload payload,
            string? subject = null,
            string? ccEmail = null,
            string? bccEmail = null,
            CancellationToken cancellationToken = default)
        {
            // =========================================================
            // 1. Validate event type
            // =========================================================

            if (string.IsNullOrWhiteSpace(eventType))
            {
                throw new ArgumentException(
                    "Email event type is required.",
                    nameof(eventType));
            }


            // =========================================================
            // 2. Validate recipient
            // =========================================================

            if (string.IsNullOrWhiteSpace(toEmail))
            {
                throw new ArgumentException(
                    "Email recipient is required.",
                    nameof(toEmail));
            }


            // =========================================================
            // 3. Validate payload
            // =========================================================

            if (payload == null)
            {
                throw new ArgumentNullException(
                    nameof(payload));
            }


            // =========================================================
            // 4. Serialize payload
            // =========================================================

            string payloadJson;

            try
            {
                payloadJson =
                    JsonSerializer.Serialize(
                        payload,
                        JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "Unable to serialize email notification payload. " +
                    "EventType={EventType}",
                    eventType);

                throw;
            }


            // =========================================================
            // 5. Create outbox record
            // =========================================================

            var outbox = new EmailNotificationOutbox
            {
                EventType = eventType.Trim(),

                ToEmail = toEmail.Trim(),

                CcEmail = string.IsNullOrWhiteSpace(ccEmail)
                    ? null
                    : ccEmail.Trim(),

                BccEmail = string.IsNullOrWhiteSpace(bccEmail)
                    ? null
                    : bccEmail.Trim(),

                Subject = string.IsNullOrWhiteSpace(subject)
                    ? null
                    : subject.Trim(),

                PayloadJson = payloadJson,

                Status =
                    EmailNotificationStatus.Pending,

                AttemptCount = 0,

                MaxAttempts = 5,

                NextAttemptAtUtc = null,

                LockedBy = null,

                LockedAtUtc = null,

                LastAttemptAtUtc = null,

                SentAtUtc = null,

                LastError = null,

                CreatedAtUtc = DateTime.UtcNow,

                UpdatedAtUtc = null
            };


            // =========================================================
            // 6. Add to current DbContext
            // =========================================================

            await _db.Tbl_EmailNotificationOutbox
                .AddAsync(
                    outbox,
                    cancellationToken);


            // =========================================================
            // 7. Save
            //
            // IMPORTANT:
            //
            // If the caller already started a DB transaction,
            // this SaveChanges participates in that transaction.
            //
            // Therefore PasswordReset + Outbox can be atomic.
            // =========================================================

            await _db.SaveChangesAsync(
                cancellationToken);


            // =========================================================
            // 8. Safe logging
            //
            // NEVER log PayloadJson because it may contain OTP.
            // =========================================================

            _logger.LogInformation(
                "Email notification queued. " +
                "OutboxId={OutboxId}, " +
                "EventType={EventType}, " +
                "Recipient={Recipient}",
                outbox.EmailNotificationOutboxId,
                outbox.EventType,
                MaskEmail(outbox.ToEmail));


            return outbox;
        }


        // =============================================================
        // Email masking
        // =============================================================

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

            var domain = email[atIndex..];

            if (localPart.Length == 1)
            {
                return "*" + domain;
            }

            if (localPart.Length == 2)
            {
                return localPart[0] + "*" + domain;
            }

            return
                localPart[0] +
                "***" +
                localPart[^1] +
                domain;
        }
    }
}