namespace ConferenceBooking.Web.Models.AuthLogin
{
    public class PasswordResetVerifyOtpDto
    {
        public string Email { get; set; } = string.Empty;

        public string Otp { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
    }
}