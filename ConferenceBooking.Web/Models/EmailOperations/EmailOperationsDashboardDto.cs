
namespace ConferenceBooking.Web.Models.EmailOperations
{
    public class EmailOperationsDashboardDto
    {
        public EmailOutboxHealthDto Health { get; set; } = new();

        public EmailDeliveryMetricsDto Metrics { get; set; } = new();

        public List<DeadLetterNotificationDto> DeadLetters { get; set; }
            = new();

        public DateTime FromUtc { get; set; }

        public DateTime ToUtc { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
