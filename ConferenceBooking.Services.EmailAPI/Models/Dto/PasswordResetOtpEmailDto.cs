namespace ConferenceBooking.Services.EmailAPI.Models.Dto
{
    public class PasswordResetOtpEmailDto
    {
        public string ToEmail { get; set; } = string.Empty;

        public string Otp { get; set; } = string.Empty;

        public int ValidityMinutes { get; set; } = 5;
    }
}