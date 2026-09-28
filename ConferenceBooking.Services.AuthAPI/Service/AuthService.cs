using Azure;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using ConferenceBooking.Services.AuthAPI.Data;
using ConferenceBooking.Services.AuthAPI.Models.Dto;
using Microsoft.AspNetCore.Mvc;
using ConferenceBooking.Services.AuthAPI.Models;
using ConferenceBooking.Services.AuthAPI.Service.IService;

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
            var displayName = _db.Tbl_User_Profile.FirstOrDefault(u => u.User_ID.ToLower() == user.Id.ToLower()).User_Agency;

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
        
        public async Task<LoginResponseDto> LoginBySSO(string username)
        {
            var user = _db.ApplicationUsers.FirstOrDefault(u => u.UserName.ToLower() == username.ToLower());            
            if (user == null)
            {
                return new LoginResponseDto() { User = null, Token = "" };
            }
            if (await _userManager.IsLockedOutAsync(user))
            {
                return new LoginResponseDto() { User = null, Token = "block" };
            }
            //if user found, Generate Jwt token
            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwtTokenGenerator.GenerateToken(user, roles);
            var displayName = _db.Tbl_User_Profile.FirstOrDefault(u => u.User_ID.ToLower() == user.Id.ToLower()).User_Agency;

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

        public async Task<(string,string)> Register(RegistrationRequestDto registrationRequestDto)
        {
            ApplicationUser user = new()
            {
                //Id=registrationRequestDto.UserID,
                UserName = registrationRequestDto.UserName,
                Email = registrationRequestDto.Email,
                NormalizedEmail = registrationRequestDto.Email.ToUpper(),
                PhoneNumber=registrationRequestDto.ContactNumber
            };
            if (registrationRequestDto.UserID == "" || registrationRequestDto.UserID == null)
            {
                try
                {
                    var result = await _userManager.CreateAsync(user, registrationRequestDto.Password);

                    if (result.Succeeded)
                    {
                        var createdUser = await _userManager.FindByNameAsync(registrationRequestDto.UserName);
                        return ("", createdUser.Id);
                    }
                    else
                    {
                        return (result.Errors.FirstOrDefault().Description, "");
                    }
                }
                catch (Exception ex)
                {
                    return ("Error Encountered", ex.Message);
                }
            }
            else
            {
                try
                {
                    // Finding the user based on userid
                    var userToUpdate= await _userManager.FindByIdAsync(registrationRequestDto.UserID);
                    // Update UserName and other details
                    userToUpdate.UserName = registrationRequestDto.UserName;
                    userToUpdate.Email = registrationRequestDto.Email;
                    userToUpdate.NormalizedEmail = registrationRequestDto.Email.ToUpper();
                    userToUpdate.PhoneNumber = registrationRequestDto.ContactNumber;

                    //Update password
                    var newPassword = registrationRequestDto.Password;
                    var token= await _userManager.GeneratePasswordResetTokenAsync(userToUpdate);
                    var passwordChangeResult = await _userManager.ResetPasswordAsync(userToUpdate, token, newPassword);                   

                    if (passwordChangeResult.Succeeded)
                    {
                        var updateResult = await _userManager.UpdateAsync(userToUpdate);
                        if (updateResult.Succeeded)
                        {
                            var updatedUser = await _userManager.FindByNameAsync(registrationRequestDto.UserName);
                            return ("", updatedUser.Id);
                        }
                        else
                        {
                            return (updateResult.Errors.FirstOrDefault().Description, "");
                        }
                    }
                    else
                    {
                        return (passwordChangeResult.Errors.FirstOrDefault().Description, "");
                    }
                }
                catch (Exception ex)
                {
                    return ("Error Encountered", ex.Message);
                }
            }                        
        }
        
        public ResponseDto UserProfileRegister(UserProfileRegistrationDto profileRegistrationDto)
        {
            try
            {   
                UserProfile obj = _mapper.Map<UserProfile>(profileRegistrationDto);
                _db.Tbl_User_Profile.Add(obj);
                _db.SaveChanges();
                _response.Result = _mapper.Map<UserProfileRegistrationDto>(obj);
                
            }
            catch(Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
                
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
        
        public async Task<ResponseDto> UpdateUserData(string userid, string displayName, string email, string phoneNumber)
        {
            var userToUpdate = await _userManager.FindByIdAsync(userid);
            if (userToUpdate != null)
            {
                try
                {
                    userToUpdate.Email = email;
                    userToUpdate.NormalizedEmail = email.ToUpper();
                    userToUpdate.PhoneNumber = phoneNumber;
                    var updateResult = await _userManager.UpdateAsync(userToUpdate);
                    if (updateResult.Succeeded)
                    {
                        var obj=_db.Tbl_User_Profile.First(i=>i.User_ID==userid);
                        obj.User_Agency = displayName;
                        _db.Tbl_User_Profile.Update(obj);
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
                _db.Tbl_User_LoginSessions.Add(obj);
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
            var session = _db.Tbl_User_LoginSessions.FirstOrDefault(u => u.SessionID == sessionId);
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
                _db.Tbl_User_LoginSessions.Update(obj);
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
            var expiredSession = _db.Tbl_User_LoginSessions.Where(x=> x.LogoutTime == null && x.LastActivity.Add(_timeout)<DateTime.Now).ToList();
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