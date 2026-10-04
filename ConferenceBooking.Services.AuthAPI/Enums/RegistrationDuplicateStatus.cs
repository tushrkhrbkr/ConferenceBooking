namespace ConferenceBooking.Services.AuthAPI.Enums
{
    public enum RegistrationDuplicateStatus : byte
    {
        None = 0,
        RegistrationPending = 1,
        AccountExists = 2,
        RegistrationBlocked = 3
    }
}