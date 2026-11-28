
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConferenceBooking.Services.UpDownload.Models
{
    [Table("Tbl_Report_DocumentRepository")]
    public class ReportDocuments
    {
        [Key]
        public Guid DocumentID { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DocumentAutoID { get; set; }

        public long? ReferenceID { get; set; }

        [MaxLength(100)]
        public string? DocumentType { get; set; }

        [MaxLength(450)]
        public string? UploadedBy { get; set; }

        public DateTime? UploadedOn { get; set; }

        [MaxLength(260)]
        public string? DocumentName { get; set; }

        [Column(TypeName = "varbinary(max)")]
        public byte[]? DocumentData { get; set; }
    }
}
