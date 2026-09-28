using ConferenceBooking.Web.Models;
using ConferenceBooking.Web.Models.AuthLogin;

namespace ConferenceBooking.Web.Service.IService
{
    public interface IAuthService
    {
        //Task<ResponseDto?> Register(ADRegistrationRequestDto registrationRequestDto);
        Task<ResponseDto?> Login(LoginRequestDto loginRequestDto);
        Task<ResponseDto?> LoginBySSO(string username);
        //Task<ResponseDto?> UserProfileRegister(UserProfileRegistrationDto userProfileRegistrationDto);       
        //Task<ResponseDto?> GetUserRole(string Uid);
        //Task<ResponseDto?> AssignRoleAsync(ADRegistrationRequestDto registrationRequestDto);
        Task<ResponseDto?> PasswordChange(PasswordChangeDto model);
        Task<ResponseDto?> GetUserRole(string Uid);
        Task<ResponseDto?> UpdateUserData(string userid, string displayName, string email, string phoneNumber);
        Task<ResponseDto?> SaveUserSession(UserLoginSessionsDto userLoginSessionsDto);
        Task<ResponseDto?> GetUserSession(Guid sessionId);
        Task<ResponseDto?> UpdateUserSession(UserLoginSessionsDto userLoginSessionsDto);

    }
}
