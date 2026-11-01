using System.Data;
using ConferenceBooking.Services.EmailAPI.Data;
using ConferenceBooking.Services.EmailAPI.Enums;
using ConferenceBooking.Services.EmailAPI.Models;
using ConferenceBooking.Services.EmailAPI.Service.IService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConferenceBooking.Services.EmailAPI.Service
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
                "Email worker started. WorkerId={WorkerId}",
                _workerId);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RecoverStaleNotificationsAsync(stoppingToken);
                    await ProcessPendingNotificationsAsync(stoppingToken);
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
                            Math.Max(1, _options.PollingIntervalSeconds)),
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation(
                "Email worker stopped. WorkerId={WorkerId}",
                _workerId);
        }

        private async Task RecoverStaleNotificationsAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var staleBeforeUtc = DateTime.UtcNow.AddMinutes(
                -Math.Max(1, _options.ProcessingLeaseMinutes));

            var failedStatus = (byte)EmailNotificationStatus.Failed;
            var deadLetterStatus = (byte)EmailNotificationStatus.DeadLetter;
            var processingStatus = (byte)EmailNotificationStatus.Processing;

            var recoveredCount =
                await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE dbo.Tbl_EmailNotificationOutbox
            SET
                Status = CASE
                    WHEN AttemptCount >= MaxAttempts
                        THEN {deadLetterStatus}
                    ELSE {failedStatus}
                END,

                NextAttemptAtUtc = CASE
                    WHEN AttemptCount >= MaxAttempts
                        THEN NULL
                    ELSE SYSUTCDATETIME()
                END,

                LockedBy = NULL,
                LockedAtUtc = NULL,
                UpdatedAtUtc = SYSUTCDATETIME(),

                LastError = CASE
                    WHEN AttemptCount >= MaxAttempts
                        THEN 'Processing lease expired; maximum attempts exhausted.'
                    ELSE 'Processing lease expired; returned to retry queue.'
                END

            WHERE
                Status = {processingStatus}
                AND LockedAtUtc IS NOT NULL
                AND LockedAtUtc < {staleBeforeUtc};",
                    cancellationToken);

            if (recoveredCount > 0)
            {
                _logger.LogWarning(
                    "Recovered {Count} stale email notifications. WorkerId={WorkerId}",
                    recoveredCount,
                    _workerId);
            }
        }

        private async Task ProcessPendingNotificationsAsync(
            CancellationToken cancellationToken)
        {
            List<long> notificationIds;

            // Claim records in a short transaction.
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                await using var transaction =
                    await db.Database.BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        cancellationToken);

                var nowUtc = DateTime.UtcNow;

                var candidates =
                    await db.EmailNotificationOutbox
                        .FromSqlInterpolated($@"
                            SELECT TOP ({Math.Max(1, _options.BatchSize)}) *
                            FROM dbo.Tbl_EmailNotificationOutbox WITH
                                (UPDLOCK, READPAST, ROWLOCK)
                            WHERE
                                Status IN (
                                    {(byte)EmailNotificationStatus.Pending},
                                    {(byte)EmailNotificationStatus.Failed}
                                )
                                AND (
                                    NextAttemptAtUtc IS NULL
                                    OR NextAttemptAtUtc <= {nowUtc}
                                )
                                AND AttemptCount < MaxAttempts
                            ORDER BY EmailNotificationOutboxId")
                        .ToListAsync(cancellationToken);

                if (candidates.Count == 0)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return;
                }

                notificationIds = candidates
                    .Select(x => x.EmailNotificationOutboxId)
                    .ToList();

                foreach (var notification in candidates)
                {
                    notification.Status =
                        EmailNotificationStatus.Processing;

                    notification.LockedBy = _workerId;
                    notification.LockedAtUtc = nowUtc;
                    notification.LastAttemptAtUtc = nowUtc;
                    notification.AttemptCount++;
                    notification.UpdatedAtUtc = nowUtc;
                }

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            // Send outside the claim transaction.
            // Send outside the claim transaction.
            try
            {
                foreach (var notificationId in notificationIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await ProcessSingleNotificationAsync(
                        notificationId,
                        cancellationToken);
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                await ReleaseOwnedNotificationsAsync();

                throw;
            }
        }

        private async Task ProcessSingleNotificationAsync(
            long notificationId,
            CancellationToken cancellationToken)
        {
            EmailNotificationOutbox? notification;

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                notification = await db.EmailNotificationOutbox
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.EmailNotificationOutboxId == notificationId &&
                            x.Status == EmailNotificationStatus.Processing &&
                            x.LockedBy == _workerId,
                        cancellationToken);
            }

            if (notification == null)
            {
                _logger.LogWarning(
                    "Outbox record {OutboxId} is no longer owned by worker {WorkerId}.",
                    notificationId,
                    _workerId);

                return;
            }

            try
            {
                // The dispatcher will be implemented in the next step.
                using var scope = _scopeFactory.CreateScope();

                var dispatcher = scope.ServiceProvider
                    .GetRequiredService<IEmailNotificationDispatcher>();

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
                    notification.AttemptCount,
                    notification.MaxAttempts,
                    ex,
                    cancellationToken);
            }
        }

        private async Task MarkAsSentAsync(
            long notificationId,
            CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var nowUtc = DateTime.UtcNow;

            var affectedRows =
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE dbo.Tbl_EmailNotificationOutbox
                    SET
                        Status = {(byte)EmailNotificationStatus.Sent},
                        SentAtUtc = {nowUtc},
                        LockedBy = NULL,
                        LockedAtUtc = NULL,
                        UpdatedAtUtc = {nowUtc},
                        LastError = NULL
                    WHERE
                        EmailNotificationOutboxId = {notificationId}
                        AND Status = {(byte)EmailNotificationStatus.Processing}
                        AND LockedBy = {_workerId};",
                    cancellationToken);

            if (affectedRows == 0)
            {
                _logger.LogWarning(
                    "Could not mark outbox record {OutboxId} as sent; worker ownership changed.",
                    notificationId);

                return;
            }

            _logger.LogInformation(
                "Email notification sent. OutboxId={OutboxId}",
                notificationId);
        }

        private async Task MarkAsFailedAsync(
            long notificationId,
            int attemptCount,
            int maxAttempts,
            Exception exception,
            CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var nowUtc = DateTime.UtcNow;
            var isDeadLetter = attemptCount >= maxAttempts;

            var newStatus = isDeadLetter
                ? EmailNotificationStatus.DeadLetter
                : EmailNotificationStatus.Failed;

            DateTime? nextAttemptAtUtc = isDeadLetter
                ? null
                : nowUtc.Add(GetRetryDelay(attemptCount));

            var safeError =
                $"Email notification processing failed ({exception.GetType().Name}).";

            var affectedRows =
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE dbo.Tbl_EmailNotificationOutbox
                    SET
                        Status = {(byte)newStatus},
                        NextAttemptAtUtc = {nextAttemptAtUtc},
                        LockedBy = NULL,
                        LockedAtUtc = NULL,
                        UpdatedAtUtc = {nowUtc},
                        LastError = {safeError}
                    WHERE
                        EmailNotificationOutboxId = {notificationId}
                        AND Status = {(byte)EmailNotificationStatus.Processing}
                        AND LockedBy = {_workerId};",
                    cancellationToken);

            if (affectedRows == 0)
            {
                _logger.LogWarning(
                    "Could not update failed outbox record {OutboxId}; worker ownership changed.",
                    notificationId);

                return;
            }

            if (isDeadLetter)
            {
                _logger.LogError(
                    "Email notification moved to DeadLetter. OutboxId={OutboxId}, Attempt={Attempt}",
                    notificationId,
                    attemptCount);
            }
            else
            {
                _logger.LogWarning(
                    "Email notification failed. OutboxId={OutboxId}, Attempt={Attempt}, NextAttempt={NextAttempt}",
                    notificationId,
                    attemptCount,
                    nextAttemptAtUtc);
            }
        }

        private static TimeSpan GetRetryDelay(int attempt)
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

        private async Task ReleaseOwnedNotificationsAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                var processingStatus =
                    (byte)EmailNotificationStatus.Processing;

                var failedStatus =
                    (byte)EmailNotificationStatus.Failed;

                var deadLetterStatus =
                    (byte)EmailNotificationStatus.DeadLetter;

                var releasedCount =
                    await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE dbo.Tbl_EmailNotificationOutbox
                SET
                    Status = CASE
                        WHEN AttemptCount >= MaxAttempts
                            THEN {deadLetterStatus}
                        ELSE {failedStatus}
                    END,

                    NextAttemptAtUtc = CASE
                        WHEN AttemptCount >= MaxAttempts
                            THEN NULL
                        ELSE SYSUTCDATETIME()
                    END,

                    LockedBy = NULL,
                    LockedAtUtc = NULL,
                    UpdatedAtUtc = SYSUTCDATETIME(),

                    LastError = CASE
                        WHEN AttemptCount >= MaxAttempts
                            THEN 'Worker stopped; maximum attempts exhausted.'
                        ELSE 'Worker stopped; notification returned to retry queue.'
                    END

                WHERE
                    Status = {processingStatus}
                    AND LockedBy = {_workerId};",
                        CancellationToken.None);

                if (releasedCount > 0)
                {
                    _logger.LogWarning(
                        "Released {Count} processing notifications during worker shutdown. WorkerId={WorkerId}",
                        releasedCount,
                        _workerId);
                }
            }
            catch (Exception ex)
            {
                // Do not hide the original cancellation.
                // Lease recovery will handle records if this cleanup fails.
                _logger.LogError(
                    ex,
                    "Could not release processing notifications during worker shutdown. WorkerId={WorkerId}",
                    _workerId);
            }
        }
    }
}
