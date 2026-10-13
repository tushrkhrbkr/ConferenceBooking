
namespace ConferenceBooking.Web.Models.AuthLogin
{
    public class RegistrationDuplicateResultDto
    {
        public byte Status { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public bool CanRegister { get; set; }

        public bool RequiresPasswordReset { get; set; }

        public long? RegistrationRequestId { get; set; }
    }
}
