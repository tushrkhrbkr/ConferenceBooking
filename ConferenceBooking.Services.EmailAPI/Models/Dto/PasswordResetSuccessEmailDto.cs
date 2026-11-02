namespace ConferenceBooking.Services.EmailAPI.Models.Dto
{
    public class PasswordResetSuccessEmailDto
    {
        public string UserName { get; set; } = string.Empty;
        public string ResetOn { get; set; } = string.Empty;
    }
}