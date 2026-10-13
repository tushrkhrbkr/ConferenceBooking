namespace ConferenceBooking.Web.Models.AuthLogin
{
    public class PendingRegistrationDto
    {
        public long RegistrationRequestId { get; set; }
        public string RequestedUserName { get; set; } = string.Empty;
        public string RequestedDisplayName { get; set; } = string.Empty;
        public string RequestedDesignation { get; set; } = string.Empty;
        public string RequestedEmail { get; set; } = string.Empty;
        public string RequestedMobile { get; set; } = string.Empty;
        public string RequestedDeskPhone { get; set; } = string.Empty;
        public int? RequestedDepartmentId { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
    }
}