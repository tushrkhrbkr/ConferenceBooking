using ConferenceBooking.Services.BookingAPI.Service.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceBooking.Services.BookingAPI.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TestController : ControllerBase
    {
        private readonly ICurrentUserService _currentUser;

        public TestController(ICurrentUserService currentUser)
        {
            _currentUser = currentUser;
        }

        [HttpGet("identity")]
        public async Task<IActionResult> GetIdentity()
        {
            var profile = await _currentUser.GetProfileAsync();

            return Ok(new
            {
                UserId = _currentUser.UserId,
                DepartmentId = profile?.DepartmentId,
                DisplayName = profile?.DisplayName,
                IsAdmin = _currentUser.IsInRole("Admin")
            });
        }
    }
}