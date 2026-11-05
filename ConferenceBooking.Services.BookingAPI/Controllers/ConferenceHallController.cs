
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
    public class ConferenceHallController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUser;
        private readonly IConferenceHallService _conferenceHallService;
        private ResponseDto _response;

        public ConferenceHallController(
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
            if (id != dto.ConferenceHallId)
                return BadRequest(new ApiResultDto<object>
                {
                    Success = false,
                    Message = "Route ID and request ID do not match."
                });

            if (string.IsNullOrWhiteSpace(_currentUser.UserId))
                return Unauthorized();

            var hall = await _db.ConferenceHalls
                .FirstOrDefaultAsync(
                    x => x.ConferenceHallId == id,
                    cancellationToken);

            if (hall == null)
                return NotFound(new ApiResultDto<object>
                {
                    Success = false,
                    Message = "Conference hall not found."
                });

            var code = dto.HallCode.Trim();

            var duplicate = await _db.ConferenceHalls
                .AnyAsync(
                    x => x.ConferenceHallId != id && x.HallCode == code,
                    cancellationToken);

            if (duplicate)
                return Conflict(new ApiResultDto<object>
                {
                    Success = false,
                    Message = "Another conference hall already uses this code."
                });

            // Optional optimistic concurrency check.
            if (dto.RowVersion is { Length: > 0 } &&
                !hall.RowVersion.SequenceEqual(dto.RowVersion))
            {
                return Conflict(new ApiResultDto<object>
                {
                    Success = false,
                    Message = "This hall was modified by another user. Reload and try again."
                });
            }

            hall.HallCode = code;
            hall.HallName = dto.HallName.Trim();
            hall.LocationType = dto.LocationType;
            hall.LocationName = dto.LocationName.Trim();
            hall.Capacity = dto.Capacity;
            hall.Description = dto.Description?.Trim();
            hall.IsActive = dto.IsActive;
            hall.UpdatedAtUtc = DateTime.UtcNow;
            hall.UpdatedByUserId = _currentUser.UserId;

            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new ApiResultDto<ConferenceHallDto>
            {
                Success = true,
                Message = "Conference hall updated successfully.",
                Data = new ConferenceHallDto
                {
                    ConferenceHallId = hall.ConferenceHallId,
                    HallCode = hall.HallCode,
                    HallName = hall.HallName,
                    LocationType = hall.LocationType,
                    LocationName = hall.LocationName,
                    Capacity = hall.Capacity,
                    Description = hall.Description,
                    IsActive = hall.IsActive
                }
            });
        }
    }
}
