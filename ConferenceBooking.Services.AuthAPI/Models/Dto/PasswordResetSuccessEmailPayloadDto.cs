namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    public class PasswordResetSuccessEmailPayloadDto
    {
        public string UserName { get; set; } = string.Empty;
        public string ResetOn { get; set; } = string.Empty;
    }
}