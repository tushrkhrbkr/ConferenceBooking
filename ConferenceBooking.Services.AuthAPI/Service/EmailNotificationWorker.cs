using System.Data;
using ConferenceBooking.Services.AuthAPI.Data;
using ConferenceBooking.Services.AuthAPI.Enums;
using ConferenceBooking.Services.AuthAPI.Models;
using ConferenceBooking.Services.AuthAPI.Service.IService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConferenceBooking.Services.AuthAPI.Service
{
    public class EmailNotificationWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailNotificationWorker> _logger;
        private readonly EmailNotificationWorkerOptions _options;

        private readonly string _workerId;

        public EmailNotificationWorker(
            IServiceScopeFactory scopeFactory,
            IOptions<EmailNotificationWorkerOptions> options,
            ILogger<EmailNotificationWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options.Value;

            _workerId =
                $"{Environment.MachineName}-{Guid.NewGuid():N}";
        }


        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Email notification worker started. WorkerId={WorkerId}",
                _workerId);


            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RecoverStaleNotificationsAsync(
                        stoppingToken);

                    await ProcessPendingNotificationsAsync(
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Unhandled error in email notification worker.");
                }


                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(
                            Math.Max(
                                1,
                                _options.PollingIntervalSeconds)),
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }


            _logger.LogInformation(
                "Email notification worker stopped. WorkerId={WorkerId}",
                _workerId);
        }


        // =============================================================
        // Recover records abandoned by a crashed worker
        // =============================================================

        private async Task RecoverStaleNotificationsAsync(
            CancellationToken cancellationToken)
        {
            using var scope =
                _scopeFactory.CreateScope();

            var db =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var staleBeforeUtc =
                DateTime.UtcNow.AddMinutes(
                    -Math.Max(
                        1,
                        _options.ProcessingLeaseMinutes));


            var staleNotifications =
                await db.Tbl_EmailNotificationOutbox
                    .Where(x =>
                        x.Status ==
                            EmailNotificationStatus.Processing &&

                        x.LockedAtUtc != null &&

                        x.LockedAtUtc <
                            staleBeforeUtc)
                    .ToListAsync(cancellationToken);


            if (staleNotifications.Count == 0)
            {
                return;
            }


            foreach (var notification
                in staleNotifications)
            {
                notification.Status =
                    EmailNotificationStatus.Failed;

                notification.NextAttemptAtUtc =
                    DateTime.UtcNow;

                notification.LockedBy = null;

                notification.LockedAtUtc = null;

                notification.UpdatedAtUtc =
                    DateTime.UtcNow;

                notification.LastError =
                    "Processing lease expired. Notification returned to retry queue.";
            }


            await db.SaveChangesAsync(
                cancellationToken);


            _logger.LogWarning(
                "Recovered {Count} stale email notification(s).",
                staleNotifications.Count);
        }


        // =============================================================
        // Claim and process a batch
        // =============================================================

        private async Task ProcessPendingNotificationsAsync(
            CancellationToken cancellationToken)
        {
            List<long> notificationIds;


            // ---------------------------------------------------------
            // CLAIM
            // ---------------------------------------------------------

            using (var scope =
                _scopeFactory.CreateScope())
            {
                var db =
                    scope.ServiceProvider
                        .GetRequiredService<ApplicationDbContext>();


                await using var transaction =
                    await db.Database.BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        cancellationToken);


                var nowUtc =
                    DateTime.UtcNow;


                var candidates =
                    await db.Tbl_EmailNotificationOutbox
                        .FromSqlInterpolated($@"
                            SELECT TOP ({Math.Max(1, _options.BatchSize)})
                                *
                            FROM dbo.Tbl_EmailNotificationOutbox WITH
                                (UPDLOCK, READPAST, ROWLOCK)
                            WHERE
                                (
                                    Status = {(byte)EmailNotificationStatus.Pending}
                                    OR
                                    Status = {(byte)EmailNotificationStatus.Failed}
                                )
                                AND
                                (
                                    NextAttemptAtUtc IS NULL
                                    OR
                                    NextAttemptAtUtc <= {nowUtc}
                                )
                                AND
                                AttemptCount < MaxAttempts
                            ORDER BY
                                EmailNotificationOutboxId")
                        .ToListAsync(cancellationToken);


                if (candidates.Count == 0)
                {
                    await transaction.CommitAsync(
                        cancellationToken);

                    return;
                }


                notificationIds =
                    candidates
                        .Select(x =>
                            x.EmailNotificationOutboxId)
                        .ToList();


                foreach (var notification
                    in candidates)
                {
                    notification.Status =
                        EmailNotificationStatus.Processing;

                    notification.LockedBy =
                        _workerId;

                    notification.LockedAtUtc =
                        nowUtc;

                    notification.LastAttemptAtUtc =
                        nowUtc;

                    notification.AttemptCount++;

                    notification.UpdatedAtUtc =
                        nowUtc;
                }


                await db.SaveChangesAsync(
                    cancellationToken);


                await transaction.CommitAsync(
                    cancellationToken);
            }


            // ---------------------------------------------------------
            // PROCESS OUTSIDE CLAIM TRANSACTION
            // ---------------------------------------------------------

            foreach (var notificationId
                in notificationIds)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }


                await ProcessSingleNotificationAsync(
                    notificationId,
                    cancellationToken);
            }
        }


        // =============================================================
        // Process one claimed notification
        // =============================================================

        private async Task ProcessSingleNotificationAsync(
            long notificationId,
            CancellationToken cancellationToken)
        {
            EmailNotificationOutbox? notification;


            // ---------------------------------------------------------
            // Load claimed notification
            // ---------------------------------------------------------

            using (var scope =
                _scopeFactory.CreateScope())
            {
                var db =
                    scope.ServiceProvider
                        .GetRequiredService<ApplicationDbContext>();


                notification =
                    await db.Tbl_EmailNotificationOutbox
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x =>
                                x.EmailNotificationOutboxId ==
                                    notificationId &&

                                x.Status ==
                                    EmailNotificationStatus.Processing &&

                                x.LockedBy ==
                                    _workerId,
                            cancellationToken);
            }


            if (notification == null)
            {
                _logger.LogWarning(
                    "Email notification {OutboxId} was no longer owned by worker {WorkerId}.",
                    notificationId,
                    _workerId);

                return;
            }


            try
            {
                // -----------------------------------------------------
                // Dispatcher is implemented in Part D.
                // -----------------------------------------------------

                using var scope =
                    _scopeFactory.CreateScope();

                var dispatcher =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IEmailNotificationDispatcher>();


                await dispatcher.DispatchAsync(
                    notification,
                    cancellationToken);


                await MarkAsSentAsync(
                    notificationId,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await MarkAsFailedAsync(
                    notificationId,
                    ex,
                    cancellationToken);
            }
        }


        // =============================================================
        // Mark SENT
        // =============================================================

        private async Task MarkAsSentAsync(
            long notificationId,
            CancellationToken cancellationToken)
        {
            using var scope =
                _scopeFactory.CreateScope();

            var db =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();


            var notification =
                await db.Tbl_EmailNotificationOutbox
                    .FirstOrDefaultAsync(
                        x =>
                            x.EmailNotificationOutboxId ==
                                notificationId &&

                            x.Status ==
                                EmailNotificationStatus.Processing &&

                            x.LockedBy ==
                                _workerId,
                        cancellationToken);


            if (notification == null)
            {
                return;
            }


            notification.Status =
                EmailNotificationStatus.Sent;

            notification.SentAtUtc =
                DateTime.UtcNow;

            notification.LockedBy = null;

            notification.LockedAtUtc = null;

            notification.UpdatedAtUtc =
                DateTime.UtcNow;

            notification.LastError = null;


            await db.SaveChangesAsync(
                cancellationToken);


            _logger.LogInformation(
                "Email notification sent. " +
                "OutboxId={OutboxId}, EventType={EventType}, Recipient={Recipient}",
                notification.EmailNotificationOutboxId,
                notification.EventType,
                MaskEmail(notification.ToEmail));
        }


        // =============================================================
        // Mark FAILED / DEAD LETTER
        // =============================================================

        private async Task MarkAsFailedAsync(
            long notificationId,
            Exception exception,
            CancellationToken cancellationToken)
        {
            using var scope =
                _scopeFactory.CreateScope();

            var db =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();


            var notification =
                await db.Tbl_EmailNotificationOutbox
                    .FirstOrDefaultAsync(
                        x =>
                            x.EmailNotificationOutboxId ==
                                notificationId &&

                            x.Status ==
                                EmailNotificationStatus.Processing &&

                            x.LockedBy ==
                                _workerId,
                        cancellationToken);


            if (notification == null)
            {
                return;
            }


            var nowUtc =
                DateTime.UtcNow;


            var safeError =
                GetSafeErrorMessage(exception);


            notification.LockedBy = null;

            notification.LockedAtUtc = null;

            notification.UpdatedAtUtc =
                nowUtc;

            notification.LastError =
                safeError;


            if (notification.AttemptCount >=
                notification.MaxAttempts)
            {
                notification.Status =
                    EmailNotificationStatus.DeadLetter;

                notification.NextAttemptAtUtc =
                    null;


                await db.SaveChangesAsync(
                    cancellationToken);


                _logger.LogError(
                    "Email notification moved to DeadLetter. " +
                    "OutboxId={OutboxId}, EventType={EventType}, Attempt={Attempt}",
                    notification.EmailNotificationOutboxId,
                    notification.EventType,
                    notification.AttemptCount);

                return;
            }


            notification.Status =
                EmailNotificationStatus.Failed;


            notification.NextAttemptAtUtc =
                nowUtc.Add(
                    GetRetryDelay(
                        notification.AttemptCount));


            await db.SaveChangesAsync(
                cancellationToken);


            _logger.LogWarning(
                "Email notification failed. " +
                "OutboxId={OutboxId}, EventType={EventType}, Attempt={Attempt}, NextAttempt={NextAttempt}",
                notification.EmailNotificationOutboxId,
                notification.EventType,
                notification.AttemptCount,
                notification.NextAttemptAtUtc);
        }


        // =============================================================
        // Retry schedule
        // =============================================================

        private static TimeSpan GetRetryDelay(
            int attempt)
        {
            return attempt switch
            {
                1 => TimeSpan.FromMinutes(1),

                2 => TimeSpan.FromMinutes(5),

                3 => TimeSpan.FromMinutes(15),

                4 => TimeSpan.FromMinutes(30),

                _ => TimeSpan.FromHours(1)
            };
        }


        // =============================================================
        // Never expose sensitive exception details unnecessarily.
        // =============================================================

        private static string GetSafeErrorMessage(
            Exception exception)
        {
            var message =
                exception.Message?.Trim();

            if (string.IsNullOrWhiteSpace(message))
            {
                return "Email notification processing failed.";
            }


            if (message.Length > 4000)
            {
                message =
                    message[..4000];
            }


            return message;
        }


        // =============================================================
        // Mask email in logs
        // =============================================================

        private static string MaskEmail(
            string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return "***";
            }


            var atIndex =
                email.IndexOf('@');


            if (atIndex <= 0)
            {
                return "***";
            }


            var localPart =
                email[..atIndex];

            var domain =
                email[atIndex..];


            if (localPart.Length == 1)
            {
                return "*" + domain;
            }


            if (localPart.Length == 2)
            {
                return localPart[0] +
                       "*" +
                       domain;
            }


            return
                localPart[0] +
                "***" +
                localPart[^1] +
                domain;
        }
    }
}