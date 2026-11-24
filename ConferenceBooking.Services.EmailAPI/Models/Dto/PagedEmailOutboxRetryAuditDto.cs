namespace ConferenceBooking.Services.EmailAPI.Models.Dto
{
    public class PagedEmailOutboxRetryAuditDto
    {
        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalRecords { get; set; }

        public int TotalPages { get; set; }

        public List<EmailOutboxRetryAuditDto> Items { get; set; }
            = new List<EmailOutboxRetryAuditDto>();
    }
}