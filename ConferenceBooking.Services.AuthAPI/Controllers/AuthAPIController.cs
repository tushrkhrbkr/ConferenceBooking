using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ConferenceBooking.Services.AuthAPI.Data;
using ConferenceBooking.Services.AuthAPI.Models;
using ConferenceBooking.Services.AuthAPI.Models.Dto;
using ConferenceBooking.Services.AuthAPI.Service.IService;

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
            var result = await _authService.Register(model);
            string errorMessage = result.Item1;
            string userId = result.Item2;
            if (!string.IsNullOrEmpty(errorMessage))
            {
                _response.IsSuccess = false;
                _response.Message = errorMessage;
                return BadRequest(_response);
            }
            _response.Result = userId;
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
        
        
        [HttpPost("AssignRole")]
        public async Task<IActionResult> AssignRole([FromBody] RegistrationRequestDto model)
        {
            var assignRoleSuccessful = await _authService.AssignRole(model.UserName, model.Role);
            if (!assignRoleSuccessful)
            {
                _response.IsSuccess = false;
                _response.Message = "Error Encountered";
                return BadRequest(_response);
            }
            return Ok(_response);
        }

        [HttpPost("RevokeRole")]
        public async Task<IActionResult> RevokeRole([FromBody] RegistrationRequestDto model)
        {
            var assignRoleSuccessful = await _authService.AssignRole(model.UserName, model.Role);
            if (!assignRoleSuccessful)
            {
                _response.IsSuccess = false;
                _response.Message = "Error Encountered";
                return BadRequest(_response);
            }
            return Ok(_response);
        }

        [HttpPost("UserProfileRegister")]
        public IActionResult UserProfileRegister([FromBody] UserProfileDto model)
        {
            var UserProfileSuccessful = _authService.UserProfileRegister(model);
            if (UserProfileSuccessful.Result==null)
            {
                _response.IsSuccess = false;
                _response.Message = "Error Encountered";
                return BadRequest(_response);
            }
            return Ok(_response);
        }
           

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
        
        [HttpPost("updateuserdata/{userid}/{displayName}/{email}/{phoneNumber}")]
        public async Task<IActionResult> UpdateUserData(string userid, string displayName, string email, string phoneNumber)
        {
            var response = await _authService.UpdateUserData(userid, displayName, email, phoneNumber);
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
