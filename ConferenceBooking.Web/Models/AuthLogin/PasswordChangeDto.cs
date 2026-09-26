using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Web.Models.AuthLogin
{
    public class PasswordChangeDto
    {
        public string UserID { get; set; }
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }
}
