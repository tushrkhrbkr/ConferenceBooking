using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.AuthAPI.Models
{
    public class UserLoginSessions
    {
        [Key]
        public Guid SessionID { get; set; }
        public string UserId { get; set; }
        public DateTime LoginTime { get; set; }
        public DateTime LastActivity {get; set; }
        public DateTime? LogoutTime { get; set; }
        public string? LogoutReason { get; set; }
        public string LoginEndpoint { get; set; }

    }
}
