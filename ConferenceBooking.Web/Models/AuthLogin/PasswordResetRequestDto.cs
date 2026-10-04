namespace ConferenceBooking.Web.Models.AuthLogin
{
    public class PasswordResetRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string IpAddress {  get; set; } = string.Empty;
    }
}
