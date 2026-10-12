namespace ConferenceBooking.Services.EmailAPI.Enums
{
    public enum EmailNotificationStatus : byte
    {
        Pending = 0,

        Processing = 1,

        Sent = 2,

        Failed = 3,

        DeadLetter = 4
    }
}
