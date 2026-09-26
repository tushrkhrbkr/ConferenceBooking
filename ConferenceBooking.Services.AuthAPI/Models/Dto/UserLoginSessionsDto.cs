namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    public class UserLoginSessionsDto
    {
        public Guid SessionID { get; set; }
        public string UserId { get; set; }
        public DateTime LoginTime { get; set; }
        public DateTime LastActivity { get; set; }
        public DateTime? LogoutTime { get; set; }
        public string? LogoutReason { get; set; }
        public string? LoginEndpoint { get; set; }

    }
}
