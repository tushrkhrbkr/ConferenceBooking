
using ConferenceBooking.Services.BookingAPI.Data;
using ConferenceBooking.Services.BookingAPI.Models;
using ConferenceBooking.Services.BookingAPI.Models.Dto;
using ConferenceBooking.Services.BookingAPI.Service.IService;
using Microsoft.EntityFrameworkCore;

namespace ConferenceBooking.Services.BookingAPI.Service
{
    public class FacilityService : IFacilityService
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUser;

        public FacilityService(
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
            var query = _db.Facilities.AsNoTracking();

            if (activeOnly)
                query = query.Where(x => x.IsActive);

            var facilities = await query
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.FacilityName)
                .Select(x => new FacilityDto
                {
                    FacilityId = x.FacilityId,
                    FacilityCode = x.FacilityCode,
                    FacilityName = x.FacilityName,
                    FacilityCategory = x.FacilityCategory,
                    RequiresParticipantSelection = x.RequiresParticipantSelection,
                    RequiresVCPlatform = x.RequiresVCPlatform,
                    IsActive = x.IsActive,
                    DisplayOrder = x.DisplayOrder,
                    IsAvailableInHall = false
                })
                .ToListAsync(cancellationToken);

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Facilities retrieved successfully.",
                Result = facilities
            };
        }

        public async Task<ResponseDto> GetById(
            int id,
            CancellationToken cancellationToken = default)
        {
            var facility = await _db.Facilities
                .AsNoTracking()
                .Where(x => x.FacilityId == id)
                .Select(x => new FacilityDto
                {
                    FacilityId = x.FacilityId,
                    FacilityCode = x.FacilityCode,
                    FacilityName = x.FacilityName,
                    FacilityCategory = x.FacilityCategory,
                    RequiresParticipantSelection = x.RequiresParticipantSelection,
                    RequiresVCPlatform = x.RequiresVCPlatform,
                    IsActive = x.IsActive,
                    DisplayOrder = x.DisplayOrder,
                    IsAvailableInHall = false
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (facility == null)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 404,
                    Message = "Facility not found."
                };
            }

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Facility retrieved successfully.",
                Result = facility
            };
        }

        public async Task<ResponseDto> Create(
            CreateFacilityDto dto,
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

            var code = dto.FacilityCode.Trim();

            var exists = await _db.Facilities
                .AnyAsync(x => x.FacilityCode == code, cancellationToken);

            if (exists)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 409,
                    Message = "A facility with this code already exists."
                };
            }

            var facility = new Facility
            {
                FacilityCode = code,
                FacilityName = dto.FacilityName.Trim(),
                FacilityCategory = dto.FacilityCategory,
                RequiresParticipantSelection = dto.RequiresParticipantSelection,
                RequiresVCPlatform = dto.RequiresVCPlatform,
                IsActive = true,
                DisplayOrder = dto.DisplayOrder,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId
            };

            _db.Facilities.Add(facility);
            await _db.SaveChangesAsync(cancellationToken);

            var result = ToFacilityDto(facility);

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 201,
                Message = "Facility created successfully.",
                Result = result
            };
        }

        public async Task<ResponseDto> Update(
            int id,
            UpdateFacilityDto dto,
            CancellationToken cancellationToken = default)
        {
            if (id != dto.FacilityId)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 400,
                    Message = "Route ID and request ID do not match."
                };
            }

            var facility = await _db.Facilities
                .FirstOrDefaultAsync(
                    x => x.FacilityId == id,
                    cancellationToken);

            if (facility == null)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 404,
                    Message = "Facility not found."
                };
            }

            var code = dto.FacilityCode.Trim();

            var duplicate = await _db.Facilities
                .AnyAsync(
                    x => x.FacilityId != id && x.FacilityCode == code,
                    cancellationToken);

            if (duplicate)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 409,
                    Message = "Another facility already uses this code."
                };
            }

            facility.FacilityCode = code;
            facility.FacilityName = dto.FacilityName.Trim();
            facility.FacilityCategory = dto.FacilityCategory;
            facility.RequiresParticipantSelection = dto.RequiresParticipantSelection;
            facility.RequiresVCPlatform = dto.RequiresVCPlatform;
            facility.DisplayOrder = dto.DisplayOrder;
            facility.IsActive = dto.IsActive;

            await _db.SaveChangesAsync(cancellationToken);

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Facility updated successfully.",
                Result = ToFacilityDto(facility)
            };
        }

        public async Task<ResponseDto> GetHallFacilities(
            int hallId,
            bool availableOnly = true,
            CancellationToken cancellationToken = default)
        {
            var hallExists = await _db.ConferenceHalls
                .AnyAsync(
                    x => x.ConferenceHallId == hallId,
                    cancellationToken);

            if (!hallExists)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 404,
                    Message = "Conference hall not found."
                };
            }

            var query =
                from map in _db.HallFacilityMaps.AsNoTracking()
                join facility in _db.Facilities.AsNoTracking()
                    on map.FacilityId equals facility.FacilityId
                where map.ConferenceHallId == hallId
                select new { map, facility };

            if (availableOnly)
            {
                query = query.Where(x =>
                    x.map.IsAvailable && x.facility.IsActive);
            }

            var facilities = await query
                .OrderBy(x => x.facility.DisplayOrder)
                .ThenBy(x => x.facility.FacilityName)
                .Select(x => new FacilityDto
                {
                    FacilityId = x.facility.FacilityId,
                    FacilityCode = x.facility.FacilityCode,
                    FacilityName = x.facility.FacilityName,
                    FacilityCategory = x.facility.FacilityCategory,
                    RequiresParticipantSelection =
                        x.facility.RequiresParticipantSelection,
                    RequiresVCPlatform = x.facility.RequiresVCPlatform,
                    IsActive = x.facility.IsActive,
                    DisplayOrder = x.facility.DisplayOrder,
                    IsAvailableInHall = x.map.IsAvailable,
                    ConfigurationJson = x.map.ConfigurationJson
                })
                .ToListAsync(cancellationToken);

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Hall facilities retrieved successfully.",
                Result = facilities
            };
        }

        public async Task<ResponseDto> MapFacilityToHall(
            int hallId,
            HallFacilitySelectionDto dto,
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

            var hallExists = await _db.ConferenceHalls
                .AnyAsync(
                    x => x.ConferenceHallId == hallId,
                    cancellationToken);

            if (!hallExists)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 404,
                    Message = "Conference hall not found."
                };
            }

            var facility = await _db.Facilities
                .FirstOrDefaultAsync(
                    x => x.FacilityId == dto.FacilityId,
                    cancellationToken);

            if (facility == null)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 404,
                    Message = "Facility not found."
                };
            }

            if (!facility.IsActive && dto.IsAvailable)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 400,
                    Message = "An inactive facility cannot be made available in a hall."
                };
            }

            var map = await _db.HallFacilityMaps
                .FirstOrDefaultAsync(
                    x => x.ConferenceHallId == hallId &&
                         x.FacilityId == dto.FacilityId,
                    cancellationToken);

            if (map == null)
            {
                map = new HallFacilityMap
                {
                    ConferenceHallId = hallId,
                    FacilityId = dto.FacilityId,
                    IsAvailable = dto.IsAvailable,
                    ConfigurationJson = dto.ConfigurationJson,
                    Notes = dto.Notes?.Trim(),
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = userId
                };

                _db.HallFacilityMaps.Add(map);
            }
            else
            {
                map.IsAvailable = dto.IsAvailable;
                map.ConfigurationJson = dto.ConfigurationJson;
                map.Notes = dto.Notes?.Trim();
            }

            await _db.SaveChangesAsync(cancellationToken);

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Facility mapping saved successfully.",
                Result = new
                {
                    map.HallFacilityMapId,
                    map.ConferenceHallId,
                    map.FacilityId,
                    map.IsAvailable,
                    map.ConfigurationJson,
                    map.Notes
                }
            };
        }

        public async Task<ResponseDto> UnmapFacilityFromHall(
            int hallId,
            int facilityId,
            CancellationToken cancellationToken = default)
        {
            var map = await _db.HallFacilityMaps
                .FirstOrDefaultAsync(
                    x => x.ConferenceHallId == hallId &&
                         x.FacilityId == facilityId,
                    cancellationToken);

            if (map == null)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 404,
                    Message = "Facility mapping not found for this hall."
                };
            }

            map.IsAvailable = false;

            await _db.SaveChangesAsync(cancellationToken);

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Facility has been made unavailable in this hall."
            };
        }

        private static FacilityDto ToFacilityDto(Facility facility)
        {
            return new FacilityDto
            {
                FacilityId = facility.FacilityId,
                FacilityCode = facility.FacilityCode,
                FacilityName = facility.FacilityName,
                FacilityCategory = facility.FacilityCategory,
                RequiresParticipantSelection =
                    facility.RequiresParticipantSelection,
                RequiresVCPlatform = facility.RequiresVCPlatform,
                IsActive = facility.IsActive,
                DisplayOrder = facility.DisplayOrder,
                IsAvailableInHall = false
            };
        }
    }
}
