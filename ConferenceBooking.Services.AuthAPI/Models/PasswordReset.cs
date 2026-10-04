using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.AuthAPI.Models
{

    public class PasswordReset
    {
        [Key]
        public long PasswordResetId { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string EmailAddress { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string OtpHash { get; set; } = string.Empty;

        public DateTime OtpExpiresAtUtc { get; set; }

        public int OtpAttemptCount { get; set; } = 0;

        public int OtpMaxAttempts { get; set; } = 5;

        public DateTime? OtpVerifiedAtUtc { get; set; }

        [MaxLength(500)]
        public string? ResetTokenHash { get; set; }

        public DateTime? ResetTokenExpiresAtUtc { get; set; }

        public DateTime? ConsumedAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        [MaxLength(64)]
        public string? RequestedIpAddress { get; set; }

        [MaxLength(64)]
        public string? VerifiedIpAddress { get; set; }

        [MaxLength(64)]
        public string? ResetIpAddress { get; set; }
    }
}

