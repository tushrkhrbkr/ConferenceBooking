namespace ConferenceBooking.Web.Service.IService
{
    public interface ICaptchaService
    {
        byte[] GenerateCaptchaImage(string challengeId);
        bool ValidateCaptcha(string challengeId, string? answer);
    }
}