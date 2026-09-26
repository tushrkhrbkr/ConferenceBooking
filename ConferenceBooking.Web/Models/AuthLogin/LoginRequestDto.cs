using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Web.Models.AuthLogin
{
    public class LoginRequestDto
    {
        [Required]
        public string? UserName { get; set; }
        [Required]
        public string? Password { get; set; }
    }
}
