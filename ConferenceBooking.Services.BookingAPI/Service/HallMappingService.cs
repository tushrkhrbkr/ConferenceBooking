using System.Data;
using Dapper;
using ConferenceBooking.Services.BookingAPI.Data;
using ConferenceBooking.Services.BookingAPI.Models;
using ConferenceBooking.Services.BookingAPI.Models.Dto;
using ConferenceBooking.Services.BookingAPI.Service.IService;
using Microsoft.EntityFrameworkCore;

namespace ConferenceBooking.Services.BookingAPI.Service
{
    public class HallMappingService : IHallMappingService
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUser;

        public HallMappingService(
            ApplicationDbContext db,
            ICurrentUserService currentUser)
        {
            _db = db;
            _currentUser = currentUser;
        }

        private ResponseDto Error(int status, string message) =>
            new()
            {
                IsSuccess = false,
                StatusCode = status,
                Message = message
            };

        private ResponseDto Success(string message, object? result = null, int status = 200) =>
            new()
            {
                IsSuccess = true,
                StatusCode = status,
                Message = message,
                Result = result
            };

        private string CurrentUserId => _currentUser.UserId;

        private async Task<bool> HallExists(int hallId, CancellationToken ct) =>
            await _db.ConferenceHalls.AnyAsync(
                x => x.ConferenceHallId == hallId && x.IsActive, ct);

        private async Task<bool> ActiveProfileExists(string userId, CancellationToken ct) =>
            await _db.UserProfiles.AnyAsync(
                x => x.UserId == userId && x.IsActive, ct);

        private async Task<bool> UserHasRole(
            string userId,
            string roleName,
            CancellationToken ct)
        {
            var connection = _db.Database.GetDbConnection();
            var openedHere = connection.State != ConnectionState.Open;

            if (openedHere)
                await connection.OpenAsync(ct);

            try
            {
                const string sql = @"
                    SELECT CAST(
                        CASE WHEN EXISTS
                        (
                            SELECT 1
                            FROM AspNetUserRoles ur
                            INNER JOIN AspNetRoles r
                                ON r.Id = ur.RoleId
                            WHERE ur.UserId = @UserId
                              AND UPPER(r.Name) = UPPER(@RoleName)
                        )
                        THEN 1 ELSE 0 END AS bit
                    );";

                return await connection.ExecuteScalarAsync<bool>(
                    new CommandDefinition(
                        sql,
                        new { UserId = userId, RoleName = roleName },
                        cancellationToken: ct));
            }
            finally
            {
                if (openedHere)
                    await connection.CloseAsync();
            }
        }

        private async Task<ResponseDto?> ValidateActor(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId))
                return Error(401, "Authenticated user information is unavailable.");

            if (!await ActiveProfileExists(CurrentUserId, ct))
                return Error(403, "Your active user profile could not be verified.");

            return null;
        }

        private static bool ValidPeriod(DateTime from, DateTime? to)
        {
            return to == null || to.Value > from;
        }

        // =========================================================
        // HALL MANAGER
        // =========================================================

        public async Task<ResponseDto> GetHallManagers(int hallId, CancellationToken ct = default)
        {
            if (!await HallExists(hallId, ct))
                return Error(404, "Active conference hall not found.");

            var now = DateTime.UtcNow;

            var result = await (
                from map in _db.HallManagerMaps.AsNoTracking()
                join profile in _db.UserProfiles.AsNoTracking()
                    on map.UserId equals profile.UserId
                where map.ConferenceHallId == hallId
                      && map.IsActive
                      && map.EffectiveFromUtc <= now
                      && (map.EffectiveToUtc == null || map.EffectiveToUtc > now)
                      && profile.IsActive
                orderby profile.DisplayName
                select new
                {
                    map.HallManagerMapId,
                    map.ConferenceHallId,
                    map.UserId,
                    profile.DisplayName,
                    profile.Designation,
                    profile.EmailAddress,
                    profile.MobileNumber,
                    map.EffectiveFromUtc,
                    map.EffectiveToUtc,
                    map.IsActive
                }).ToListAsync(ct);

            return Success("Hall Managers retrieved successfully.", result);
        }

        public async Task<ResponseDto> AssignHallManager(
            HallUserMappingDto dto, CancellationToken ct = default)
        {
            var actorError = await ValidateActor(ct);
            if (actorError != null) return actorError;

            if (string.IsNullOrWhiteSpace(dto.UserId) ||
                !ValidPeriod(dto.EffectiveFromUtc, dto.EffectiveToUtc))
                return Error(400, "Invalid user ID or effective date range.");

            if (!await HallExists(dto.ConferenceHallId, ct))
                return Error(404, "Active conference hall not found.");

            if (!await ActiveProfileExists(dto.UserId, ct))
                return Error(404, "Target user does not have an active profile.");

            if (!await UserHasRole(dto.UserId, "HALLMANAGER", ct))
                return Error(400, "Target user does not have the HALLMANAGER role.");

            var existing = await _db.HallManagerMaps.FirstOrDefaultAsync(
                x => x.ConferenceHallId == dto.ConferenceHallId
                     && x.UserId == dto.UserId
                     && x.IsActive, ct);

            if (existing != null)
            {
                existing.EffectiveFromUtc = dto.EffectiveFromUtc;
                existing.EffectiveToUtc = dto.EffectiveToUtc;
            }
            else
            {
                _db.HallManagerMaps.Add(new HallManagerMap
                {
                    ConferenceHallId = dto.ConferenceHallId,
                    UserId = dto.UserId,
                    EffectiveFromUtc = dto.EffectiveFromUtc,
                    EffectiveToUtc = dto.EffectiveToUtc,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = CurrentUserId
                });
            }

            await _db.SaveChangesAsync(ct);
            return Success("Hall Manager mapping saved successfully.");
        }

        public async Task<ResponseDto> RemoveHallManager(
            int hallId, string userId, CancellationToken ct = default)
        {
            var map = await _db.HallManagerMaps.FirstOrDefaultAsync(
                x => x.ConferenceHallId == hallId
                     && x.UserId == userId
                     && x.IsActive, ct);

            if (map == null)
                return Error(404, "Active Hall Manager mapping not found.");

            map.IsActive = false;
            map.EffectiveToUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return Success("Hall Manager mapping deactivated.");
        }

        // =========================================================
        // SUB ADMIN
        // =========================================================

        public async Task<ResponseDto> GetHallSubAdmins(int hallId, CancellationToken ct = default)
        {
            if (!await HallExists(hallId, ct))
                return Error(404, "Active conference hall not found.");

            var now = DateTime.UtcNow;

            var result = await (
                from map in _db.HallSubAdminMaps.AsNoTracking()
                join profile in _db.UserProfiles.AsNoTracking()
                    on map.UserId equals profile.UserId
                where map.ConferenceHallId == hallId
                      && map.IsActive
                      && map.EffectiveFromUtc <= now
                      && (map.EffectiveToUtc == null || map.EffectiveToUtc > now)
                      && profile.IsActive
                orderby profile.DisplayName
                select new
                {
                    map.HallSubAdminMapId,
                    map.ConferenceHallId,
                    map.UserId,
                    profile.DisplayName,
                    profile.Designation,
                    profile.EmailAddress,
                    profile.MobileNumber,
                    map.EffectiveFromUtc,
                    map.EffectiveToUtc,
                    map.IsActive
                }).ToListAsync(ct);

            return Success("Sub Admins retrieved successfully.", result);
        }

        public async Task<ResponseDto> AssignHallSubAdmin(
            HallUserMappingDto dto, CancellationToken ct = default)
        {
            var actorError = await ValidateActor(ct);
            if (actorError != null) return actorError;

            if (string.IsNullOrWhiteSpace(dto.UserId) ||
                !ValidPeriod(dto.EffectiveFromUtc, dto.EffectiveToUtc))
                return Error(400, "Invalid user ID or effective date range.");

            if (!await HallExists(dto.ConferenceHallId, ct))
                return Error(404, "Active conference hall not found.");

            if (!await ActiveProfileExists(dto.UserId, ct))
                return Error(404, "Target user does not have an active profile.");

            if (!await UserHasRole(dto.UserId, "SUBADMIN", ct))
                return Error(400, "Target user does not have the SUBADMIN role.");

            var existing = await _db.HallSubAdminMaps.FirstOrDefaultAsync(
                x => x.ConferenceHallId == dto.ConferenceHallId
                     && x.UserId == dto.UserId
                     && x.IsActive, ct);

            if (existing != null)
            {
                existing.EffectiveFromUtc = dto.EffectiveFromUtc;
                existing.EffectiveToUtc = dto.EffectiveToUtc;
            }
            else
            {
                _db.HallSubAdminMaps.Add(new HallSubAdminMap
                {
                    ConferenceHallId = dto.ConferenceHallId,
                    UserId = dto.UserId,
                    EffectiveFromUtc = dto.EffectiveFromUtc,
                    EffectiveToUtc = dto.EffectiveToUtc,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = CurrentUserId
                });
            }

            await _db.SaveChangesAsync(ct);
            return Success("Sub Admin mapping saved successfully.");
        }

        public async Task<ResponseDto> RemoveHallSubAdmin(
            int hallId, string userId, CancellationToken ct = default)
        {
            var map = await _db.HallSubAdminMaps.FirstOrDefaultAsync(
                x => x.ConferenceHallId == hallId
                     && x.UserId == userId
                     && x.IsActive, ct);

            if (map == null)
                return Error(404, "Active Sub Admin mapping not found.");

            map.IsActive = false;
            map.EffectiveToUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return Success("Sub Admin mapping deactivated.");
        }

        // =========================================================
        // HALL - DEPARTMENT
        // =========================================================

        public async Task<ResponseDto> GetHallDepartments(int hallId, CancellationToken ct = default)
        {
            if (!await HallExists(hallId, ct))
                return Error(404, "Active conference hall not found.");

            var result = await (
                from map in _db.HallDepartmentMaps.AsNoTracking()
                join department in _db.Departments.AsNoTracking()
                    on map.DepartmentId equals department.DepartmentId
                where map.ConferenceHallId == hallId
                      && map.IsActive
                      && department.IsActive
                orderby department.DepartmentName
                select new
                {
                    map.HallDepartmentMapId,
                    map.ConferenceHallId,
                    map.DepartmentId,
                    department.DepartmentCode,
                    department.DepartmentName,
                    map.IsActive
                }).ToListAsync(ct);

            return Success("Hall departments retrieved successfully.", result);
        }

        public async Task<ResponseDto> MapDepartmentToHall(
            HallDepartmentMappingDto dto, CancellationToken ct = default)
        {
            var actorError = await ValidateActor(ct);
            if (actorError != null) return actorError;

            if (!await HallExists(dto.ConferenceHallId, ct))
                return Error(404, "Active conference hall not found.");

            var department = await _db.Departments.FirstOrDefaultAsync(
                x => x.DepartmentId == dto.DepartmentId && x.IsActive, ct);

            if (department == null)
                return Error(404, "Active department not found.");

            var map = await _db.HallDepartmentMaps.FirstOrDefaultAsync(
                x => x.ConferenceHallId == dto.ConferenceHallId
                     && x.DepartmentId == dto.DepartmentId, ct);

            if (map == null)
            {
                map = new HallDepartmentMap
                {
                    ConferenceHallId = dto.ConferenceHallId,
                    DepartmentId = dto.DepartmentId,
                    IsActive = dto.IsActive,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = CurrentUserId
                };

                _db.HallDepartmentMaps.Add(map);
            }
            else
            {
                map.IsActive = dto.IsActive;
            }

            await _db.SaveChangesAsync(ct);
            return Success("Hall-department mapping saved successfully.");
        }

        public async Task<ResponseDto> RemoveDepartmentFromHall(
            int hallId, int departmentId, CancellationToken ct = default)
        {
            var map = await _db.HallDepartmentMaps.FirstOrDefaultAsync(
                x => x.ConferenceHallId == hallId
                     && x.DepartmentId == departmentId
                     && x.IsActive, ct);

            if (map == null)
                return Error(404, "Active hall-department mapping not found.");

            map.IsActive = false;
            await _db.SaveChangesAsync(ct);

            return Success("Department mapping deactivated.");
        }

        // =========================================================
        // REMOTE USER - HALL
        // =========================================================

        public async Task<ResponseDto> GetRemoteUserHalls(
            string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return Error(400, "User ID is required.");

            var result = await (
                from map in _db.RemoteUserHallMaps.AsNoTracking()
                join hall in _db.ConferenceHalls.AsNoTracking()
                    on map.ConferenceHallId equals hall.ConferenceHallId
                where map.UserId == userId
                      && map.IsActive
                      && hall.IsActive
                orderby map.IsPrimary descending, hall.HallName
                select new
                {
                    map.RemoteUserHallMapId,
                    map.UserId,
                    map.ConferenceHallId,
                    hall.HallCode,
                    hall.HallName,
                    hall.LocationName,
                    map.EffectiveFromUtc,
                    map.EffectiveToUtc,
                    map.IsPrimary,
                    map.IsActive
                }).ToListAsync(ct);

            return Success("Remote user hall mappings retrieved successfully.", result);
        }

        public async Task<ResponseDto> MapRemoteUserToHall(
            RemoteUserHallMappingDto dto, CancellationToken ct = default)
        {
            var actorError = await ValidateActor(ct);
            if (actorError != null) return actorError;

            if (string.IsNullOrWhiteSpace(dto.UserId) ||
                !ValidPeriod(dto.EffectiveFromUtc, dto.EffectiveToUtc))
                return Error(400, "Invalid user ID or effective date range.");

            if (!await HallExists(dto.ConferenceHallId, ct))
                return Error(404, "Active conference hall not found.");

            if (!await ActiveProfileExists(dto.UserId, ct))
                return Error(404, "Target user does not have an active profile.");

            if (!await UserHasRole(dto.UserId, "REMOTEUSER", ct))
                return Error(400, "Target user does not have the REMOTEUSER role.");

            await using var transaction =
                await _db.Database.BeginTransactionAsync(ct);

            if (dto.IsPrimary)
            {
                var currentPrimary = await _db.RemoteUserHallMaps
                    .Where(x => x.UserId == dto.UserId && x.IsActive && x.IsPrimary)
                    .ToListAsync(ct);

                foreach (var item in currentPrimary)
                    item.IsPrimary = false;
            }

            var map = await _db.RemoteUserHallMaps.FirstOrDefaultAsync(
                x => x.UserId == dto.UserId
                     && x.ConferenceHallId == dto.ConferenceHallId
                     && x.IsActive, ct);

            if (map == null)
            {
                map = new RemoteUserHallMap
                {
                    UserId = dto.UserId,
                    ConferenceHallId = dto.ConferenceHallId,
                    EffectiveFromUtc = dto.EffectiveFromUtc,
                    EffectiveToUtc = dto.EffectiveToUtc,
                    IsPrimary = dto.IsPrimary,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = CurrentUserId
                };

                _db.RemoteUserHallMaps.Add(map);
            }
            else
            {
                map.EffectiveFromUtc = dto.EffectiveFromUtc;
                map.EffectiveToUtc = dto.EffectiveToUtc;
                map.IsPrimary = dto.IsPrimary;
            }

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return Success("Remote user hall mapping saved successfully.");
        }

        public async Task<ResponseDto> RemoveRemoteUserFromHall(
            int hallId, string userId, CancellationToken ct = default)
        {
            var map = await _db.RemoteUserHallMaps.FirstOrDefaultAsync(
                x => x.ConferenceHallId == hallId
                     && x.UserId == userId
                     && x.IsActive, ct);

            if (map == null)
                return Error(404, "Active remote user hall mapping not found.");

            map.IsActive = false;
            map.IsPrimary = false;
            map.EffectiveToUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return Success("Remote user hall mapping deactivated.");
        }
    }
}