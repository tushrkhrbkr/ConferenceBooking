using ConferenceBooking.Services.AuthAPI.Enums;

namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    public class RegistrationDuplicateResultDto
    {
        public RegistrationDuplicateStatus Status { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public bool CanRegister { get; set; }

        public bool RequiresPasswordReset { get; set; }

        public long? RegistrationRequestId { get; set; }
    }
}