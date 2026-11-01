using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.EmailAPI.Models.Dto
{
    public class EmailTemplateAuditDto
    {
        [Key]
        public long AuditId { get; set; }

        [Required]
        [MaxLength(100)]
        public string TemplateCode { get; set; } = string.Empty;

        public int Version { get; set; }

        [Required]
        [MaxLength(500)]
        public string SubjectTemplate { get; set; } = string.Empty;

        [Required]
        public string HtmlBody { get; set; } = string.Empty;

        public string? TextBody { get; set; }

        [Required]
        [MaxLength(30)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? ModifiedBy { get; set; }

        public DateTime ModifiedOn { get; set; }
    }
}
