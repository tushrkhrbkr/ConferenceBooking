
using ConferenceBooking.Web.Models;
using ConferenceBooking.Web.Models.EmailOperations;

namespace ConferenceBooking.Web.Service.IService
{
    public interface IEmailOperationsService
    {
        Task<ResponseDto?> GetOutboxHealthAsync();

        Task<ResponseDto?> GetDeliveryMetricsAsync(
            DateTime fromUtc,
            DateTime toUtc);

        Task<ResponseDto?> GetDeadLetterNotificationsAsync();

        Task<ResponseDto?> RetryDeadLetterNotificationAsync(
            long notificationId,
            string reason,
            string operatorUserId,
            string operatorName);

        Task<ResponseDto?> GetRetryAuditHistoryAsync(
    int page,
    int pageSize,
    long? notificationId);

    }
}
