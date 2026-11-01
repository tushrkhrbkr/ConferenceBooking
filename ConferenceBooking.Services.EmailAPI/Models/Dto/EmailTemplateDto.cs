using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.EmailAPI.Models.Dto
{
    public class EmailTemplateDto
    {
        [Key]
        public long EmailTemplateId { get; set; }

        [Required]
        [MaxLength(100)]
        public string TemplateCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string TemplateName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string SubjectTemplate { get; set; } = string.Empty;

        [Required]
        public string HtmlBody { get; set; } = string.Empty;

        public string? TextBody { get; set; }

        public int Version { get; set; } = 1;

        public bool IsActive { get; set; }

        public DateTime CreatedOn { get; set; }

        public DateTime? ModifiedOn { get; set; }

        [MaxLength(256)]
        public string? ModifiedBy { get; set; }
    }
}
