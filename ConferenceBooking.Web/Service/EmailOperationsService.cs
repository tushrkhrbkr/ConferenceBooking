
using ConferenceBooking.Web.Models;
using ConferenceBooking.Web.Service.IService;
using ConferenceBooking.Web.Utility;

namespace ConferenceBooking.Web.Service
{
    public class EmailOperationsService : IEmailOperationsService
    {
        private readonly IBaseService _baseService;
        private readonly IConfiguration _configuration;

        public EmailOperationsService(
            IBaseService baseService,
            IConfiguration configuration)
        {
            _baseService = baseService;
            _configuration = configuration;
        }

        private Dictionary<string, string> GetHeaders()
        {
            var apiKey = _configuration[
                "InternalServices:EmailAPIKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "EmailAPI internal key is not configured.");
            }

            return new Dictionary<string, string>
            {
                ["X-Internal-Api-Key"] = apiKey
            };
        }

        private string GetBaseUrl()
        {
            var baseUrl = _configuration[
                "ServiceUrls:EMailAPI"];

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException(
                    "EmailAPI URL is not configured.");
            }

            return baseUrl.TrimEnd('/');
        }

        public async Task<ResponseDto?> GetOutboxHealthAsync()
        {
            return await _baseService.SendAsync(new RequestDto
            {
                ApiType = SD.ApiType.GET,
                Url = GetBaseUrl() + "/api/EmailAPI/outbox-health",
                Headers = GetHeaders()
            }, withBearer: false);
        }

        public async Task<ResponseDto?> GetDeliveryMetricsAsync(
            DateTime fromUtc,
            DateTime toUtc)
        {
            var from = Uri.EscapeDataString(
                fromUtc.ToString("O"));

            var to = Uri.EscapeDataString(
                toUtc.ToString("O"));

            return await _baseService.SendAsync(new RequestDto
            {
                ApiType = SD.ApiType.GET,
                Url = GetBaseUrl() +
                      $"/api/EmailAPI/outbox/metrics?fromUtc={from}&toUtc={to}",
                Headers = GetHeaders()
            }, withBearer: false);
        }

        public async Task<ResponseDto?> GetDeadLetterNotificationsAsync()
        {
            return await _baseService.SendAsync(new RequestDto
            {
                ApiType = SD.ApiType.GET,
                Url = GetBaseUrl() +
                      "/api/EmailAPI/outbox/dead-letter",
                Headers = GetHeaders()
            }, withBearer: false);
        }

        public async Task<ResponseDto?> RetryDeadLetterNotificationAsync(
            long notificationId,
            string reason)
        {
            return await _baseService.SendAsync(new RequestDto
            {
                ApiType = SD.ApiType.POST,
                Url = GetBaseUrl() +
                      $"/api/EmailAPI/outbox/dead-letter/{notificationId}/retry",
                Headers = GetHeaders(),
                Data = new
                {
                    ConfirmRetry = true,
                    Reason = reason
                }
            }, withBearer: false);
        }
    }
}
