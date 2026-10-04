using ConferenceBooking.Services.EmailAPI.Models.Dto;

namespace ConferenceBooking.Services.EmailAPI.Service.IService
{
    public interface IEmailAPIService
    {
            Task SendPasswordResetOtpAsync(PasswordResetOtpEmailDto request);
    }
}
