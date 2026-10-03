using ConferenceBooking.Services.AuthAPI.Models;
using ConferenceBooking.Services.AuthAPI.Models.Dto;

namespace ConferenceBooking.Services.AuthAPI.Service.IService
{
    public interface IAuthService
    {
        Task<LoginResponseDto> Login(LoginRequestDto loginRequestDto);

        Task<ResponseDto> RequestRegistration(RegistrationRequestDto registrationRequestDto);
        Task<ResponseDto> ApproveRegistration(ApproveRegistrationDto approveRequest);
        Task<ResponseDto> RejectRegistration(RejectRegistrationDto rejectRequest);
       
        Task<bool> AssignRole(string username, string roleName);
        Task<bool> RevokeRole(string username, string roleName);
        ResponseDto GetRole(string userid);

        Task<ResponseDto> ChangePassword(PasswordChangeDto model);
        Task<ResponseDto> UpdateUserData(string userid, string displayName, string phoneNumber);

        ResponseDto SaveUserSession(UserLoginSessionsDto userLoginSessionsDto);

        ResponseDto GetUserSession(Guid sessionId);

        ResponseDto UpdateUserSession(UserLoginSessionsDto userLoginSessionsDto);

        ResponseDto GetExpiredUserSession(TimeSpan _timeout);
    }
}