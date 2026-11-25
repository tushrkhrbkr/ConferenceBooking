namespace ConferenceBooking.Services.UpDownload.Models.Dto
{
    public class ReportDocumentsDto
    {
        public Guid DocumentID { get; set; }
        public int DocumentAutoID { get; set; }
        public string ReferenceTable { get; set; }
        public int ReferenceID { get; set; }
        public string DocumentName { get; set; }
        public byte[] DocumentData { get; set; }
        public string DocumentType { get; set; }
        public string UploadedBy { get; set; }
        public DateTime UploadedOn { get; set; } = DateTime.Now;
    }
}
