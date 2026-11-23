
namespace ConferenceBooking.Services.EmailAPI.Models.Dto
{
    public class EmailDeliveryMetricsDto
    {
        public DateTime FromUtc { get; set; }

        public DateTime ToUtc { get; set; }

        public int TotalCount { get; set; }

        public int PendingCount { get; set; }

        public int ProcessingCount { get; set; }

        public int SentCount { get; set; }

        public int FailedCount { get; set; }

        public int DeadLetterCount { get; set; }

        public List<DailyEmailDeliveryMetricDto> DailyMetrics { get; set; }
            = new();

        public List<EventEmailDeliveryMetricDto> EventMetrics { get; set; }
            = new();
    }

    public class DailyEmailDeliveryMetricDto
    {
        public DateTime DateUtc { get; set; }

        public int SentCount { get; set; }

        public int FailedCount { get; set; }

        public int DeadLetterCount { get; set; }
    }

    public class EventEmailDeliveryMetricDto
    {
        public string EventType { get; set; } = string.Empty;

        public int TotalCount { get; set; }

        public int SentCount { get; set; }

        public int FailedCount { get; set; }

        public int DeadLetterCount { get; set; }
    }
}
