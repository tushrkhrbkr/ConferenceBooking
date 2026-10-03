using Azure;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using ConferenceBooking.Services.AuthAPI.Data;
using ConferenceBooking.Services.AuthAPI.Models.Dto;
using Microsoft.AspNetCore.Mvc;
using ConferenceBooking.Services.AuthAPI.Models;
using ConferenceBooking.Services.AuthAPI.Service.IService;
using Microsoft.EntityFrameworkCore;

namespace ConferenceBooking.Services.AuthAPI.Service
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ResponseDto _response;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IMapper _mapper;

        public AuthService(ApplicationDbContext db,UserManager<ApplicationUser> userManager,RoleManager<IdentityRole>roleManager, IJwtTokenGenerator jwtTokenGenerator, IMapper mapper)
        {
            _db = db;
            _response = new ResponseDto();
            _userManager = userManager;
            _roleManager = roleManager;
            _jwtTokenGenerator = jwtTokenGenerator;
            _mapper = mapper;
        }

        public async Task<LoginResponseDto> Login(LoginRequestDto loginRequestDto)
        {
            var user = _db.ApplicationUsers.FirstOrDefault(u => u.UserName.ToLower() == loginRequestDto.UserName.ToLower());
            bool isValid = await _userManager.CheckPasswordAsync(user, loginRequestDto.Password);
            if(user==null || isValid == false)
            {
                return new LoginResponseDto() { User = null, Token = "" };
            }
            if(await _userManager.IsLockedOutAsync(user))
            {
                return new LoginResponseDto() { User = null, Token = "block" };
            }
            //if user found, Generate Jwt token
            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwtTokenGenerator.GenerateToken(user,roles);
            var displayName = _db.Tbl_UserProfile.FirstOrDefault(u => u.UserId.ToLower() == user.Id.ToLower()).DisplayName;

            UserDto userDto = new()
            {
                ID = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                DisplayName = displayName
            };
            LoginResponseDto loginResponseDto = new()
            {
                User = userDto,
                Token = token
            };
            return loginResponseDto;
        }

        public async Task<ResponseDto> RequestRegistration(RegistrationRequestDto newrequest)
        {
            try
            {
                RegistrationRequest obj = _mapper.Map<RegistrationRequest>(newrequest);
                _db.Tbl_RegistrationRequest.Add(obj);
                _db.SaveChanges();
                _response.Result = _mapper.Map<RegistrationRequestDto>(obj);

            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;

            }
            return _response;
        }

        public async Task<ResponseDto> ApproveRegistration(ApproveRegistrationDto approveRequest)
        {
            var existingRequest = await _db.Tbl_RegistrationRequest.FirstOrDefaultAsync(u => u.RegistrationRequestId == approveRequest.RegistrationRequestId);

            if (existingRequest != null)
            {
                RegistrationRequestDto registrationRequestDto = _mapper.Map<RegistrationRequestDto>(existingRequest);
                ApplicationUser user = new()
                {
                    //Id=registrationRequestDto.UserID,
                    UserName = registrationRequestDto.Username,
                    Email = registrationRequestDto.Email,
                    NormalizedEmail = registrationRequestDto.Email.ToUpper(),
                    PhoneNumber = registrationRequestDto.MobileNumber
                };

                try
                {
                    var result = await _userManager.CreateAsync(user, registrationRequestDto.Password);

                    if (result.Succeeded)
                    {
                        var createdUser = await _userManager.FindByNameAsync(registrationRequestDto.Username);

                        existingRequest.RequestStatus = Enums.RegistrationRequestStatus.Approved;
                        existingRequest.ReviewedAtUtc = DateTime.UtcNow;
                        existingRequest.ReviewedByUserId = approveRequest.ReviewedByUserId;
                        existingRequest.ReviewRemarks = approveRequest.Remarks;

                        await _db.SaveChangesAsync();

                        UserProfileDto newUser = new()
                        {
                            UserId = createdUser.Id,
                            Username = registrationRequestDto.Username,
                            DisplayName = registrationRequestDto.DisplayName,
                            Designation = registrationRequestDto.Designation,
                            EmailAddress = registrationRequestDto.Email,
                            MobileNumber = registrationRequestDto.MobileNumber,
                            DeskPhone = registrationRequestDto.DeskPhone,
                            DepartmentId = approveRequest.DepartmentId,
                            IsActive = true
                        };


                        UserProfile obj = _mapper.Map<UserProfile>(newUser);
                        _db.Tbl_UserProfile.Add(obj);
                        _db.SaveChanges();

                        await AssignRole(createdUser.UserName, approveRequest.RoleName);
                        _response.Result = _mapper.Map<UserProfileDto>(obj);
                        _response.Message = "User created successfully";

                    }
                    else
                    {
                        _response.IsSuccess = false;
                        _response.Message = result.Errors.FirstOrDefault().Description;
                   
                    }
                }
                catch (Exception ex)
                {
                    _response.IsSuccess = false;
                    _response.Message = ex.Message;
                }

            }
            else
            {
                _response.IsSuccess = false;
                _response.Message = "Error accessing current request";
            }
            return _response;
        }
        
        public async Task<ResponseDto> RejectRegistration(RejectRegistrationDto rejectRequest)
        {
            var existingRequest = await _db.Tbl_RegistrationRequest.FirstOrDefaultAsync(u => u.RegistrationRequestId == rejectRequest.RegistrationRequestId);

            if (existingRequest != null)
            {
                try
                {
                    existingRequest.RequestStatus = Enums.RegistrationRequestStatus.Rejected;
                    existingRequest.ReviewedAtUtc = DateTime.UtcNow;
                    existingRequest.ReviewedByUserId = rejectRequest.ReviewedByUserId;
                    existingRequest.ReviewRemarks = rejectRequest.Remarks;
                    await _db.SaveChangesAsync();

                    _response.Message = "Request rejection successful";
                }
                catch (Exception ex)
                {
                    _response.IsSuccess = false;
                    _response.Message = ex.Message;
                }

            }
            else
            {
                _response.IsSuccess = false;
                _response.Message = "Error accessing current request";
            }
            return _response;
        }
               
        public async Task<bool> AssignRole(string username, string roleName)
        {            
            var user = _db.ApplicationUsers.FirstOrDefault(u => u.UserName.ToLower() == username.ToLower());
            if (user != null)
            {
                if (!_roleManager.RoleExistsAsync(roleName).GetAwaiter().GetResult())
                {
                    _roleManager.CreateAsync(new IdentityRole(roleName)).GetAwaiter().GetResult();
                }
                await _userManager.AddToRoleAsync(user, roleName);
                return true;
            }
            return false;
        }

        public async Task<bool> RevokeRole(string username, string roleName)
        {
            var user = _db.ApplicationUsers.FirstOrDefault(u => u.UserName.ToLower() == username.ToLower());
            if (user != null)
            {
                await _userManager.RemoveFromRoleAsync(user, roleName);
                return true;
            }
            return false;
        }

        public ResponseDto GetRole(string userid)
        {
            var user = _db.ApplicationUsers.FirstOrDefault(u => u.Id == userid);            
            if (user != null)
            {
                try
                {
                    var roleList = _userManager.GetRolesAsync(user).GetAwaiter().GetResult();
                    _response.Result = roleList;
                }
                catch (Exception ex)
                {
                    _response.IsSuccess = false;
                    _response.Message = ex.Message;
                }
                               
            }
            else
            {
                _response.IsSuccess= false;
                _response.Message = "No user found!!!";
            }
            return _response;
        }
        
        public async Task <ResponseDto> ChangePassword(PasswordChangeDto model)
        {
            var user = await _userManager.FindByIdAsync(model.UserID);
            if (user != null)
            {
                var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
                if (result.Succeeded)
                {
                    _response.Result = "Password Changed Successfully.";
                }
                else
                {
                    _response.IsSuccess = false;
                    _response.Message = result.Errors.FirstOrDefault().Description;
                }
            }
            else
            {
                _response.IsSuccess = false;
                _response.Message = "No user found!!!";
            }
            return _response;
        }
        
        public async Task<ResponseDto> UpdateUserData(string userid, string displayName, string phoneNumber)
        {
            var userToUpdate = await _userManager.FindByIdAsync(userid);
            if (userToUpdate != null)
            {
                try
                {
                    
                    userToUpdate.PhoneNumber = phoneNumber;
                    var updateResult = await _userManager.UpdateAsync(userToUpdate);
                    if (updateResult.Succeeded)
                    {
                        var obj=_db.Tbl_UserProfile.First(i=>i.UserId==userid);
                        obj.DisplayName = displayName;
                        obj.MobileNumber = phoneNumber;
                        _db.Tbl_UserProfile.Update(obj);
                        _db.SaveChanges();
                    }
                    else
                    {
                        _response.IsSuccess = false;
                        _response.Message = updateResult.Errors.FirstOrDefault().Description;
                    }
                }
                catch (Exception ex)
                {
                    _response.IsSuccess = false;
                    _response.Message = ex.Message;
                }

            }
            else
            {
                _response.IsSuccess = false;
                _response.Message = "No user found!!!";
            }
            return _response;
        }
        
        public ResponseDto SaveUserSession(UserLoginSessionsDto userLoginSessionsDto)
        {
            try
            {
                UserLoginSessions obj = _mapper.Map<UserLoginSessions>(userLoginSessionsDto);
                _db.Tbl_UserLoginSessions.Add(obj);
                _db.SaveChanges();
                _response.Result = _mapper.Map<UserLoginSessionsDto>(obj);

            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;

            }
            return _response;
        }
        
        public ResponseDto GetUserSession(Guid sessionId)
        {
            var session = _db.Tbl_UserLoginSessions.FirstOrDefault(u => u.SessionID == sessionId);
            if (session != null)
            {
                _response.Result = session;
            }
            else
            {
                _response.IsSuccess = false;
                _response.Message = "No user found!!!";
            }
            return _response;
        }
        
        public ResponseDto UpdateUserSession(UserLoginSessionsDto userLoginSessionsDto)
        {
            try
            {
                UserLoginSessions obj = _mapper.Map<UserLoginSessions>(userLoginSessionsDto);
                _db.Tbl_UserLoginSessions.Update(obj);
                _db.SaveChanges();
                _response.Result = _mapper.Map<UserLoginSessionsDto>(obj);

            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;

            }
            return _response;
        }
        
        public ResponseDto GetExpiredUserSession(TimeSpan _timeout)
        {
            var expiredSession = _db.Tbl_UserLoginSessions.Where(x=> x.LogoutTime == null && x.LastActivity.Add(_timeout)<DateTime.Now).ToList();
            if (expiredSession != null)
            {
                _response.Result = expiredSession;
            }
            else
            {
                _response.IsSuccess = false;
                _response.Message = "No expired session found!!!";
            }
            return _response;
        }
    
    }
}