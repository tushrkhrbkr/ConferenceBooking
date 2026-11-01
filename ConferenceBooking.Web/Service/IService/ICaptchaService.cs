namespace ConferenceBooking.Web.Service.IService
{
    public interface ICaptchaService
    {
        byte[] GenerateCaptchaImage();
        bool ValidateCaptcha(string? answer);
    }
}
