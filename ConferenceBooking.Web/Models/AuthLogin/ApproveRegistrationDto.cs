using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Web.Models.AuthLogin
{
    public class ApproveRegistrationDto
    {
        [Required]
        public long RegistrationRequestId { get; set; }

        [Required]
        public int DepartmentId { get; set; }

        [Required]
        [StringLength(50)]
        public string RoleName { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Remarks { get; set; }
    }
}