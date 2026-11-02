namespace ConferenceBooking.Services.EmailAPI.Models.Dto
{
    public class PasswordChangedEmailDto
    {
        public string UserName { get; set; } = string.Empty;
        public string ChangedOn { get; set; } = string.Empty;
    }
}