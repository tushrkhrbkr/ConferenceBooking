using ConferenceBooking.Services.EmailAPI.Models;
using ConferenceBooking.Services.EmailAPI.Models.Dto;
using ConferenceBooking.Services.EmailAPI.Service.IService;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace ConferenceBooking.Services.EmailAPI.Service
{
    public class EmailAPIService : IEmailAPIService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailAPIService> _logger;

        public EmailAPIService(
            IOptions<EmailSettings> emailSettings,
            ILogger<EmailAPIService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task SendPasswordResetOtpAsync(
            PasswordResetOtpEmailDto request)
        {
            if (string.IsNullOrWhiteSpace(request.ToEmail))
            {
                throw new ArgumentException(
                    "Recipient email address is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Otp))
            {
                throw new ArgumentException(
                    "OTP is required.");
            }

            if (request.ValidityMinutes <= 0)
            {
                throw new ArgumentException(
                    "OTP validity must be greater than zero.");
            }

            ValidateConfiguration();

            var subject = "CONVENE - Password Reset OTP";

            var htmlBody = BuildHtmlBody(
                request.Otp,
                request.ValidityMinutes);

            var plainTextBody = BuildPlainTextBody(
                request.Otp,
                request.ValidityMinutes);

            using var message = new MailMessage
            {
                From = new MailAddress(
                    _emailSettings.FromEmail,
                    _emailSettings.FromName),

                Subject = subject,

                Body = htmlBody,

                IsBodyHtml = true
            };

            message.To.Add(
                new MailAddress(request.ToEmail));

            // Alternate plain-text representation.
            message.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    plainTextBody,
                    null,
                    "text/plain"));

            using var smtpClient = new SmtpClient(
                _emailSettings.SmtpHost,
                _emailSettings.SmtpPort)
            {
                EnableSsl = _emailSettings.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(
                    _emailSettings.SmtpUsername))
            {
                smtpClient.Credentials =
                    new NetworkCredential(
                        _emailSettings.SmtpUsername,
                        _emailSettings.SmtpPassword);
            }
            else
            {
                smtpClient.UseDefaultCredentials = true;
            }

            try
            {
                await smtpClient.SendMailAsync(message);

                _logger.LogInformation(
                    "Password reset OTP email sent successfully to {Email}.",
                    MaskEmail(request.ToEmail));
            }
            catch (Exception ex)
            {
                // Never log the OTP.
                _logger.LogError(
                    ex,
                    "Failed to send password reset OTP email to {Email}.",
                    MaskEmail(request.ToEmail));

                throw;
            }
        }

        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(
                    _emailSettings.SmtpHost))
            {
                throw new InvalidOperationException(
                    "Email SMTP host is not configured.");
            }

            if (_emailSettings.SmtpPort <= 0)
            {
                throw new InvalidOperationException(
                    "Email SMTP port is not configured correctly.");
            }

            if (string.IsNullOrWhiteSpace(
                    _emailSettings.FromEmail))
            {
                throw new InvalidOperationException(
                    "Email sender address is not configured.");
            }
        }

        private static string BuildHtmlBody(
    string otp,
    int validityMinutes)
        {
            var encodedOtp =
                WebUtility.HtmlEncode(otp);

            return
                "<!DOCTYPE html>" +
                "<html>" +
                "<head>" +
                "<meta charset=\"utf-8\" />" +
                "<title>CONVENE Password Reset</title>" +
                "</head>" +
                "<body style=\"font-family:Arial,Helvetica,sans-serif;" +
                "background-color:#f5f6f8;padding:30px;\">" +

                "<div style=\"max-width:600px;margin:auto;" +
                "background:#ffffff;padding:30px;" +
                "border-radius:8px;\">" +

                "<h2 style=\"margin-top:0;\">CONVENE</h2>" +

                "<p>" +
                "You requested to reset your " +
                "CONVENE account password." +
                "</p>" +

                "<p>" +
                "Your One-Time Password (OTP) is:" +
                "</p>" +

                "<div style=\"font-size:32px;font-weight:bold;" +
                "letter-spacing:8px;text-align:center;" +
                "padding:20px;background:#f1f3f5;" +
                "margin:20px 0;\">" +

                encodedOtp +

                "</div>" +

                "<p>" +
                "This OTP is valid for " +
                "<strong>" +
                validityMinutes +
                " minutes</strong>." +
                "</p>" +

                "<p>" +
                "If you did not request a password reset, " +
                "please ignore this email." +
                "</p>" +

                "<hr />" +

                "<p style=\"font-size:12px;color:#777;\">" +
                "This is an automated message from " +
                "CONVENE - Conference &amp; Venue " +
                "Management System." +
                "</p>" +

                "</div>" +
                "</body>" +
                "</html>";
        }

        private static string BuildPlainTextBody(
     string otp,
     int validityMinutes)
        {
            return
                "CONVENE - Password Reset" +
                Environment.NewLine +
                Environment.NewLine +

                "You requested to reset your " +
                "CONVENE account password." +
                Environment.NewLine +
                Environment.NewLine +

                "Your One-Time Password (OTP) is:" +
                Environment.NewLine +
                Environment.NewLine +

                otp +
                Environment.NewLine +
                Environment.NewLine +

                "This OTP is valid for " +
                validityMinutes +
                " minutes." +
                Environment.NewLine +
                Environment.NewLine +

                "If you did not request a password reset, " +
                "please ignore this email." +
                Environment.NewLine +
                Environment.NewLine +

                "CONVENE" +
                Environment.NewLine +
                "Conference & Venue Management System";
        }

        private static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return "***";
            }

            var atIndex = email.IndexOf('@');

            if (atIndex <= 0)
            {
                return "***";
            }

            var localPart = email[..atIndex];

            if (localPart.Length == 1)
            {
                return $"*@{email[(atIndex + 1)..]}";
            }

            if (localPart.Length == 2)
            {
                return $"{localPart[0]}*@{email[(atIndex + 1)..]}";
            }

            return
                $"{localPart[0]}***{localPart[^1]}@" +
                email[(atIndex + 1)..];
        }
    }
}