namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    public class PasswordResetOtpEmailPayloadDto
    {
        public string Otp { get; set; } = string.Empty;

        public int ValidityMinutes { get; set; }
    }
}
