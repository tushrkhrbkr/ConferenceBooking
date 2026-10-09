using ConferenceBooking.Services.AuthAPI.Models;

namespace ConferenceBooking.Services.AuthAPI.Service.IService
{
    public interface IEmailNotificationDispatcher
    {
        Task DispatchAsync(
            EmailNotificationOutbox notification,
            CancellationToken cancellationToken = default);
    }
}