
namespace ConferenceBooking.Services.EmailAPI.Models.Dto
{
    public class EmailOutboxHealthDto
    {
        public DateTime GeneratedAtUtc { get; set; }

        public int PendingCount { get; set; }

        public int ProcessingCount { get; set; }

        public int SentCount { get; set; }

        public int FailedCount { get; set; }

        public int DeadLetterCount { get; set; }

        public DateTime? OldestPendingCreatedAtUtc { get; set; }

        public DateTime? OldestProcessingLockedAtUtc { get; set; }
    }
}
