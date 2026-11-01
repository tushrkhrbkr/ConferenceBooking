using ConferenceBooking.Web.Service.IService;
using System.Security.Cryptography;
using System.Text;

namespace ConferenceBooking.Web.Service
{
    public class CaptchaService : ICaptchaService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        private const string CaptchaSessionKey = "CONVENE_CAPTCHA";
        private const string CaptchaExpiryKey = "CONVENE_CAPTCHA_EXPIRY";

        private const string Characters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public CaptchaService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public byte[] GenerateCaptchaImage()
        {
            var context = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("HTTP context unavailable.");

            var answer = GenerateAnswer(6);
            var salt = RandomNumberGenerator.GetBytes(16);

            var hash = ComputeHash(answer, salt);

            context.Session.SetString(
                CaptchaSessionKey,
                Convert.ToBase64String(salt) + ":" + hash);

            context.Session.SetString(
                CaptchaExpiryKey,
                DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeSeconds().ToString());

            var svg = BuildSvg(answer);

            return Encoding.UTF8.GetBytes(svg);
        }

        public bool ValidateCaptcha(string? answer)
        {
            var context = _httpContextAccessor.HttpContext;

            if (context == null)
                return false;

            var storedValue = context.Session.GetString(CaptchaSessionKey);
            var expiryValue = context.Session.GetString(CaptchaExpiryKey);

            // Consume the challenge regardless of the result.
            context.Session.Remove(CaptchaSessionKey);
            context.Session.Remove(CaptchaExpiryKey);

            if (string.IsNullOrWhiteSpace(storedValue) ||
                string.IsNullOrWhiteSpace(expiryValue) ||
                string.IsNullOrWhiteSpace(answer))
            {
                return false;
            }

            if (!long.TryParse(expiryValue, out var expiry))
                return false;

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry)
                return false;

            var parts = storedValue.Split(':');

            if (parts.Length != 2)
                return false;

            try
            {
                var salt = Convert.FromBase64String(parts[0]);
                var expectedHash = Convert.FromBase64String(parts[1]);

                var suppliedHash = Convert.FromBase64String(
                    ComputeHash(answer.Trim().ToUpperInvariant(), salt));

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
                var index = RandomNumberGenerator.GetInt32(Characters.Length);
                result.Append(Characters[index]);
            }

            return result.ToString();
        }

        private static string ComputeHash(string answer, byte[] salt)
        {
            var input = salt.Concat(
                Encoding.UTF8.GetBytes(answer)).ToArray();

            return Convert.ToBase64String(
                SHA256.HashData(input));
        }

        private static string BuildSvg(string answer)
        {
            Func<int, int, int> random = RandomNumberGenerator.GetInt32;

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