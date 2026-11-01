namespace ConferenceBooking.Services.EmailAPI.Models.Dto
{
    public class RegistrationRequestedEmailDto
    {
        public string UserName { get; set; } = string.Empty;

        public string ApplicationReference { get; set; } = string.Empty;

        public string SubmittedOn { get; set; } = string.Empty;
    }
}