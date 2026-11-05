
using ConferenceBooking.Services.BookingAPI.Data;
using ConferenceBooking.Services.BookingAPI.Models;
using ConferenceBooking.Services.BookingAPI.Models.Dto;
using ConferenceBooking.Services.BookingAPI.Service.IService;
using Microsoft.EntityFrameworkCore;

namespace ConferenceBooking.Services.BookingAPI.Service
{
    public class ConferenceHallService : IConferenceHallService
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUser;

        public ConferenceHallService(
            ApplicationDbContext db,
            ICurrentUserService currentUser)
        {
            _db = db;
            _currentUser = currentUser;
        }

        public async Task<ResponseDto> GetAll(
            bool activeOnly = true,
            CancellationToken cancellationToken = default)
        {
            var query = _db.ConferenceHalls.AsNoTracking();

            if (activeOnly)
            {
                query = query.Where(x => x.IsActive);
            }

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
                    IsActive = x.IsActive,
                    RowVersion = x.RowVersion
                })
                .ToListAsync(cancellationToken);

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Conference halls retrieved successfully.",
                Result = halls
            };
        }

        public async Task<ResponseDto> GetById(
            int id,
            CancellationToken cancellationToken = default)
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
                    IsActive = x.IsActive,
                    RowVersion = x.RowVersion
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (hall == null)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 404,
                    Message = "Conference hall not found."
                };
            }

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Conference hall retrieved successfully.",
                Result = hall
            };
        }

        public async Task<ResponseDto> Create(
            CreateHallDto dto,
            CancellationToken cancellationToken = default)
        {
            var userId = _currentUser.UserId;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 401,
                    Message = "Authenticated user information is not available."
                };
            }

            var code = dto.HallCode.Trim();

            var exists = await _db.ConferenceHalls
                .AnyAsync(x => x.HallCode == code, cancellationToken);

            if (exists)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 409,
                    Message = "A conference hall with this code already exists."
                };
            }

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
                CreatedByUserId = userId
            };

            _db.ConferenceHalls.Add(hall);

            await _db.SaveChangesAsync(cancellationToken);

            var result = new ConferenceHallDto
            {
                ConferenceHallId = hall.ConferenceHallId,
                HallCode = hall.HallCode,
                HallName = hall.HallName,
                LocationType = hall.LocationType,
                LocationName = hall.LocationName,
                Capacity = hall.Capacity,
                Description = hall.Description,
                IsActive = hall.IsActive,
                RowVersion = hall.RowVersion
            };

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 201,
                Message = "Conference hall created successfully.",
                Result = result
            };
        }

        public async Task<ResponseDto> Update(
            int id,
            UpdateHallDto dto,
            CancellationToken cancellationToken = default)
        {
            if (id != dto.ConferenceHallId)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 400,
                    Message = "Route ID and request ID do not match."
                };
            }

            if (dto.RowVersion == null || dto.RowVersion.Length == 0)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 400,
                    Message = "RowVersion is required. Reload the hall and submit its latest RowVersion."
                };
            }

            var userId = _currentUser.UserId;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 401,
                    Message = "Authenticated user information is not available."
                };
            }

            var hall = await _db.ConferenceHalls
                .FirstOrDefaultAsync(
                    x => x.ConferenceHallId == id,
                    cancellationToken);

            if (hall == null)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 404,
                    Message = "Conference hall not found."
                };
            }

            // Check whether the client is updating the latest version.
            if (!hall.RowVersion.SequenceEqual(dto.RowVersion))
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 409,
                    Message = "This hall was modified by another user. Reload the hall and try again."
                };
            }

            var code = dto.HallCode.Trim();

            var duplicate = await _db.ConferenceHalls
                .AnyAsync(
                    x => x.ConferenceHallId != id && x.HallCode == code,
                    cancellationToken);

            if (duplicate)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 409,
                    Message = "Another conference hall already uses this code."
                };
            }

            hall.HallCode = code;
            hall.HallName = dto.HallName.Trim();
            hall.LocationType = dto.LocationType;
            hall.LocationName = dto.LocationName.Trim();
            hall.Capacity = dto.Capacity;
            hall.Description = dto.Description?.Trim();
            hall.IsActive = dto.IsActive;
            hall.UpdatedAtUtc = DateTime.UtcNow;
            hall.UpdatedByUserId = userId;

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 409,
                    Message = "This hall was modified by another user. Reload the hall and try again."
                };
            }

            var result = new ConferenceHallDto
            {
                ConferenceHallId = hall.ConferenceHallId,
                HallCode = hall.HallCode,
                HallName = hall.HallName,
                LocationType = hall.LocationType,
                LocationName = hall.LocationName,
                Capacity = hall.Capacity,
                Description = hall.Description,
                IsActive = hall.IsActive,
                RowVersion = hall.RowVersion
            };

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Conference hall updated successfully.",
                Result = result
            };
        }
    }
}
