
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
    }
}
