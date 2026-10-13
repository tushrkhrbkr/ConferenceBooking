using ConferenceBooking.Web.Models;
using ConferenceBooking.Web.Models.AuthLogin;
using ConferenceBooking.Web.Service.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace ConferenceBooking.Web.Controllers
{
    [Authorize(Roles = "Admin,ADMIN")]
    public class AdminRegistrationController : Controller
    {
        private readonly IAuthService _authService;

        public AdminRegistrationController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var response = await _authService.GetPendingRegistrations();

            if (response == null || !response.IsSuccess)
            {
                TempData["error"] =
                    response?.Message ?? "Unable to load pending registrations.";

                return View(new List<PendingRegistrationDto>());
            }

            var registrations =
                JsonConvert.DeserializeObject<List<PendingRegistrationDto>>(
                    Convert.ToString(response.Result) ?? "[]")
                ?? new List<PendingRegistrationDto>();

            return View(registrations);
        }

        [HttpGet]
        public async Task<IActionResult> Review(long id)
        {
            var pendingResponse =
                await _authService.GetPendingRegistrations();

            if (pendingResponse == null || !pendingResponse.IsSuccess)
            {
                TempData["error"] = "Unable to load registration details.";
                return RedirectToAction(nameof(Index));
            }

            var registrations =
                JsonConvert.DeserializeObject<List<PendingRegistrationDto>>(
                    Convert.ToString(pendingResponse.Result) ?? "[]")
                ?? new List<PendingRegistrationDto>();

            var registration = registrations
                .FirstOrDefault(x => x.RegistrationRequestId == id);

            if (registration == null)
            {
                TempData["error"] =
                    "The request was not found or is no longer pending.";

                return RedirectToAction(nameof(Index));
            }

            var lookupResponse =
                await _authService.GetRegistrationLookups();

            if (lookupResponse == null || !lookupResponse.IsSuccess)
            {
                TempData["error"] = "Unable to load departments and roles.";
                return RedirectToAction(nameof(Index));
            }

            var lookups =
                JsonConvert.DeserializeObject<RegistrationLookupsDto>(
                    Convert.ToString(lookupResponse.Result) ?? "{}");

            if (lookups == null)
            {
                TempData["error"] = "Invalid lookup response.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Departments = lookups.Departments;
            ViewBag.Roles = lookups.Roles;

            return View(registration);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(ApproveRegistrationDto request)
        {
            if (!ModelState.IsValid)
            {
                TempData["error"] = "Please provide valid approval details.";
                return RedirectToAction(
                    nameof(Review),
                    new { id = request.RegistrationRequestId });
            }

            var response = await _authService.ApproveRegistration(request);

            if (response != null && response.IsSuccess)
            {
                TempData["success"] = "Registration approved successfully.";
            }
            else
            {
                TempData["error"] =
                    response?.Message ?? "Unable to approve registration.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(RejectRegistrationDto request)
        {
            if (!ModelState.IsValid ||
                string.IsNullOrWhiteSpace(request.Remarks))
            {
                TempData["error"] =
                    "Please provide remarks explaining the rejection.";

                return RedirectToAction(nameof(Index));
            }

            var response = await _authService.RejectRegistration(request);

            if (response != null && response.IsSuccess)
            {
                TempData["success"] = "Registration rejected.";
            }
            else
            {
                TempData["error"] =
                    response?.Message ?? "Unable to reject registration.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}