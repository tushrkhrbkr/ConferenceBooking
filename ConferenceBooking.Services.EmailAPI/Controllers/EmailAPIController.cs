using ConferenceBooking.Services.EmailAPI.Models.Dto;
using ConferenceBooking.Services.EmailAPI.Service.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceBooking.Services.EmailAPI.Controllers
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class EmailAPIController : ControllerBase
    {
        private const string InternalApiKeyHeader =
            "X-Internal-Api-Key";

        private readonly IEmailAPIService _emailAPIService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailAPIController> _logger;

        public EmailAPIController(
            IEmailAPIService emailAPIService,
            IConfiguration configuration,
            ILogger<EmailAPIController> logger)
        {
            _emailAPIService = emailAPIService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("password-reset-otp")]
        public async Task<IActionResult> SendPasswordResetOtp(
            [FromHeader(Name = InternalApiKeyHeader)]
            string? apiKey,
            [FromBody] PasswordResetOtpEmailDto request)
        {
            if (!IsValidInternalApiKey(apiKey))
            {
                return Unauthorized(new
                {
                    IsSuccess = false,
                    Message = "Unauthorized."
                });
            }

            if (request == null)
            {
                return BadRequest(new
                {
                    IsSuccess = false,
                    Message = "Invalid request."
                });
            }

            try
            {
                await _emailAPIService.SendPasswordResetOtpAsync(request);

                return Ok(new
                {
                    IsSuccess = true,
                    Message = "Email sent successfully."
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Invalid password reset email request.");

                return BadRequest(new
                {
                    IsSuccess = false,
                    Message = "Invalid email request."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Password reset email delivery failed.");

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        IsSuccess = false,
                        Message = "Email delivery failed."
                    });
            }
        }


        [HttpGet("outbox-health")]
        public async Task<IActionResult> GetOutboxHealth(
            [FromHeader(Name = InternalApiKeyHeader)]
    string? apiKey,
            CancellationToken cancellationToken)
        {
            if (!IsValidInternalApiKey(apiKey))
            {
                return Unauthorized(new
                {
                    IsSuccess = false,
                    Message = "Unauthorized."
                });
            }

            try
            {
                var result = await _emailAPIService.GetOutboxHealthAsync(
                    cancellationToken);

                return Ok(new
                {
                    IsSuccess = true,
                    Message = "Email outbox health retrieved successfully.",
                    Result = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Email outbox health retrieval failed.");

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        IsSuccess = false,
                        Message = "Unable to retrieve email outbox health."
                    });
            }
        }

        [HttpGet("outbox/dead-letter")]
        public async Task<IActionResult> GetDeadLetterNotifications(
    [FromHeader(Name = InternalApiKeyHeader)]
    string? apiKey,
    CancellationToken cancellationToken)
        {
            if (!IsValidInternalApiKey(apiKey))
            {
                return Unauthorized(new
                {
                    IsSuccess = false,
                    Message = "Unauthorized."
                });
            }

            try
            {
                var result =
                    await _emailAPIService.GetDeadLetterNotificationsAsync(
                        cancellationToken);

                return Ok(new
                {
                    IsSuccess = true,
                    Message = "Dead-letter notifications retrieved successfully.",
                    Result = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Dead-letter notification retrieval failed.");

                return StatusCode(500, new
                {
                    IsSuccess = false,
                    Message = "Unable to retrieve dead-letter notifications."
                });
            }
        }

        [HttpPost("outbox/dead-letter/{notificationId:long}/retry")]
        public async Task<IActionResult> RetryDeadLetterNotification(
    long notificationId,
    [FromHeader(Name = InternalApiKeyHeader)]
    string? apiKey,
    [FromBody] RetryDeadLetterNotificationDto request,
    CancellationToken cancellationToken)
        {
            if (!IsValidInternalApiKey(apiKey))
            {
                return Unauthorized(new
                {
                    IsSuccess = false,
                    Message = "Unauthorized."
                });
            }

            if (request == null ||
                !request.ConfirmRetry ||
                string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest(new
                {
                    IsSuccess = false,
                    Message = "Explicit retry confirmation and reason are required."
                });
            }

            try
            {
                var retried =
                    await _emailAPIService.RetryDeadLetterNotificationAsync(
                        notificationId,
                        request,
                        cancellationToken);

                if (!retried)
                {
                    return Conflict(new
                    {
                        IsSuccess = false,
                        Message = "Notification was not found, is not in DeadLetter status, or its event type is not eligible for manual retry."
                    });
                }

                return Ok(new
                {
                    IsSuccess = true,
                    Message = "Notification requeued successfully. The email worker will process it."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Dead-letter retry failed. OutboxId={OutboxId}",
                    notificationId);

                return StatusCode(500, new
                {
                    IsSuccess = false,
                    Message = "Unable to requeue the notification."
                });
            }
        }


        [HttpGet("outbox/metrics")]
        public async Task<IActionResult> GetDeliveryMetrics(
            [FromHeader(Name = InternalApiKeyHeader)]
    string? apiKey,
            [FromQuery] DateTime fromUtc,
            [FromQuery] DateTime toUtc,
            CancellationToken cancellationToken)
        {
            if (!IsValidInternalApiKey(apiKey))
            {
                return Unauthorized(new
                {
                    IsSuccess = false,
                    Message = "Unauthorized."
                });
            }

            var from = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);
            var to = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc);

            if (to <= from)
            {
                return BadRequest(new
                {
                    IsSuccess = false,
                    Message = "The end date must be later than the start date."
                });
            }

            if ((to - from).TotalDays > 90)
            {
                return BadRequest(new
                {
                    IsSuccess = false,
                    Message = "The maximum reporting period is 90 days."
                });
            }

            try
            {
                var result = await _emailAPIService.GetDeliveryMetricsAsync(
                    from,
                    to,
                    cancellationToken);

                return Ok(new
                {
                    IsSuccess = true,
                    Message = "Email delivery metrics retrieved successfully.",
                    Result = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Email delivery metrics retrieval failed.");

                return StatusCode(500, new
                {
                    IsSuccess = false,
                    Message = "Unable to retrieve email delivery metrics."
                });
            }
        }


        private bool IsValidInternalApiKey(
            string? suppliedApiKey)
        {
            var configuredApiKey =
                _configuration[
                    "InternalApiKey"];

            if (string.IsNullOrWhiteSpace(
                    configuredApiKey))
            {
                _logger.LogError(
                    "EmailAPI InternalApiKey is not configured.");

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    suppliedApiKey))
            {
                return false;
            }

            return CryptographicEquals(
                suppliedApiKey,
                configuredApiKey);
        }

        private static bool CryptographicEquals(
            string left,
            string right)
        {
            var leftBytes =
                System.Text.Encoding.UTF8.GetBytes(left);

            var rightBytes =
                System.Text.Encoding.UTF8.GetBytes(right);

            return System.Security.Cryptography
                .CryptographicOperations
                .FixedTimeEquals(
                    leftBytes,
                    rightBytes);
        }
    }
}