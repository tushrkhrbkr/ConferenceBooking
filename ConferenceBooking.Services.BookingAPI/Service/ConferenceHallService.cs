using AutoMapper;
using ConferenceBooking.Services.BookingAPI.Data;
using ConferenceBooking.Services.BookingAPI.Models;
using ConferenceBooking.Services.BookingAPI.Models.Dto;
using ConferenceBooking.Services.BookingAPI.Service.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConferenceBooking.Services.BookingAPI.Service
{
    public class ConferenceHallService: IConferenceHallService
    {
        private readonly ApplicationDbContext _db;
        private IMapper _mapper;
        private readonly ICurrentUserService _currentUser;
        private readonly IConferenceHallService _conferenceHallService;
        private readonly ResponseDto _response;

        public ConferenceHallService(
            ApplicationDbContext db, IMapper mapper,
            ICurrentUserService currentUser,
            IConferenceHallService conferenceHallService)
        {
            _db = db;
            _mapper = mapper;
            _currentUser = currentUser;
            _conferenceHallService = conferenceHallService;
            _response = new ResponseDto();
        }

        public async Task<ResponseDto> GetAll(bool activeOnly = true,
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
                    IsActive = x.IsActive,
                    RowVersion = x.RowVersion
                })
                .ToListAsync(cancellationToken);

            _response.IsSuccess = true;
            _response.Result = halls;

            return _response;
        }

        public async Task<ResponseDto> GetById(
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
                    IsActive = x.IsActive,
                    RowVersion = x.RowVersion
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (hall == null)
            {
                _response.IsSuccess = false;
                _response.Message = "Conference hall not found.";
            }

            _response.IsSuccess = true;
            _response.Message = "Conference hall retrieved successfully.";
            _response.Result = hall;
            
            return _response;
        }

        public async Task<ResponseDto> Create(
            CreateHallDto dto,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_currentUser.UserId))
            {
                _response.IsSuccess = false;
                _response.Message = "Unauthorized";
                return _response;
            }

            var code = dto.HallCode.Trim();

            var exists = await _db.ConferenceHalls
                .AnyAsync(x => x.HallCode == code, cancellationToken);

            if (exists)
            {
                _response.IsSuccess = false;
                _response.Message = "A conference hall with this code already exists.";
                return _response;
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
                CreatedByUserId = _currentUser.UserId
            };

            _db.ConferenceHalls.Add(hall);
            await _db.SaveChangesAsync(cancellationToken);

            _response.IsSuccess = true;
            _response.Message = "Conference hall created successfully.";
            _response.Result = hall;

            return _response;

             
        }

        public async Task<ResponseDto> Update(
            int id, UpdateHallDto dto,
            CancellationToken cancellationToken)
        {
            if (id != dto.ConferenceHallId)
            {
                _response.IsSuccess = false;
                _response.Message = "Route ID and request ID do not match.";
                return _response;
                }

            if (string.IsNullOrWhiteSpace(_currentUser.UserId))
                {
                    _response.IsSuccess = false;
                    _response.Message = "Unauthorized";
                    return _response;
            }

            var hall = await _db.ConferenceHalls
                .FirstOrDefaultAsync(
                    x => x.ConferenceHallId == id,
                    cancellationToken);

            if (hall == null)
                {
                _response.IsSuccess = false;
                _response.Message = "Conference hall not found.";
                return _response;
                }

            var code = dto.HallCode.Trim();

            var duplicate = await _db.ConferenceHalls
                .AnyAsync(
                    x => x.ConferenceHallId != id && x.HallCode == code,
                    cancellationToken);

            if (duplicate)
                
                {
                _response.IsSuccess = false;
                    _response.Message = "Another conference hall already uses this code.";
                return _response;
                }

            // Optional optimistic concurrency check.
            if (dto.RowVersion is { Length: > 0 } &&
                !hall.RowVersion.SequenceEqual(dto.RowVersion))
            {
                
                    _response.IsSuccess = false;
                    _response.Message = "This hall was modified by another user. Reload and try again.";
                    return _response;
                
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

                _response.IsSuccess = true,
                _response.Message = "Conference hall updated successfully.",
                _response.Result = hall;
            return _response;
            
        }
    }
}
