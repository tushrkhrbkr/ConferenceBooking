using ConferenceBooking.Web.Models;
using ConferenceBooking.Web.Models.AuthLogin;

namespace ConferenceBooking.Web.Service.IService
{
    public interface IAuthService
    {
        //Task<ResponseDto?> Register(ADRegistrationRequestDto registrationRequestDto);
        Task<ResponseDto?> Login(LoginRequestDto loginRequestDto);

        //Task<ResponseDto?> UserProfileRegister(UserProfileRegistrationDto userProfileRegistrationDto);       
        //Task<ResponseDto?> GetUserRole(string Uid);
        //Task<ResponseDto?> AssignRoleAsync(ADRegistrationRequestDto registrationRequestDto);
        Task<ResponseDto?> PasswordChange(PasswordChangeDto model);
        Task<ResponseDto?> GetUserRole(string Uid);
        Task<ResponseDto?> UpdateUserData(string userid, string displayName, string email, string phoneNumber);
        Task<ResponseDto?> SaveUserSession(UserLoginSessionsDto userLoginSessionsDto);
        Task<ResponseDto?> GetUserSession(Guid sessionId);
        Task<ResponseDto?> UpdateUserSession(UserLoginSessionsDto userLoginSessionsDto);

        Task<ResponseDto?> ForgotPassword(PasswordResetInternalRequestDto request);

        Task<ResponseDto?> VerifyPasswordResetOtp(PasswordResetInternalVerifyOtpdto request);

        Task<ResponseDto?> ResetPassword(PasswordResetCompleteDto request);


        Task<ResponseDto?> CheckRegistration(RegistrationDuplicateCheckDto request);

        Task<ResponseDto?> Register(RegistrationRequestDto request);
        Task<ResponseDto?> GetPendingRegistrations();
        Task<ResponseDto?> GetRegistrationLookups();
        Task<ResponseDto?> ApproveRegistration(ApproveRegistrationDto request);
        Task<ResponseDto?> RejectRegistration(RejectRegistrationDto request);
        Task<ResponseDto?> GetDepartments();

        Task<ResponseDto?> CreateDepartment(
            DepartmentManagementDto request);

        Task<ResponseDto?> UpdateDepartment(
            int departmentId,
            DepartmentManagementDto request);

        Task<ResponseDto?> SetDepartmentStatus(
            int departmentId,
            DepartmentStatusDto request);

    }
}
