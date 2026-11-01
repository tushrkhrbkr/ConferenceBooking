using ConferenceBooking.Web.Service.IService;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;

namespace ConferenceBooking.Web.Service
{
    public class CaptchaService : ICaptchaService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        private const string KeyPrefix = "CONVENE_CAPTCHA_";
        private const string Characters =
            "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        private const int ExpiryMinutes = 3;
        private const int MaxChallenges = 5;

        public CaptchaService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public byte[] GenerateCaptchaImage(string challengeId)
        {
            if (!Guid.TryParse(challengeId, out _))
                throw new ArgumentException("Invalid CAPTCHA challenge ID.");

            var context = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException(
                    "HTTP context unavailable.");

            var answer = GenerateAnswer(6);
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = ComputeHash(answer, salt);

            var session = context.Session;

            // Remove expired challenges and enforce the session limit.
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var keys = session.Keys
                .Where(k => k.StartsWith(KeyPrefix))
                .ToList();

            foreach (var key in keys)
            {
                var value = session.GetString(key);

                if (string.IsNullOrWhiteSpace(value))
                {
                    session.Remove(key);
                    continue;
                }

                var parts = value.Split('|');

                if (parts.Length != 3 ||
                    !long.TryParse(parts[2], out var expiry) ||
                    expiry < now)
                {
                    session.Remove(key);
                }
            }

            keys = session.Keys
                .Where(k => k.StartsWith(KeyPrefix))
                .ToList();

            while (keys.Count >= MaxChallenges)
            {
                session.Remove(keys[0]);
                keys.RemoveAt(0);
            }

            var expiryTime = DateTimeOffset.UtcNow
                .AddMinutes(ExpiryMinutes)
                .ToUnixTimeSeconds();

            var storedValue =
                Convert.ToBase64String(salt) + "|" +
                hash + "|" +
                expiryTime;

            session.SetString(
                KeyPrefix + challengeId,
                storedValue);

            return Encoding.UTF8.GetBytes(BuildSvg(answer));
        }

        public bool ValidateCaptcha(
            string challengeId,
            string? answer)
        {
            var context = _httpContextAccessor.HttpContext;

            if (context == null ||
                !Guid.TryParse(challengeId, out _))
                return false;

            var key = KeyPrefix + challengeId;
            var storedValue = context.Session.GetString(key);

            // A challenge can be attempted only once.
            context.Session.Remove(key);

            if (string.IsNullOrWhiteSpace(storedValue) ||
                string.IsNullOrWhiteSpace(answer))
                return false;

            var parts = storedValue.Split('|');

            if (parts.Length != 3 ||
                !long.TryParse(parts[2], out var expiry))
                return false;

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry)
                return false;

            try
            {
                var salt = Convert.FromBase64String(parts[0]);
                var expectedHash = Convert.FromBase64String(parts[1]);

                var suppliedHash = Convert.FromBase64String(
                    ComputeHash(
                        answer.Trim().ToUpperInvariant(),
                        salt));

                return CryptographicOperations.FixedTimeEquals(
                    expectedHash,
                    suppliedHash);
            }
            catch
            {
                return false;
            }
        }

        private static string GenerateAnswer(int length)
        {
            var result = new StringBuilder(length);

            for (int i = 0; i < length; i++)
            {
                int index =
                    RandomNumberGenerator.GetInt32(Characters.Length);

                result.Append(Characters[index]);
            }

            return result.ToString();
        }

        private static string ComputeHash(string answer, byte[] salt)
        {
            var input = salt
                .Concat(Encoding.UTF8.GetBytes(answer))
                .ToArray();

            return Convert.ToBase64String(
                SHA256.HashData(input));
        }

        private static string BuildSvg(string answer)
        {
            Func<int, int, int> random =
                RandomNumberGenerator.GetInt32;

            var svg = new StringBuilder();

            svg.Append(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" " +
                "width=\"220\" height=\"65\" viewBox=\"0 0 220 65\">" +
                "<rect width=\"220\" height=\"65\" rx=\"6\" fill=\"#f1f3f5\"/>"
            );

            for (int i = 0; i < 7; i++)
            {
                int x1 = random(0, 220);
                int y1 = random(0, 65);
                int x2 = random(0, 220);
                int y2 = random(0, 65);

                svg.Append(
                    $"<line x1='{x1}' y1='{y1}' x2='{x2}' y2='{y2}' " +
                    "stroke='#adb5bd' stroke-width='1'/>");
            }

            for (int i = 0; i < answer.Length; i++)
            {
                int x = 22 + (i * 32);
                int y = random(38, 51);
                int rotation = random(-15, 16);

                svg.Append(
                    $"<text x='{x}' y='{y}' " +
                    $"transform='rotate({rotation} {x} {y})' " +
                    "font-family='Arial,sans-serif' font-size='27' " +
                    "font-weight='bold' fill='#212529'>" +
                    answer[i] +
                    "</text>");
            }

            svg.Append("</svg>");
            return svg.ToString();
        }
    }
}