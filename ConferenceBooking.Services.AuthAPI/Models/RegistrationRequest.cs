using ConferenceBooking.Services.AuthAPI.Enums;

namespace ConferenceBooking.Services.AuthAPI.Models
{
    public class RegistrationRequest
    {
        public long RegistrationRequestId { get; set; }

        public string RequestedUserName { get; set; } = string.Empty;

        public string RequestedDisplayName { get; set; } = string.Empty;

        public string RequestedDesignation { get; set; } = string.Empty;

        public string RequestedEmail { get; set; } = string.Empty;

        public string RequestedMobile { get; set; } = string.Empty;

        public string RequestedDeskPhone {  get; set; } = string.Empty;

        public int? RequestedDepartmentId { get; set; }

        public string? RequestedRoleCode { get; set; }

        public RegistrationRequestStatus RequestStatus { get; set; }

        public DateTime SubmittedAtUtc { get; set; }

        public string? ReviewedByUserId { get; set; }

        public DateTime? ReviewedAtUtc { get; set; }

        public string? ReviewRemarks { get; set; }

    }
}
