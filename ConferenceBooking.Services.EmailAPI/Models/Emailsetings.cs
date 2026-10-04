namespace ConferenceBooking.Services.EmailAPI.Models
{
    public class EmailSettings
    {
        public string SmtpHost { get; set; } = string.Empty;

        public int SmtpPort { get; set; } = 587;

        public bool EnableSsl { get; set; } = true;

        public string SmtpUsername { get; set; } = string.Empty;

        public string SmtpPassword { get; set; } = string.Empty;

        public string FromEmail { get; set; } = string.Empty;

        public string FromName { get; set; } = "CONVENE";
    }
}