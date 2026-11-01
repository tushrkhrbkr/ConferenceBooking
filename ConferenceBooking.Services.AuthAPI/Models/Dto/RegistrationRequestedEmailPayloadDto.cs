namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    public class RegistrationRequestedEmailPayloadDto
    {
        public string UserName { get; set; } = string.Empty;

        public string ApplicationReference { get; set; } = string.Empty;

        public string SubmittedOn { get; set; } = string.Empty;
    }
}