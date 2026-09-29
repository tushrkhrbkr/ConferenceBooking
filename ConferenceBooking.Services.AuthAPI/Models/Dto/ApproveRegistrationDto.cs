using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    public class ApproveRegistrationDto
    {

        [Required]
        public int DepartmentId { get; set; }

        [Required]
        [StringLength(50)]
        public string RoleCode { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Remarks { get; set; }
    }
}
