
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

        public ConferenceHallController(
            ApplicationDbContext db,
            ICurrentUserService currentUser)
        {
            _db = db;
            _currentUser = currentUser;
        }

        // GET: api/conference-halls
        [HttpGet]
        [Authorize(Policy = "AuthenticatedUser")]
        public async Task<IActionResult> GetAll(
            [FromQuery] bool activeOnly = true,
            CancellationToken cancellationToken = default)
        {
            var query = _db.ConferenceHalls.AsNoTracking();

            if (activeOnly)
                query = query.Where(x => x.IsActive);

            var halls = await query
                .OrderBy(x => x.LocationType)
                .ThenBy(x => x.HallName)
                .Select(x => new ConferenceHallDto
                {
                    ConferenceHallId = x.ConferenceHallId,
                    HallCode = x.HallCode,
                    HallName = x.HallName,
                    LocationType = x.LocationType,
                    LocationName = x.LocationName,
                    Capacity = x.Capacity,
                    Description = x.Description,
                    IsActive = x.IsActive
                })
                .ToListAsync(cancellationToken);

            return Ok(new ApiResultDto<List<ConferenceHallDto>>
            {
                Success = true,
                Message = "Conference halls retrieved successfully.",
                Data = halls
            });
        }

        // GET: api/conference-halls/5
        [HttpGet("{id:int}")]
        [Authorize(Policy = "AuthenticatedUser")]
        public async Task<IActionResult> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            var hall = await _db.ConferenceHalls
                .AsNoTracking()
                .Where(x => x.ConferenceHallId == id)
                .Select(x => new ConferenceHallDto
                {
                    ConferenceHallId = x.ConferenceHallId,
                    HallCode = x.HallCode,
                    HallName = x.HallName,
                    LocationType = x.LocationType,
                    LocationName = x.LocationName,
                    Capacity = x.Capacity,
                    Description = x.Description,
                    IsActive = x.IsActive
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (hall == null)
                return NotFound(new ApiResultDto<object>
                {
                    Success = false,
                    Message = "Conference hall not found."
                });

            return Ok(new ApiResultDto<ConferenceHallDto>
            {
                Success = true,
                Message = "Conference hall retrieved successfully.",
                Data = hall
            });
        }

        // POST: api/conference-halls
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Create(
            [FromBody] CreateHallDto dto,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_currentUser.UserId))
                return Unauthorized();

            var code = dto.HallCode.Trim();

            var exists = await _db.ConferenceHalls
                .AnyAsync(x => x.HallCode == code, cancellationToken);

            if (exists)
                return Conflict(new ApiResultDto<object>
                {
                    Success = false,
                    Message = "A conference hall with this code already exists."
                });

            var hall = new ConferenceHall
            {
                HallCode = code,
                HallName = dto.HallName.Trim(),
                LocationType = dto.LocationType,
                LocationName = dto.LocationName.Trim(),
                Capacity = dto.Capacity,
                Description = dto.Description?.Trim(),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = _currentUser.UserId
            };

            _db.ConferenceHalls.Add(hall);
            await _db.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = hall.ConferenceHallId },
                new ApiResultDto<ConferenceHallDto>
                {
                    Success = true,
                    Message = "Conference hall created successfully.",
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
