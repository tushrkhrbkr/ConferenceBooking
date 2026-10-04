namespace ConferenceBooking.Web.Models.AuthLogin
{ 
    public class PasswordResetCompleteDto
    {
        public string ResetToken { get; set; } = string.Empty;

        public string NewPassword { get; set; } = string.Empty;

        public string ConfirmPassword { get; set; } = string.Empty; 
        public string IpAddress { get; set; } = string.Empty;
    }
}