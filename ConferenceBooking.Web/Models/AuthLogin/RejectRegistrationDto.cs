using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Web.Models.AuthLogin
{
    public class RejectRegistrationDto
    {
        [Required]
        public long RegistrationRequestId { get; set; }

        [Required]
        [StringLength(1000)]
        public string Remarks { get; set; } = string.Empty;
    }
}