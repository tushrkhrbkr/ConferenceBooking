
namespace ConferenceBooking.Services.EmailAPI.Models.Dto
{
    public class BookingLifecycleEmailDto
    {
        public string RecipientName { get; set; } = string.Empty;

        public string RequesterName { get; set; } = string.Empty;

        public string BookingReference { get; set; } = string.Empty;

        public string MeetingSubject { get; set; } = string.Empty;

        public string HallName { get; set; } = string.Empty;

        public string MeetingStart { get; set; } = string.Empty;

        public string MeetingEnd { get; set; } = string.Empty;

        public string ActionBy { get; set; } = string.Empty;

        public string Remarks { get; set; } = string.Empty;
    }
}
