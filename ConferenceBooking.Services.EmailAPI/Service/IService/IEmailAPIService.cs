using ConferenceBooking.Services.EmailAPI.Models.Dto;

namespace ConferenceBooking.Services.EmailAPI.Service.IService
{
    public interface IEmailAPIService
    {
            Task SendPasswordResetOtpAsync(PasswordResetOtpEmailDto request);

        Task<EmailOutboxHealthDto> GetOutboxHealthAsync(
    CancellationToken cancellationToken = default);

        Task<List<DeadLetterNotificationDto>> GetDeadLetterNotificationsAsync(
    CancellationToken cancellationToken = default);

        Task<bool> RetryDeadLetterNotificationAsync(
            long notificationId,
            RetryDeadLetterNotificationDto request,
            CancellationToken cancellationToken = default);

        Task<EmailDeliveryMetricsDto> GetDeliveryMetricsAsync(
    DateTime fromUtc,
    DateTime toUtc,
    CancellationToken cancellationToken = default);
    }
}
