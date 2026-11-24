
using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.EmailAPI.Models.Dto
{
    public class RetryDeadLetterNotificationDto
    {
        [Required]
        public bool ConfirmRetry { get; set; }

        [Required]
        [StringLength(500, MinimumLength = 10)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        public string OperatorUserId { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        public string OperatorName { get; set; } = string.Empty;
    }
}
