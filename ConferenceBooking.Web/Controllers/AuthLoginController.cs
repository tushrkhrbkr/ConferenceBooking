using ConferenceBooking.Web.Models;
using ConferenceBooking.Web.Models.AuthLogin;
using ConferenceBooking.Web.Service;
using ConferenceBooking.Web.Service.IService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.DirectoryServices;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Principal;

namespace MUS.Webapp.Controllers
{
    public class AuthLoginController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ITokenProvider _tokenProvider;
        private readonly ICaptchaService _captchaService;

        public AuthLoginController(IAuthService authService, ITokenProvider tokenProvider, ICaptchaService captchaService)
        {
            _authService = authService;
            _tokenProvider = tokenProvider;
            _captchaService = captchaService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View(new LoginRequestDto());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginRequestDto obj, string captcha, string captchaId)
        {
            if (!_captchaService.ValidateCaptcha(captchaId, captcha))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invalid or expired CAPTCHA. Please try again.");

                return View(obj);
            }

            if (!ModelState.IsValid)
                return View(obj);

            ResponseDto responseDto = await _authService.Login(obj);

            if (responseDto != null && responseDto.IsSuccess)
            {
                LoginResponseDto loginResponseDto = JsonConvert.DeserializeObject<LoginResponseDto>(Convert.ToString(responseDto.Result));

                var userid = loginResponseDto.User.ID;
                var sessionId = Guid.NewGuid();

                HttpContext.Session.SetString("UserID", userid);
                HttpContext.Session.SetString("SessionId", sessionId.ToString());

                ResponseDto userRoleResponseDto = await _authService.GetUserRole(userid);

                List<string> roleList = new();

                if (userRoleResponseDto != null &&
                    userRoleResponseDto.IsSuccess)
                {
                    HttpContext.Session.SetString(
                        "userRole",
                        userRoleResponseDto.Result.ToString());

                    roleList =
                        JsonConvert.DeserializeObject<List<string>>(
                            Convert.ToString(userRoleResponseDto.Result));
                }

                await SignInUser(loginResponseDto, roleList);

                _tokenProvider.SetToken(loginResponseDto.Token);

                UserLoginSessionsDto userLoginSessionsDto =
                    new UserLoginSessionsDto
                    {
                        SessionID = sessionId,
                        UserId = userid,
                        LoginEndpoint = "U&P",
                        LoginTime = DateTime.Now,
                        LastActivity = DateTime.Now
                    };

                await _authService.SaveUserSession(userLoginSessionsDto);

                TempData["success"] = "Login successful";

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(
                string.Empty,
                responseDto?.Message ?? "Unable to sign in. Please try again.");

            return View(obj);
        }
        
        public async Task<IActionResult> Logout()
        {
            var sessionIdStr = HttpContext.Session.GetString("SessionId");
            if (!string.IsNullOrEmpty(sessionIdStr) && Guid.TryParse(sessionIdStr, out var sessionId))
            {
                UserLoginSessionsDto userLoginSessionsDto = new UserLoginSessionsDto();
                ResponseDto sessionResponseDto = await _authService.GetUserSession(sessionId);
                if (sessionResponseDto != null && sessionResponseDto.IsSuccess)
                {
                    userLoginSessionsDto = JsonConvert.DeserializeObject<UserLoginSessionsDto>(Convert.ToString(sessionResponseDto.Result));
                }
                if (userLoginSessionsDto != null)
                {
                    userLoginSessionsDto.LogoutTime = DateTime.Now;
                    userLoginSessionsDto.LogoutReason = "ManualLogout";
                    await _authService.UpdateUserSession(userLoginSessionsDto);
                }

            }
            await HttpContext.SignOutAsync();
            _tokenProvider.ClearToken();
            return RedirectToAction("Login", "AuthLogin");
        }
        
        private async Task SignInUser(LoginResponseDto model, List<string> roleList)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(model.Token);
            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(JwtRegisteredClaimNames.Email, jwt.Claims.FirstOrDefault(u => u.Type == JwtRegisteredClaimNames.Email).Value));
            identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, jwt.Claims.FirstOrDefault(u => u.Type == JwtRegisteredClaimNames.Sub).Value));
            identity.AddClaim(new Claim(JwtRegisteredClaimNames.Name, model.User.DisplayName));

            identity.AddClaim(new Claim(ClaimTypes.Name, jwt.Claims.FirstOrDefault(u => u.Type == JwtRegisteredClaimNames.Name).Value));
            //identity.AddClaim(new Claim(ClaimTypes.Role, jwt.Claims.FirstOrDefault(u => u.Type == "role").Value));
            foreach (var item in roleList)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, item));
            }

            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }

        [HttpGet]
        public IActionResult PasswordChange()
        {
            var userid = HttpContext.Session.GetString("UserID");
            //var userid = _sessionCache.Get<string>("UserID");
            if (userid == null)
            {
                return RedirectToAction("Login", "AuthLogin");
            }

            return View();
        }
        
        [HttpPost]
        public async Task<IActionResult> PasswordChange(PasswordChangeDto model)
        {
            var userid = HttpContext.Session.GetString("UserID");
            //var userid = _sessionCache.Get<string>("UserID");
            if (userid == null)
            {
                return RedirectToAction("Login", "AuthLogin");
            }
            model.UserID = userid;
            
            if (ModelState.IsValid)
            {
                ResponseDto responseDto = await _authService.PasswordChange(model);

                if (responseDto != null && responseDto.IsSuccess)
                {
                    var result = responseDto.Result;
                    TempData["success"] = result.ToString();
                    await HttpContext.SignOutAsync();
                    _tokenProvider.ClearToken();
                    return RedirectToAction("Login", "AuthLogin");
                }
                TempData["error"] = responseDto.Message;
            }
            return View();
        }

        public IActionResult AccessDenied(string ReturnUrl)
        {
            return View();
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email, string captcha, string captchaId)
        {
            if (!_captchaService.ValidateCaptcha(captchaId, captcha))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invalid or expired CAPTCHA. Please try again.");

                ViewBag.Email = email;
                return View();
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Please enter your registered email address.");

                return View();
            }

            email = email.Trim();

            var request = new PasswordResetInternalRequestDto
            {
                Email = email,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                            ?? string.Empty
            };

            var response = await _authService.ForgotPassword(request);

            if (response == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The password reset service is currently unavailable. " +
                    "Please try again later or contact the CONVENE Administrator.");

                ViewBag.Email = email;
                return View();
            }

            string status = string.Empty;

            if (response.Result != null)
            {
                try
                {
                    var resultJson = JObject.Parse(
                        JsonConvert.SerializeObject(response.Result));

                    status = resultJson["status"]?.ToString() ?? string.Empty;
                }
                catch
                {
                    status = string.Empty;
                }
            }

            if (!response.IsSuccess || status != "OTP_QUEUED")
            {
                ModelState.AddModelError(
                    string.Empty,
                    response.Message ?? "Unable to process your request.");

                ViewBag.Email = email;
                return View();
            }

            TempData["PasswordResetEmail"] = email;

            TempData["success"] =
                "Email sent successfully. Please check your inbox.";

            return RedirectToAction(nameof(VerifyOtp));
        }

        [HttpGet]
        public IActionResult VerifyOtp()
        {
            var email = TempData.Peek("PasswordResetEmail")?.ToString();

            if (string.IsNullOrWhiteSpace(email))
                return RedirectToAction(nameof(ForgotPassword));

            ViewBag.Email = email;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(string email, string otp)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(otp))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Email and OTP are required.");

                ViewBag.Email = email;
                return View();
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(otp, @"^[0-9]{6}$"))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "OTP must contain exactly six numeric digits.");

                ViewBag.Email = email;
                return View();
            }

            var request = new PasswordResetInternalVerifyOtpdto
            {
                Email = email.Trim(),
                Otp = otp.Trim(),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                            ?? string.Empty
            };

            var response =
                await _authService.VerifyPasswordResetOtp(request);

            if (response == null || !response.IsSuccess)
            {
                ModelState.AddModelError(
                    string.Empty,
                    response?.Message ?? "OTP verification failed.");

                ViewBag.Email = email;
                return View();
            }

            var tokenResponse =
                JsonConvert.DeserializeObject<PasswordResetTokenResponseDto>(
                    Convert.ToString(response.Result) ?? string.Empty);

            if (tokenResponse == null ||
                string.IsNullOrWhiteSpace(tokenResponse.ResetToken))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Unable to continue password reset. Please request a new OTP.");

                ViewBag.Email = email;
                return View();
            }

            TempData["PasswordResetEmail"] = email.Trim();

            // The token is carried to the reset form, not exposed in the URL.
            TempData["PasswordResetToken"] = tokenResponse.ResetToken;

            return RedirectToAction(nameof(ResetPassword));
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            var resetToken =
                TempData["PasswordResetToken"]?.ToString();

            if (string.IsNullOrWhiteSpace(resetToken))
                return RedirectToAction(nameof(ForgotPassword));

            return View(new PasswordResetCompleteDto
            {
                ResetToken = resetToken
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(PasswordResetCompleteDto model)
        {
            if (string.IsNullOrWhiteSpace(model.ResetToken))
            {
                TempData["error"] =
                    "Your password reset session is invalid. Please request a new OTP.";

                return RedirectToAction(nameof(ForgotPassword));
            }

            if (!ModelState.IsValid)
                return View(model);

            model.IpAddress =
                HttpContext.Connection.RemoteIpAddress?.ToString()
                ?? string.Empty;

            var response = await _authService.ResetPassword(model);

            if (response != null && response.IsSuccess)
            {
                //TempData["success"] =
                //    "Your password has been reset successfully. Please log in.";

                TempData["PasswordResetCompleted"] = "true";
                return RedirectToAction(nameof(ResetPasswordSuccess));
            }

            ModelState.AddModelError(
                string.Empty,
                response?.Message ?? "Unable to reset password.");

            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordSuccess()
        {
            if (TempData["PasswordResetCompleted"]?.ToString() != "true")
            {
                return RedirectToAction(nameof(Login));
            }

            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegistrationRequestDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            RegistrationRequestDto model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Check duplicates before attempting registration.
            var duplicateResponse = await _authService.CheckRegistration(
                new RegistrationDuplicateCheckDto
                {
                    Email = model.Email
                });

            if (duplicateResponse == null ||
                !duplicateResponse.IsSuccess)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Registration service is temporarily unavailable. Please try again.");

                return View(model);
            }

            RegistrationDuplicateResultDto? duplicate = null;

            if (duplicateResponse.Result != null)
            {
                duplicate = JsonConvert.DeserializeObject
                    <RegistrationDuplicateResultDto>(
                        Convert.ToString(duplicateResponse.Result) ?? string.Empty);
            }

            if (duplicate == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Unable to validate the email address. Please try again.");

                return View(model);
            }

            if (!duplicate.CanRegister)
            {
                if (duplicate.RequiresPasswordReset)
                {
                    TempData["error"] =
                        "An account already exists for this email. Please use Forgot Password.";

                    return RedirectToAction(nameof(ForgotPassword));
                }

                ModelState.AddModelError(
                    string.Empty,
                    string.IsNullOrWhiteSpace(duplicate.Message)
                        ? "This email cannot be registered."
                        : duplicate.Message);

                return View(model);
            }

            var response = await _authService.Register(model);

            if (response != null && response.IsSuccess)
            {
                string registrationReference = string.Empty;

                if (response.Result != null)
                {
                    var resultJson = JObject.Parse(
                        JsonConvert.SerializeObject(response.Result));

                    registrationReference =
                        resultJson["registrationRequestId"]?.ToString()
                        ?? resultJson["RegistrationRequestId"]?.ToString()
                        ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(registrationReference))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Registration was submitted, but the reference number " +
                        "could not be retrieved. Please contact the administrator.");

                    return View(model);
                }

                TempData["RegistrationReference"] = registrationReference;

                return RedirectToAction(nameof(RegistrationSuccess));
            }

            ModelState.AddModelError(
                string.Empty,
                response?.Message ??
                "Registration could not be completed. Please try again.");

            return View(model);
        }

        [HttpGet]
        public IActionResult RegistrationSuccess()
        {
            var registrationReference =
                TempData["RegistrationReference"]?.ToString();

            if (string.IsNullOrWhiteSpace(registrationReference))
                return RedirectToAction(nameof(Register));

            ViewBag.RegistrationReference = registrationReference;

            return View();
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult CaptchaImage(string challengeId)
        {
            var image =
                _captchaService.GenerateCaptchaImage(challengeId);

            Response.Headers["Cache-Control"] =
                "no-store, no-cache, must-revalidate";

            Response.Headers["Pragma"] = "no-cache";

            return File(image, "image/svg+xml");
        }
    }
}
