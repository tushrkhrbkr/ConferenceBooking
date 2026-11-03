using ConferenceBooking.Services.BookingAPI.Models;

namespace ConferenceBooking.Services.BookingAPI.Service.IService
{
    public interface ICurrentUserService
    {
        string UserId { get; }
        bool IsAuthenticated { get; }

        bool IsInRole(string role);

        Task<UserProfile?> GetProfileAsync(CancellationToken cancellationToken = default);

        Task<int?> GetDepartmentIdAsync(CancellationToken cancellationToken = default);
    }
}