using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.UpDownload.Models
{
    public class ReportDocuments
    {
        [Key]
        public Guid DocumentID { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DocumentAutoID { get; set; } = 0;
        public string ReferenceTable { get; set; }
        public int ReferenceID { get; set; }
        public string DocumentName { get; set; }
        public byte[] DocumentData { get; set; }
        public string DocumentType { get; set; }
        public string UploadedBy { get; set; }
        public DateTime UploadedOn { get; set; } = DateTime.Now;
    }
}
