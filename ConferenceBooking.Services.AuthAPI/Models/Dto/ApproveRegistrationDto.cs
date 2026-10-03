using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    public class ApproveRegistrationDto
    {
        public string? ReviewedByUserId { get; set; }

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
