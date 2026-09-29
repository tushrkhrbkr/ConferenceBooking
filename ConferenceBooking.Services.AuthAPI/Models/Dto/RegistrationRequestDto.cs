namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    using System.ComponentModel.DataAnnotations;

    namespace ConferenceBooking.Services.AuthAPI.Models.Dto
    {
        public class RegistrationRequestDto
        {
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
            [MinLength(8)]
            public string Password { get; set; } = string.Empty;

            [Required]
            [Compare(nameof(Password))]
            public string ConfirmPassword { get; set; } = string.Empty;

            public int? RequestedDepartmentId { get; set; }
        }
    }
}
