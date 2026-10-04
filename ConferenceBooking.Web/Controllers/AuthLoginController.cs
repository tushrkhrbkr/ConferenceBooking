using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ConferenceBooking.Web.Service.IService;
using System.Security.Claims;
using ConferenceBooking.Web.Models;
using System.IdentityModel.Tokens.Jwt;
using System.DirectoryServices;
using System.Security.Principal;
using Microsoft.AspNetCore.HttpOverrides;
using System;
using ConferenceBooking.Web.Models.AuthLogin;
using Microsoft.AspNetCore.Http;

namespace MUS.Webapp.Controllers
{
    public class AuthLoginController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ITokenProvider _tokenProvider;

        public AuthLoginController(IAuthService authService, ITokenProvider tokenProvider)
        {
            _authService = authService;
            _tokenProvider = tokenProvider;
        }
        
        [HttpGet]
        public IActionResult Login()
        {            
            return View();
        }
        

        [HttpPost]
        public async Task<IActionResult> Login(LoginRequestDto obj)
        {

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
                if (userRoleResponseDto != null && userRoleResponseDto.IsSuccess)
                {
                    HttpContext.Session.SetString("userRole", userRoleResponseDto.Result.ToString());
                    roleList = JsonConvert.DeserializeObject<List<string>>(Convert.ToString(userRoleResponseDto.Result));
                }

                await SignInUser(loginResponseDto, roleList);
                _tokenProvider.SetToken(loginResponseDto.Token);

                UserLoginSessionsDto userLoginSessionsDto = new UserLoginSessionsDto();
                userLoginSessionsDto.SessionID = sessionId;
                userLoginSessionsDto.UserId = userid;
                userLoginSessionsDto.LoginEndpoint = "U&P";
                userLoginSessionsDto.LoginTime = DateTime.Now;
                userLoginSessionsDto.LastActivity = DateTime.Now;

                await _authService.SaveUserSession(userLoginSessionsDto);
                TempData["success"] = "Login successful";
                return RedirectToAction("Index", "Home");
            }
            else
            {
                ModelState.AddModelError("CustomError", responseDto.Message);
                return View(obj);
            }

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
            return View();
        }
        
        [HttpPost]
        public async Task<IActionResult> PasswordChange(PasswordChangeDto model)
        {
            if (ModelState.IsValid)
            {
                var userid = HttpContext.Session.GetString("UserID");
                //var userid = _sessionCache.Get<string>("UserID");
                if (userid == null)
                {
                    return RedirectToAction("Login", "AuthLogin");
                }
                model.UserID = userid;
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
        public async Task<IActionResult> ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            var clientIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            var request = new PasswordResetInternalRequestDto
            {
                Email = email,
                IpAddress = clientIpAddress
            };

            ResponseDto resetRequestDto = await _authService.ForgotPassword(request);
            return View();
        }
        
    }
}
