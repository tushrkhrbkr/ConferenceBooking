namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    public class PasswordChangedEmailPayloadDto
    {
        public string UserName { get; set; } = string.Empty;
        public string ChangedOn { get; set; } = string.Empty;
    }
}