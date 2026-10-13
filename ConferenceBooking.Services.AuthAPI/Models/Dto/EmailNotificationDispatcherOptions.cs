namespace ConferenceBooking.Services.AuthAPI.Models
{
    public class EmailNotificationDispatcherOptions
    {
        public string BaseUrl { get; set; } = string.Empty;

        public string InternalApiKey { get; set; } = string.Empty;
    }
}