using ConferenceBooking.Services.BookingAPI.Data;
using ConferenceBooking.Services.BookingAPI.Models;
using ConferenceBooking.Services.BookingAPI.Models.Dto;
using ConferenceBooking.Services.BookingAPI.Service.IService;
using Microsoft.EntityFrameworkCore;

namespace ConferenceBooking.Services.BookingAPI.Service
{
    public class VCPlatformService : IVCPlatformService
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUser;

        public VCPlatformService(
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
            var query = _db.VCPlatforms.AsNoTracking();

            if (activeOnly)
                query = query.Where(x => x.IsActive);

            var platforms = await query
                .OrderBy(x => x.NetworkType)
                .ThenBy(x => x.PlatformName)
                .Select(x => new VCPlatformDto
                {
                    VCPlatformId = x.VCPlatformId,
                    PlatformCode = x.PlatformCode,
                    PlatformName = x.PlatformName,
                    NetworkType = x.NetworkType,
                    Description = x.Description,
                    IsActive = x.IsActive,
                    IsAvailableInHall = false
                })
                .ToListAsync(cancellationToken);

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "VC platforms retrieved successfully.",
                Result = platforms
            };
        }

        public async Task<ResponseDto> GetById(
            int id,
            CancellationToken cancellationToken = default)
        {
            var platform = await _db.VCPlatforms
                .AsNoTracking()
                .Where(x => x.VCPlatformId == id)
                .Select(x => new VCPlatformDto
                {
                    VCPlatformId = x.VCPlatformId,
                    PlatformCode = x.PlatformCode,
                    PlatformName = x.PlatformName,
                    NetworkType = x.NetworkType,
                    Description = x.Description,
                    IsActive = x.IsActive,
                    IsAvailableInHall = false
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (platform == null)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 404,
                    Message = "VC platform not found."
                };
            }

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "VC platform retrieved successfully.",
                Result = platform
            };
        }

        public async Task<ResponseDto> Create(
            CreateVCPlatformDto dto,
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

            var code = dto.PlatformCode.Trim();

            var exists = await _db.VCPlatforms
                .AnyAsync(
                    x => x.PlatformCode == code,
                    cancellationToken);

            if (exists)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 409,
                    Message = "A VC platform with this code already exists."
                };
            }

            var platform = new VCPlatform
            {
                PlatformCode = code,
                PlatformName = dto.PlatformName.Trim(),
                NetworkType = dto.NetworkType,
                Description = dto.Description?.Trim(),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId
            };

            _db.VCPlatforms.Add(platform);
            await _db.SaveChangesAsync(cancellationToken);

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 201,
                Message = "VC platform created successfully.",
                Result = ToDto(platform)
            };
        }

        public async Task<ResponseDto> Update(
            int id,
            UpdateVCPlatformDto dto,
            CancellationToken cancellationToken = default)
        {
            if (id != dto.VCPlatformId)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 400,
                    Message = "Route ID and request ID do not match."
                };
            }

            var platform = await _db.VCPlatforms
                .FirstOrDefaultAsync(
                    x => x.VCPlatformId == id,
                    cancellationToken);

            if (platform == null)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 404,
                    Message = "VC platform not found."
                };
            }

            var code = dto.PlatformCode.Trim();

            var duplicate = await _db.VCPlatforms
                .AnyAsync(
                    x => x.VCPlatformId != id &&
                         x.PlatformCode == code,
                    cancellationToken);

            if (duplicate)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 409,
                    Message = "Another VC platform already uses this code."
                };
            }

            platform.PlatformCode = code;
            platform.PlatformName = dto.PlatformName.Trim();
            platform.NetworkType = dto.NetworkType;
            platform.Description = dto.Description?.Trim();
            platform.IsActive = dto.IsActive;

            await _db.SaveChangesAsync(cancellationToken);

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "VC platform updated successfully.",
                Result = ToDto(platform)
            };
        }

        public async Task<ResponseDto> GetHallVCPlatforms(
            int hallId,
            bool activeOnly = true,
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
                from map in _db.HallVCPlatformMaps.AsNoTracking()
                join platform in _db.VCPlatforms.AsNoTracking()
                    on map.VCPlatformId equals platform.VCPlatformId
                where map.ConferenceHallId == hallId
                select new { map, platform };

            if (activeOnly)
            {
                query = query.Where(x =>
                    x.map.IsActive && x.platform.IsActive);
            }

            var platforms = await query
                .OrderBy(x => x.platform.NetworkType)
                .ThenBy(x => x.platform.PlatformName)
                .Select(x => new VCPlatformDto
                {
                    VCPlatformId = x.platform.VCPlatformId,
                    PlatformCode = x.platform.PlatformCode,
                    PlatformName = x.platform.PlatformName,
                    NetworkType = x.platform.NetworkType,
                    Description = x.platform.Description,
                    IsActive = x.platform.IsActive,
                    IsAvailableInHall = x.map.IsActive,
                    ConfigurationJson = x.map.ConfigurationJson
                })
                .ToListAsync(cancellationToken);

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Hall VC platforms retrieved successfully.",
                Result = platforms
            };
        }

        public async Task<ResponseDto> MapVCPlatformToHall(
            int hallId,
            HallVCPlatformSelectionDto dto,
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

            var platform = await _db.VCPlatforms
                .FirstOrDefaultAsync(
                    x => x.VCPlatformId == dto.VCPlatformId,
                    cancellationToken);

            if (platform == null)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 404,
                    Message = "VC platform not found."
                };
            }

            if (!platform.IsActive && dto.IsActive)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 400,
                    Message = "An inactive VC platform cannot be activated for a hall."
                };
            }

            var map = await _db.HallVCPlatformMaps
                .FirstOrDefaultAsync(
                    x => x.ConferenceHallId == hallId &&
                         x.VCPlatformId == dto.VCPlatformId,
                    cancellationToken);

            if (map == null)
            {
                map = new HallVCPlatformMap
                {
                    ConferenceHallId = hallId,
                    VCPlatformId = dto.VCPlatformId,
                    IsActive = dto.IsActive,
                    ConfigurationJson = dto.ConfigurationJson,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = userId
                };

                _db.HallVCPlatformMaps.Add(map);
            }
            else
            {
                map.IsActive = dto.IsActive;
                map.ConfigurationJson = dto.ConfigurationJson;
            }

            await _db.SaveChangesAsync(cancellationToken);

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Hall VC platform mapping saved successfully.",
                Result = new
                {
                    map.HallVCPlatformMapId,
                    map.ConferenceHallId,
                    map.VCPlatformId,
                    map.IsActive,
                    map.ConfigurationJson
                }
            };
        }

        public async Task<ResponseDto> UnmapVCPlatformFromHall(
            int hallId,
            int vcPlatformId,
            CancellationToken cancellationToken = default)
        {
            var map = await _db.HallVCPlatformMaps
                .FirstOrDefaultAsync(
                    x => x.ConferenceHallId == hallId &&
                         x.VCPlatformId == vcPlatformId,
                    cancellationToken);

            if (map == null)
            {
                return new ResponseDto
                {
                    IsSuccess = false,
                    StatusCode = 404,
                    Message = "VC platform mapping not found for this hall."
                };
            }

            // Retain the mapping for historical/configuration purposes.
            map.IsActive = false;

            await _db.SaveChangesAsync(cancellationToken);

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "VC platform has been deactivated for this hall."
            };
        }

        private static VCPlatformDto ToDto(VCPlatform platform)
        {
            return new VCPlatformDto
            {
                VCPlatformId = platform.VCPlatformId,
                PlatformCode = platform.PlatformCode,
                PlatformName = platform.PlatformName,
                NetworkType = platform.NetworkType,
                Description = platform.Description,
                IsActive = platform.IsActive,
                IsAvailableInHall = false
            };
        }
    }
}