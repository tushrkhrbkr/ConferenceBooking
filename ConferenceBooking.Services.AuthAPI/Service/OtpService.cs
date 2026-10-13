using System.Security.Cryptography;
using System.Text;
using ConferenceBooking.Services.AuthAPI.Service.IService;

namespace ConferenceBooking.Services.AuthAPI.Service
{
    public class OtpService : IOtpService
    {
        public string GenerateOtp()
        {
            var value = RandomNumberGenerator.GetInt32(0, 1000000);

            return value.ToString("D6");
        }

        public string HashOtp(string otp)
        {
            using var sha256 = SHA256.Create();

            var bytes = Encoding.UTF8.GetBytes(otp);

            var hash = sha256.ComputeHash(bytes);

            return Convert.ToHexString(hash);
        }
    }
}