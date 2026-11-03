using System.Security.Claims;
using ConferenceBooking.Services.BookingAPI.Data;
using ConferenceBooking.Services.BookingAPI.Models;
using ConferenceBooking.Services.BookingAPI.Service.IService;
using Microsoft.EntityFrameworkCore;

namespace ConferenceBooking.Services.BookingAPI.Service
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _db;

        public CurrentUserService(
            IHttpContextAccessor httpContextAccessor,
            ApplicationDbContext db)
        {
            _httpContextAccessor = httpContextAccessor;
            _db = db;
        }

        private ClaimsPrincipal? Principal =>
            _httpContextAccessor.HttpContext?.User;

        public bool IsAuthenticated =>
            Principal?.Identity?.IsAuthenticated == true;

        public string UserId
        {
            get
            {
                if (!IsAuthenticated)
                    return string.Empty;

                return Principal?.FindFirst(
                           ClaimTypes.NameIdentifier)?.Value
                       ?? Principal?.FindFirst("sub")?.Value
                       ?? string.Empty;
            }
        }

        public bool IsInRole(string role)
        {
            return IsAuthenticated &&
                   Principal?.IsInRole(role) == true;
        }

        public async Task<UserProfile?> GetProfileAsync(
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated || string.IsNullOrWhiteSpace(UserId))
                return null;

            return await _db.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserId == UserId && x.IsActive,
                    cancellationToken);
        }

        public async Task<int?> GetDepartmentIdAsync(
            CancellationToken cancellationToken = default)
        {
            var profile = await GetProfileAsync(cancellationToken);

            return profile?.DepartmentId;
        }
    }
}