namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    public class PasswordResetRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string IpAddress {  get; set; } = string.Empty;
    }
}
