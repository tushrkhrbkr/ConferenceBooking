
using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.EmailAPI.Models
{
    public class EmailOutboxRetryAudit
    {
        [Key]
        public long RetryAuditId { get; set; }

        public long EmailNotificationOutboxId { get; set; }

        [Required]
        [MaxLength(100)]
        public string EventType { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string OperatorUserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string OperatorName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string RetryReason { get; set; } = string.Empty;

        public int PreviousAttemptCount { get; set; }

        public int PreviousMaxAttempts { get; set; }

        public DateTime RetriedAtUtc { get; set; }
    }
}
