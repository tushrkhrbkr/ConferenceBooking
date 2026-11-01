using ConferenceBooking.Services.EmailAPI.Models;

namespace ConferenceBooking.Services.EmailAPI.Service.IService
{
    public interface IEmailNotificationDispatcher
    {
        Task DispatchAsync(
            EmailNotificationOutbox notification,
            CancellationToken cancellationToken = default);
    }
}