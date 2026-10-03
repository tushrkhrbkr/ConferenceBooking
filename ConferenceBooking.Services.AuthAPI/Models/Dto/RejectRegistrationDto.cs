using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{

    public class RejectRegistrationDto
    {
        public string? ReviewedByUserId { get; set; }

        [Required]
        public long RegistrationRequestId { get; set; }

        [Required]
        [StringLength(1000)]
        public string Remarks { get; set; } = string.Empty;
    }
}
