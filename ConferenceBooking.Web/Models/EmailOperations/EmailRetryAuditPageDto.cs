namespace ConferenceBooking.Web.Models.EmailOperations
{
    public class EmailRetryAuditPageDto
    {
        public PagedEmailOutboxRetryAuditDto Audit { get; set; }
            = new PagedEmailOutboxRetryAuditDto();

        public long? NotificationId { get; set; }
    }
}