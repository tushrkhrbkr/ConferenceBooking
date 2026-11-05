
using ConferenceBooking.Services.BookingAPI.Data;
using ConferenceBooking.Services.BookingAPI.Models;
using ConferenceBooking.Services.BookingAPI.Models.Dto;
using ConferenceBooking.Services.BookingAPI.Service.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConferenceBooking.Services.BookingAPI.Controllers
{
    [Route("api/conference-halls")]
    [ApiController]
    public class BookinngAPIController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUser;
        private readonly IConferenceHallService _conferenceHallService;
        private ResponseDto _response;

        public BookinngAPIController(
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            IConferenceHallService conferenceHallService)
        {
            _db = db;
            _currentUser = currentUser;
            _response = new ResponseDto();
            _conferenceHallService = conferenceHallService;
        }

        // GET: api/conference-halls
        [HttpGet]
        [Authorize(Policy = "AuthenticatedUser")]
        public async Task<IActionResult> GetAll(
            [FromQuery] bool activeOnly = true,
            CancellationToken cancellationToken = default)
        {
            _response = await _conferenceHallService.GetAll(activeOnly, cancellationToken);
            return Ok(_response);
        }

        // GET: api/conference-halls/5
        [HttpGet("{id:int}")]
        [Authorize(Policy = "AuthenticatedUser")]
        public async Task<IActionResult> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            _response = await _conferenceHallService.GetById(id, cancellationToken);

            return Ok(_response);
            
        }

        // POST: api/conference-halls
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Create(
            [FromBody] CreateHallDto dto,
            CancellationToken cancellationToken)
        {
            _response = await _conferenceHallService.Create(dto, cancellationToken);
            return Ok(_response);
        }

        // PUT: api/conference-halls/5
        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] UpdateHallDto dto,
            CancellationToken cancellationToken)
        {
            _response = await _conferenceHallService.Update(id, dto, cancellationToken);
            return Ok(_response);
        }
    }
}
