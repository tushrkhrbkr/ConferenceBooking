namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    public class RegistrationRequestDto
    {
        public string? UserID { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string ContactNumber { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
    }
}
