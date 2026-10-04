using ConferenceBooking.Web.Models;
using ConferenceBooking.Web.Service.IService;
using ConferenceBooking.Web.Models.AuthLogin;
using ConferenceBooking.Web.Utility;

namespace ConferenceBooking.Web.Service
{
    public class AuthService : IAuthService
    {
        private readonly IBaseService _baseService;
        public AuthService(IBaseService baseService)
        {
            _baseService = baseService;
        }

        
        public async Task<ResponseDto?> Login(LoginRequestDto loginRequestDto)
        {
            return await _baseService.SendAsync(new RequestDto()
            {
                ApiType = SD.ApiType.POST,
                Url = SD.AuthAPIBase + "/api/auth/login",
                Data = loginRequestDto
            }, withBearer: false);
        }
        
        public async Task<ResponseDto?> PasswordChange(PasswordChangeDto model)
        {
            throw new NotImplementedException();
        }

        //public async Task<ResponseDto?> Register(ADRegistrationRequestDto registrationRequestDto)
        //{
        //    return await _baseService.SendAsync(new RequestDto()
        //    {
        //        ApiType = SD.ApiType.POST,
        //        Url = SD.AuthAPIBase + "/api/auth/register",
        //        Data = registrationRequestDto
        //    }, withBearer: false);
        //}

        public async Task<ResponseDto?> UpdateUserData(string userid, string displayName, string email, string phoneNumber)
        {
            return await _baseService.SendAsync(new RequestDto()
            {
                ApiType = SD.ApiType.POST,
                Url = SD.AuthAPIBase + "/api/auth/updateuserdata/" + userid + "/" + displayName + "/" + email + "/" + phoneNumber
            }, withBearer: false);
        }

        //public async Task<ResponseDto?> UserProfileRegister(UserProfileRegistrationDto userProfileRegistrationDto)
        //{
        //    return await _baseService.SendAsync(new RequestDto()
        //    {
        //        ApiType = SD.ApiType.POST,
        //        Url = SD.AuthAPIBase + "/api/auth/UserProfileRegister",
        //        Data = userProfileRegistrationDto
        //    }, withBearer: false);
        //}

        //public async Task<ResponseDto?> AssignRoleAsync(ADRegistrationRequestDto registrationRequestDto)
        //{
        //    return await _baseService.SendAsync(new RequestDto()
        //    {
        //        ApiType = SD.ApiType.POST,
        //        Url = SD.AuthAPIBase + "/api/auth/AssignRole",
        //        Data = registrationRequestDto
        //    });
        //}


        public async Task<ResponseDto?> GetUserRole(string Uid)
        {
            return await _baseService.SendAsync(new RequestDto()
            {
                ApiType = SD.ApiType.GET,
                Url = SD.AuthAPIBase + "/api/auth/GetUserRole/" + Uid,
                Data = Uid
            }, withBearer: false);
        }

        public async Task<ResponseDto?> SaveUserSession(UserLoginSessionsDto userLoginSessionsDto)
        {
            return await _baseService.SendAsync(new RequestDto()
            {
                ApiType = SD.ApiType.POST,
                Url = SD.AuthAPIBase + "/api/auth/SaveUserSession",
                Data = userLoginSessionsDto
            }, withBearer: false);
        }

        public async Task<ResponseDto?> GetUserSession(Guid sessionId)
        {
            return await _baseService.SendAsync(new RequestDto()
            {
                ApiType = SD.ApiType.GET,
                Url = SD.AuthAPIBase + "/api/auth/GetUserSession/" + sessionId
            }, withBearer: false);
        }

        public async Task<ResponseDto?> UpdateUserSession(UserLoginSessionsDto userLoginSessionsDto)
        {
            return await _baseService.SendAsync(new RequestDto()
            {
                ApiType = SD.ApiType.POST,
                Url = SD.AuthAPIBase + "/api/auth/UpdateUserSession",
                Data = userLoginSessionsDto
            }, withBearer: false);
        }

        public async Task<ResponseDto?> ForgotPassword(
    PasswordResetRequestDto request)
        {
            return await _baseService.SendAsync(new RequestDto()
            {
                ApiType = SD.ApiType.POST,

                Url = SD.AuthAPIBase +
                      "/api/auth/password/forgot",

                Data = request
            },
            withBearer: false);
        }


        public async Task<ResponseDto?> VerifyPasswordResetOtp(
            PasswordResetVerifyOtpDto request)
        {
            return await _baseService.SendAsync(new RequestDto()
            {
                ApiType = SD.ApiType.POST,

                Url = SD.AuthAPIBase +
                      "/api/auth/password/verify-otp",

                Data = request
            },
            withBearer: false);
        }


        public async Task<ResponseDto?> ResetPassword(
            PasswordResetCompleteDto request)
        {
            return await _baseService.SendAsync(new RequestDto()
            {
                ApiType = SD.ApiType.POST,

                Url = SD.AuthAPIBase +
                      "/api/auth/password/reset",

                Data = request
            },
            withBearer: false);
        }


        private static Dictionary<string, string> BuildClientIpHeader(
            string? clientIpAddress)
        {
            var headers = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(clientIpAddress))
            {
                headers["X-Client-IP"] = clientIpAddress;
            }

            return headers;
        }
    }
}
