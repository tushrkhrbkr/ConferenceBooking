
using ConferenceBooking.Web.Models;
using ConferenceBooking.Web.Models.EmailOperations;
using ConferenceBooking.Web.Service.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace ConferenceBooking.Web.Controllers
{
    [Authorize(Roles = "Admin,ADMIN")]
    public class EmailOperationsController : Controller
    {
        private readonly IEmailOperationsService _emailService;
        private readonly ILogger<EmailOperationsController> _logger;

        public EmailOperationsController(
            IEmailOperationsService emailService,
            ILogger<EmailOperationsController> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }


        [HttpGet]
        public async Task<IActionResult> Index(
            DateTime? fromUtc,
            DateTime? toUtc)
        {
            var now = DateTime.UtcNow;

            // The dashboard date picker uses inclusive calendar dates.
            var from = DateTime.SpecifyKind(
                (fromUtc ?? now.Date.AddDays(-29)).Date,
                DateTimeKind.Utc);

            var selectedToDate = (toUtc ?? now.Date).Date;

            // Prevent overflow when converting an inclusive end date
            // into the exclusive endpoint required by the metrics API.
            if (selectedToDate == DateTime.MaxValue.Date)
            {
                return BadRequest("Select a valid end date.");
            }

            var to = DateTime.SpecifyKind(
                selectedToDate.AddDays(1),
                DateTimeKind.Utc);

            if (to <= from || (to - from).TotalDays > 90)
            {
                return BadRequest(
                    "Select a valid reporting period of up to 90 days.");
            }

            var model = new EmailOperationsDashboardDto
            {
                FromUtc = from,
                ToUtc = to
            };

            try
            {
                var healthResponse =
                    await _emailService.GetOutboxHealthAsync();

                var metricsResponse =
                    await _emailService.GetDeliveryMetricsAsync(from, to);

                var deadLetterResponse =
                    await _emailService.GetDeadLetterNotificationsAsync();

                if (healthResponse?.IsSuccess == true)
                {
                    model.Health =
                        JsonConvert.DeserializeObject<EmailOutboxHealthDto>(
                            JsonConvert.SerializeObject(healthResponse.Result))
                        ?? new EmailOutboxHealthDto();
                }

                if (metricsResponse?.IsSuccess == true)
                {
                    model.Metrics =
                        JsonConvert.DeserializeObject<EmailDeliveryMetricsDto>(
                            JsonConvert.SerializeObject(metricsResponse.Result))
                        ?? new EmailDeliveryMetricsDto();
                }

                if (deadLetterResponse?.IsSuccess == true)
                {
                    model.DeadLetters =
                        JsonConvert.DeserializeObject<List<DeadLetterNotificationDto>>(
                            JsonConvert.SerializeObject(deadLetterResponse.Result))
                        ?? new List<DeadLetterNotificationDto>();
                }

                if (healthResponse?.IsSuccess != true ||
                    metricsResponse?.IsSuccess != true ||
                    deadLetterResponse?.IsSuccess != true)
                {
                    model.ErrorMessage =
                        "One or more EmailAPI monitoring services could not be reached. " +
                        "Please check the service configuration.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unable to load Email Operations dashboard.");

                model.ErrorMessage =
                    "Email Operations data is temporarily unavailable.";
            }

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Retry(
            long notificationId,
            string reason)
        {
            if (notificationId <= 0 ||
                string.IsNullOrWhiteSpace(reason) ||
                reason.Trim().Length < 10 ||
                reason.Trim().Length > 500)
            {
                TempData["error"] =
                    "A valid notification ID and retry reason " +
                    "between 10 and 500 characters are required.";

                return RedirectToAction(nameof(Index));
            }

            try
            {
                var response =
                    await _emailService.RetryDeadLetterNotificationAsync(
                        notificationId,
                        reason.Trim());

                if (response?.IsSuccess == true)
                {
                    TempData["success"] =
                        "Notification requeued successfully. " +
                        "The EmailAPI worker will process it.";
                }
                else
                {
                    TempData["error"] =
                        response?.Message ??
                        "Unable to requeue the notification.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Manual email retry failed. OutboxId={OutboxId}",
                    notificationId);

                TempData["error"] =
                    "Unable to requeue the notification.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
