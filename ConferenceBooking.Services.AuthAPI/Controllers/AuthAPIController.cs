using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ConferenceBooking.Services.AuthAPI.Data;
using ConferenceBooking.Services.AuthAPI.Models;
using ConferenceBooking.Services.AuthAPI.Models.Dto;
using ConferenceBooking.Services.AuthAPI.Service.IService;
using Microsoft.AspNetCore.Authorization;

namespace ConferenceBooking.Services.AuthAPI.Controllers
{
    
    [Route("api/auth")]
    [ApiController]
    public class AuthAPIController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ApplicationDbContext _db;
        protected ResponseDto _response;
        private IMapper _mapper;
        public AuthAPIController(IAuthService authService, IMapper mapper, ApplicationDbContext db)
        {
            _authService = authService;
            _response = new();
            _db = db;
            _mapper= mapper;           
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegistrationRequestDto model)
        {

            _response = await _authService.RequestRegistration(model);

            return Ok(_response);
        }

        [HttpPost("check-duplicate")]
        public async Task<IActionResult> CheckRegistrationDuplicate([FromBody] RegistrationDuplicateCheckDto model)
        {
            _response = await _authService.CheckRegistrationAsync(model);

            return Ok(_response);
        }


        [Authorize]
        [HttpPost("approve")]
        public async Task<IActionResult> Approve([FromBody] ApproveRegistrationDto model)
        {

            _response = await _authService.ApproveRegistration(model);

            return Ok(_response);
        }
        [Authorize]
        [HttpPost("reject")]
        public async Task<IActionResult> Reject([FromBody] RejectRegistrationDto model)
        {

            _response = await _authService.RejectRegistration(model);

            return Ok(_response);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto model)
        {
            var loginResponse = await _authService.Login(model);
            if (loginResponse.User == null && loginResponse.Token == "")
            {
                _response.IsSuccess = false;
                _response.Message = "Username or Password is incorrect";
                return BadRequest(_response);
            }
            else if(loginResponse.User == null && loginResponse.Token == "block")
            {
                _response.IsSuccess = false;
                _response.Message = "User is blocked. Please contact Administrator.";
                return BadRequest(_response);
            }
            _response.Result = loginResponse;
            return Ok(_response);
        }

        [Authorize]
        [HttpPost("AssignRole")]
        public async Task<IActionResult> AssignRole([FromBody] ManageRoleDto model)
        {
            var assignRoleSuccessful = await _authService.AssignRole(model.UserName, model.RoleName);
            if (!assignRoleSuccessful)
            {
                _response.IsSuccess = false;
                _response.Message = "Error Encountered";
                return BadRequest(_response);
            }
            return Ok(_response);
        }

        [Authorize]
        [HttpPost("RevokeRole")]
        public async Task<IActionResult> RevokeRole([FromBody] ManageRoleDto model)
        {
            var revokRoleSuccessful = await _authService.RevokeRole(model.UserName, model.RoleName);
            if (!revokRoleSuccessful)
            {
                _response.IsSuccess = false;
                _response.Message = "Error Encountered";
                return BadRequest(_response);
            }
            return Ok(_response);
        }

        [Authorize]
        [HttpGet("GetUserRole/{Uid}")]
        public IActionResult GetUserRole(string Uid)
        {
            var list = _authService.GetRole(Uid);
            if (list.Result == null)
            {
                _response.IsSuccess = false;
                _response.Message = "Error Encountered";
                return BadRequest(_response);
            }
            else
            {
                _response.Result = list.Result;
                return Ok(_response);
            }
        }

        [Authorize]
        [HttpPost("changePassword")]
        public async Task<IActionResult> PasswordChange([FromBody] PasswordChangeDto model)
        {
            var changePpResponse = await _authService.ChangePassword(model);
            if (changePpResponse.Result == null && !changePpResponse.IsSuccess)
            {
                _response.IsSuccess = false;
                _response.Message = changePpResponse.Message;
                return BadRequest(_response);
            }
            _response.Result = changePpResponse.Result;
            return Ok(_response);
        }

        [AllowAnonymous]
        [HttpPost("password/forgot")]
        public async Task<IActionResult> ForgotPassword([FromBody] PasswordResetRequestDto request)
        {
            _response = await _authService.ForgotPasswordAsync(request);
            return Ok(_response);
        }

        [AllowAnonymous]
        [HttpPost("password/verify-otp")]
        public async Task<IActionResult> VerifyPasswordResetOtp([FromBody] PasswordResetVerifyOtpDto request)
        {
            _response = await _authService.VerifyPasswordResetOtpAsync(request);
            return Ok(_response);
        }

        [AllowAnonymous]
        [HttpPost("password/reset")]
        public async Task<IActionResult> ResetPassword([FromBody] PasswordResetCompleteDto request)
        {
            _response = await _authService.ResetPasswordAsync(request);
            return Ok(_response);
        }

        [Authorize]
        [HttpPost("updateuserdata/{userid}/{displayName}/{phoneNumber}")]
        public async Task<IActionResult> UpdateUserData(string userid, string displayName, string phoneNumber)
        {
            var response = await _authService.UpdateUserData(userid, displayName, phoneNumber);
            if (response.Result == null && !response.IsSuccess)
            {
                _response.IsSuccess = false;
                _response.Message = response.Message;
                return BadRequest(_response);
            }
            _response.Result = response.Result;
            return Ok(_response);
        }


        [HttpPost("SaveUserSession")]
        public IActionResult SaveUserSession([FromBody] UserLoginSessionsDto userLoginSessionsDto)
        {
            var saveUserSessionSucessful = _authService.SaveUserSession(userLoginSessionsDto);
            if (saveUserSessionSucessful.Result == null)
            {
                _response.IsSuccess = false;
                _response.Message = "Error Encountered";
                return BadRequest(_response);
            }
            return Ok(_response);
        }

        [HttpGet("GetUserSession/{sessionId}")]
        public IActionResult GetUserSession(Guid sessionId)
        {
            var response = _authService.GetUserSession(sessionId);
            if (response.Result == null)
            {
                _response.IsSuccess = false;
                _response.Message = "Error Encountered";
                return BadRequest(_response);
            }
            else
            {
                _response.Result = response.Result;
                return Ok(_response);
            }
        }

        [HttpPost("UpdateUserSession")]
        public IActionResult UpdateUserSession([FromBody] UserLoginSessionsDto userLoginSessionsDto)
        {
            var saveUserSessionSucessful = _authService.UpdateUserSession(userLoginSessionsDto);
            if (saveUserSessionSucessful.Result == null)
            {
                _response.IsSuccess = false;
                _response.Message = "Error Encountered";
                return BadRequest(_response);
            }
            return Ok(_response);
        }

        [HttpGet("GetExpiredUserSession/{timeSpan}")]
        public IActionResult GetExpiredUserSession(TimeSpan _timeout)
        {
            var response = _authService.GetExpiredUserSession(_timeout);
            if (response.Result == null)
            {
                _response.IsSuccess = false;
                _response.Message = "No expired session";
                return BadRequest(_response);
            }
            else
            {
                _response.Result = response.Result;
                return Ok(_response);
            }
        }

    }
}
