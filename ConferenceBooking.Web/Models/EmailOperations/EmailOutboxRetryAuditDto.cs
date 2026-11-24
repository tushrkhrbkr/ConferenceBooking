namespace ConferenceBooking.Web.Models.EmailOperations
{
    public class EmailOutboxRetryAuditDto
    {
        public long RetryAuditId { get; set; }

        public long EmailNotificationOutboxId { get; set; }

        public string EventType { get; set; } = string.Empty;

        public string OperatorUserId { get; set; } = string.Empty;

        public string OperatorName { get; set; } = string.Empty;

        public string RetryReason { get; set; } = string.Empty;

        public int PreviousAttemptCount { get; set; }

        public int PreviousMaxAttempts { get; set; }

        public DateTime RetriedAtUtc { get; set; }
    }
}