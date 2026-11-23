
namespace ConferenceBooking.Services.EmailAPI.Models.Dto
{
    public class VCMeetingInvitationEmailDto
    {
        public long BookingId { get; set; }

        public long BookingParticipantId { get; set; }
    }
}
