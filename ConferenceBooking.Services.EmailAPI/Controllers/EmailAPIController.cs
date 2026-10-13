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