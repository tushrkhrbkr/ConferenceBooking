namespace ConferenceBooking.Services.AuthAPI.Service.IService
{
    public interface IOtpService
    {
        string GenerateOtp();

        string HashOtp(string otp);
    }
}
