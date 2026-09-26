using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    public class PasswordChangeDto
    {
        public string UserID { get; set; }              
        public string OldPassword { get; set; }        
        public string NewPassword { get; set; }        
        public string ConfirmPassword { get; set; }
    }
}
