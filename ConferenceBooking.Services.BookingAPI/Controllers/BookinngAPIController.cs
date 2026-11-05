
using ConferenceBooking.Services.BookingAPI.Models.Dto;
using ConferenceBooking.Services.BookingAPI.Service.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceBooking.Services.BookingAPI.Controllers
{
    [Route("api/conference-halls")]
    [ApiController]
    public class BookingAPIController : ControllerBase
    {
        private readonly IConferenceHallService _conferenceHallService;

        public BookingAPIController(
            IConferenceHallService conferenceHallService)
        {
            _conferenceHallService = conferenceHallService;
        }

        // GET: api/conference-halls
        [HttpGet]
        [Authorize(Policy = "AuthenticatedUser")]
        public async Task<IActionResult> GetAll(
            [FromQuery] bool activeOnly = true,
            CancellationToken cancellationToken = default)
        {
            ResponseDto response = await _conferenceHallService.GetAll(
                activeOnly,
                cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // GET: api/conference-halls/5
        [HttpGet("{id:int}")]
        [Authorize(Policy = "AuthenticatedUser")]
        public async Task<IActionResult> GetById(
            int id,
            CancellationToken cancellationToken = default)
        {
            ResponseDto response = await _conferenceHallService.GetById(
                id,
                cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // POST: api/conference-halls
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Create(
            [FromBody] CreateHallDto dto,
            CancellationToken cancellationToken = default)
        {
            ResponseDto response = await _conferenceHallService.Create(
                dto,
                cancellationToken);

            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/conference-halls/5
        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] UpdateHallDto dto,
            CancellationToken cancellationToken = default)
        {
            ResponseDto response = await _conferenceHallService.Update(
                id,
                dto,
                cancellationToken);

            return StatusCode(response.StatusCode, response);
        }
    }
}
