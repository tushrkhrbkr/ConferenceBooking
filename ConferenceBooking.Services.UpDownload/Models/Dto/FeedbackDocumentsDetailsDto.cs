namespace ConferenceBooking.Services.UpDownload.Models.Dto
{
    public class FeedbackDocumentsDetailsDto
    {
        public Guid DocumentID { get; set; }
        public int DocumentAutoID { get; set; }
        public string ReferenceTable { get; set; }
        public long ReferenceID { get; set; }
        public string DocumentName { get; set; }
        public byte[] DocumentData { get; set; }
        public string DocumentType { get; set; }
    }
}
