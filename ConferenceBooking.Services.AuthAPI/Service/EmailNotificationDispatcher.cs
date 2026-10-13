using System.Net.Http.Json;
using System.Text.Json;
using ConferenceBooking.Services.AuthAPI.Enums;
using ConferenceBooking.Services.AuthAPI.Models;
using ConferenceBooking.Services.AuthAPI.Models.Dto;
using ConferenceBooking.Services.AuthAPI.Service.IService;
using Microsoft.Extensions.Options;

namespace ConferenceBooking.Services.AuthAPI.Service
{
    public class EmailNotificationDispatcher
        : IEmailNotificationDispatcher
    {
        private readonly HttpClient _httpClient;
        private readonly EmailNotificationDispatcherOptions _options;
        private readonly ILogger<EmailNotificationDispatcher> _logger;

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        public EmailNotificationDispatcher(
            HttpClient httpClient,
            IOptions<EmailNotificationDispatcherOptions> options,
            ILogger<EmailNotificationDispatcher> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;

            if (string.IsNullOrWhiteSpace(_options.BaseUrl) ||
                !Uri.TryCreate(
                    _options.BaseUrl,
                    UriKind.Absolute,
                    out var baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp &&
                 baseUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    "EmailNotificationDispatcher:BaseUrl must be a valid absolute HTTP/HTTPS URL.");
            }

            if (string.IsNullOrWhiteSpace(_options.InternalApiKey))
            {
                throw new InvalidOperationException(
                    "EmailNotificationDispatcher:InternalApiKey is not configured.");
            }

            _httpClient.BaseAddress =
                new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        }

        public async Task DispatchAsync(
            EmailNotificationOutbox notification,
            CancellationToken cancellationToken = default)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            switch (notification.EventType)
            {
                case EmailEventTypes.PasswordResetOtp:
                    await DispatchPasswordResetOtpAsync(
                        notification,
                        cancellationToken);
                    break;

                default:
                    throw new NotSupportedException(
                        $"Email event type '{notification.EventType}' is not supported by the dispatcher.");
            }
        }

        private async Task DispatchPasswordResetOtpAsync(
            EmailNotificationOutbox notification,
            CancellationToken cancellationToken)
        {
            PasswordResetOtpEmailPayloadDto? payload;

            try
            {
                payload = JsonSerializer.Deserialize
                    <PasswordResetOtpEmailPayloadDto>(
                        notification.PayloadJson,
                        JsonOptions);
            }
            catch (JsonException ex)
            {
                // Do not log PayloadJson: it contains the OTP.
                _logger.LogError(
                    ex,
                    "Invalid password-reset email payload. OutboxId: {OutboxId}",
                    notification.EmailNotificationOutboxId);

                throw new InvalidOperationException(
                    "The password-reset notification payload is invalid.");
            }

            if (payload == null ||
                string.IsNullOrWhiteSpace(payload.Otp) ||
                payload.ValidityMinutes <= 0)
            {
                throw new InvalidOperationException(
                    "The password-reset notification payload is incomplete.");
            }

            var requestBody = new PasswordResetOtpEmailRequest
            {
                ToEmail = notification.ToEmail,
                Otp = payload.Otp,
                ValidityMinutes = payload.ValidityMinutes
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "api/EmailAPI/password-reset-otp");

            request.Headers.Add(
                "X-Internal-Api-Key",
                _options.InternalApiKey);

            request.Content = JsonContent.Create(requestBody);

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "EmailAPI returned HTTP {StatusCode} for outbox record {OutboxId}.",
                    (int)response.StatusCode,
                    notification.EmailNotificationOutboxId);

                // The worker will apply the retry policy.
                // Do not log the request body, OTP, or API key.
                throw new HttpRequestException(
                    $"EmailAPI returned HTTP {(int)response.StatusCode}.");
            }

            _logger.LogInformation(
                "EmailAPI accepted outbox record {OutboxId} for dispatch.",
                notification.EmailNotificationOutboxId);
        }

        private sealed class PasswordResetOtpEmailRequest
        {
            public string ToEmail { get; set; } = string.Empty;

            public string Otp { get; set; } = string.Empty;

            public int ValidityMinutes { get; set; }
        }
    }
}