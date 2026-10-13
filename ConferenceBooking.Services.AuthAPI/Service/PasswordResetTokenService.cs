using System.Security.Cryptography;
using System.Text;
using ConferenceBooking.Services.AuthAPI.Service.IService;

namespace ConferenceBooking.Services.AuthAPI.Service
{
    public class PasswordResetTokenService : IPasswordResetTokenService
    {
        public string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);

            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }

        public string HashToken(string token)
        {
            using var sha256 = SHA256.Create();

            var bytes = Encoding.UTF8.GetBytes(token);

            var hash = sha256.ComputeHash(bytes);

            return Convert.ToHexString(hash);
        }
    }
}