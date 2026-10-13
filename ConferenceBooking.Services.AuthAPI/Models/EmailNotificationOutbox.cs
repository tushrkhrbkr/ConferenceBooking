using ConferenceBooking.Services.AuthAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.AuthAPI.Models
{
 
    public class EmailNotificationOutbox
    {
        [Key]
        public long EmailNotificationOutboxId { get; set; }


        [Required]
        [MaxLength(100)]
        public string EventType { get; set; } = string.Empty;


        [Required]
        [MaxLength(256)]
        public string ToEmail { get; set; } = string.Empty;


        [MaxLength(2000)]
        public string? CcEmail { get; set; }


        [MaxLength(2000)]
        public string? BccEmail { get; set; }


        [MaxLength(500)]
        public string? Subject { get; set; }


        [Required]
        public string PayloadJson { get; set; } = string.Empty;


        public EmailNotificationStatus Status { get; set; } = EmailNotificationStatus.Pending;


        public int AttemptCount { get; set; }


        public int MaxAttempts { get; set; } = 5;


        public DateTime? NextAttemptAtUtc { get; set; }


        [MaxLength(100)]
        public string? LockedBy { get; set; }


        public DateTime? LockedAtUtc { get; set; }


        public DateTime? LastAttemptAtUtc { get; set; }


        public DateTime? SentAtUtc { get; set; }


        [MaxLength(4000)]
        public string? LastError { get; set; }


        public DateTime CreatedAtUtc { get; set; }


        public DateTime? UpdatedAtUtc { get; set; }
    }
}