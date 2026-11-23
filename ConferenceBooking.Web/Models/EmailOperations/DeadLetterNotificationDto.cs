
namespace ConferenceBooking.Web.Models.EmailOperations
{
    public class DeadLetterNotificationDto
    {
        public long EmailNotificationOutboxId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public int AttemptCount { get; set; }
        public int MaxAttempts { get; set; }
        public DateTime? LastAttemptAtUtc { get; set; }
        public string? LastError { get; set; }
    }
}
