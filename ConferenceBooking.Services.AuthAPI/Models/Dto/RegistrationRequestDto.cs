using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    public class RegistrationRequestDto
    {
        public string? UserID { get; set; }

        public long? RegistrationRequestId { get; set; }

        [Required]
        [StringLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string Designation { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone]
        [StringLength(30)]
        public string MobileNumber { get; set; } = string.Empty;

        [Required]
        [Phone]
        [StringLength(10)]
        public string DeskPhone { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = string.Empty;

        public int? RequestedDepartmentId { get; set; }
    }
}
