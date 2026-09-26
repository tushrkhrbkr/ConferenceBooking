using ConferenceBooking.Services.AuthAPI.Models;

namespace ConferenceBooking.Services.AuthAPI.Service.IService
{
    public interface IJwtTokenGenerator
    {
        string GenerateToken(ApplicationUser applicationUser, IEnumerable<string>roles);
    }
}
