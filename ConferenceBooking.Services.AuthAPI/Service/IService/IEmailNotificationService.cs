using ConferenceBooking.Services.AuthAPI.Models;

namespace ConferenceBooking.Services.AuthAPI.Service.IService
{
    public interface IEmailNotificationService
    {
        Task<EmailNotificationOutbox> EnqueueAsync<TPayload>(
            string eventType,
            string toEmail,
            TPayload payload,
            string? subject = null,
            string? ccEmail = null,
            string? bccEmail = null,
            CancellationToken cancellationToken = default);
    }
}