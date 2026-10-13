namespace ConferenceBooking.Services.AuthAPI.Service.IService
{
    public interface IPasswordResetTokenService
    {
        string GenerateToken();

        string HashToken(string token);
    }
}
