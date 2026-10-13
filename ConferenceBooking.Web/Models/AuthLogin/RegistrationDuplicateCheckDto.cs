
using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Web.Models.AuthLogin
{
    public class RegistrationDuplicateCheckDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
