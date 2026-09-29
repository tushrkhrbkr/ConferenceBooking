using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{

    public class RejectRegistrationDto
    {
        [Required]
        [StringLength(1000)]
        public string Remarks { get; set; } = string.Empty;
    }
}
