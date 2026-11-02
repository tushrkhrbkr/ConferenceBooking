namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    public class RegistrationRejectedEmailPayloadDto
    {
        public string UserName { get; set; } = string.Empty;
        public string ApplicationReference { get; set; } = string.Empty;
        public string ReviewedOn { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }
}