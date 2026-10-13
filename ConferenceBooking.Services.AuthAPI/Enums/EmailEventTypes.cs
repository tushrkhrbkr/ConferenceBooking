namespace ConferenceBooking.Services.AuthAPI.Enums
{
    public static class EmailEventTypes
    {
        // Authentication
        public const string PasswordResetOtp =
            "PasswordResetOtp";

        public const string RegistrationApproved =
            "RegistrationApproved";

        public const string RegistrationRejected =
            "RegistrationRejected";


        // Booking
        public const string BookingSubmitted =
            "BookingSubmitted";

        public const string BookingApproved =
            "BookingApproved";

        public const string BookingRejected =
            "BookingRejected";

        public const string BookingCancelled =
            "BookingCancelled";

        public const string BookingChanged =
            "BookingChanged";


        // Administrative reservations
        public const string ReservationCreated =
            "ReservationCreated";

        public const string ReservationReleased =
            "ReservationReleased";


        // Meetings
        public const string MeetingReminder =
            "MeetingReminder";
    }
}