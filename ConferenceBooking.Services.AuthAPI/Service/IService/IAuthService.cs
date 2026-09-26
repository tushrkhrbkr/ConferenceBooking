using ConferenceBooking.Services.AuthAPI.Models;
using ConferenceBooking.Services.AuthAPI.Models.Dto;

namespace ConferenceBooking.Services.AuthAPI.Service.IService
{
    public interface IAuthService
    {
        Task<(string, string)> Register(RegistrationRequestDto registrationRequestDto);
        Task<LoginResponseDto> Login(LoginRequestDto loginRequestDto);
        Task<LoginResponseDto> LoginBySSO(string username);
        ResponseDto UserProfileRegister(UserProfileRegistrationDto profileRegistrationDto);        
        Task<bool> AssignRole(string username, string roleName);
        Task<bool> RevokeRole(string username, string roleName);
        ResponseDto GetRole(string userid);
        Task<ResponseDto> ChangePassword(PasswordChangeDto model);
        Task<ResponseDto> UpdateUserData(string userid, string displayName, string email, string phoneNumber);

        ResponseDto SaveUserSession(UserLoginSessionsDto userLoginSessionsDto);

        ResponseDto GetUserSession(Guid sessionId);

        ResponseDto UpdateUserSession(UserLoginSessionsDto userLoginSessionsDto);

        ResponseDto GetExpiredUserSession(TimeSpan _timeout);
    }
}