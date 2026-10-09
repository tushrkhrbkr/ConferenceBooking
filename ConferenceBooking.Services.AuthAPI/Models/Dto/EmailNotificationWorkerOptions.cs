namespace ConferenceBooking.Services.AuthAPI.Models
{
    public class EmailNotificationWorkerOptions
    {
        public int PollingIntervalSeconds { get; set; } = 10;

        public int BatchSize { get; set; } = 20;

        public int ProcessingLeaseMinutes { get; set; } = 10;

        public int InitialRetryDelaySeconds { get; set; } = 60;
    }
}