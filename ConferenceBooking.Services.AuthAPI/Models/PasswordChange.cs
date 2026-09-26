using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.AuthAPI.Models
{
    public class PasswordChange
    {
        [Required]
        public string UserID { get; set; }
        [Required]        
        public string OldPassword { get; set; }
        [Required]
        public string NewPassword { get; set; }
        [Required]
        public string ConfirmPassword { get; set; }
    }
}
