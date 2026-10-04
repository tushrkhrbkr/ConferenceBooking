using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    public class RegistrationDuplicateCheckDto
    {
        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;
    }
}