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
        private readonly SignInManager<ApplicationUser> _signinManager;
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

        public async Task<LoginResponseDto> Login(
     LoginRequestDto loginRequestDto)
        {
            if (loginRequestDto == null ||
                string.IsNullOrWhiteSpace(loginRequestDto.UserName) ||
                string.IsNullOrWhiteSpace(loginRequestDto.Password))
            {
                return new LoginResponseDto
                {
                    User = null,
                    Token = string.Empty
                };
            }

            try
            {
                var username =
                    loginRequestDto.UserName.Trim();

                // =========================================================
                // 1. Find Identity user
                // =========================================================

                var user =
                    await _userManager.FindByNameAsync(username);

                if (user == null)
                {
                    return new LoginResponseDto
                    {
                        User = null,
                        Token = string.Empty
                    };
                }

                // =========================================================
                // 2. Check lockout BEFORE password authentication
                // =========================================================

                if (await _userManager.IsLockedOutAsync(user))
                {
                    return new LoginResponseDto
                    {
                        User = null,
                        Token = "block"
                    };
                }

                // =========================================================
                // 3. Authenticate with lockoutOnFailure
                // =========================================================

                var signInResult =
                    await _signinManager.CheckPasswordSignInAsync(
                        user,
                        loginRequestDto.Password,
                        lockoutOnFailure: true);

                if (signInResult.IsLockedOut)
                {
                    return new LoginResponseDto
                    {
                        User = null,
                        Token = "block"
                    };
                }

                if (signInResult.IsNotAllowed)
                {
                    return new LoginResponseDto
                    {
                        User = null,
                        Token = string.Empty
                    };
                }

                if (!signInResult.Succeeded)
                {
                    return new LoginResponseDto
                    {
                        User = null,
                        Token = string.Empty
                    };
                }

                // =========================================================
                // 4. Find application UserProfile
                // =========================================================

                var userProfile =
                    await _db.Tbl_UserProfile
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x =>
                            x.UserId == user.Id);

                if (userProfile == null)
                {
                    return new LoginResponseDto
                    {
                        User = null,
                        Token = string.Empty
                    };
                }

                // =========================================================
                // 5. User must have been approved
                // =========================================================

                if (!userProfile.IsActive)
                {
                    // Identity account exists but Admin has not approved it.
                    return new LoginResponseDto
                    {
                        User = null,
                        Token = "pending"
                    };
                }

                // =========================================================
                // 6. Validate Department
                // =========================================================

                Department? department = null;

                if (userProfile.DepartmentId.HasValue)
                {
                    department =
                        await _db.Tbl_Department
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x =>
                                x.DepartmentId ==
                                    userProfile.DepartmentId.Value &&
                                x.IsActive);
                }

                if (department == null)
                {
                    return new LoginResponseDto
                    {
                        User = null,
                        Token = string.Empty
                    };
                }

                // =========================================================
                // 7. Get roles
                // =========================================================

                var roles =
                    await _userManager.GetRolesAsync(user);

                if (roles == null || roles.Count == 0)
                {
                    return new LoginResponseDto
                    {
                        User = null,
                        Token = string.Empty
                    };
                }

                // =========================================================
                // 8. Generate JWT
                // =========================================================

                var token =
                    _jwtTokenGenerator.GenerateToken(
                        user,
                        roles);

                // =========================================================
                // 9. Build response
                // =========================================================

                var userDto = new UserDto
                {
                    ID = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    DisplayName = userProfile.DisplayName
                };

                return new LoginResponseDto
                {
                    User = userDto,
                    Token = token
                };
            }
            catch
            {
                // Do not expose internal authentication/database
                // exceptions to the caller.
                return new LoginResponseDto
                {
                    User = null,
                    Token = string.Empty
                };
            }
        }

        public async Task<ResponseDto> RequestRegistration(RegistrationRequestDto newRequest)
        {
            var response = new ResponseDto();

            if (newRequest == null)
            {
                response.IsSuccess = false;
                response.Message = "Registration request is required.";
                return response;
            }

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            try
            {
                var username = newRequest.Username.Trim();
                var email = newRequest.Email.Trim().ToLowerInvariant();

                // ---------------------------------------------------------
                // 1. Validate duplicate Identity username
                // ---------------------------------------------------------
                var existingUserByUsername =
                    await _userManager.FindByNameAsync(username);

                if (existingUserByUsername != null)
                {
                    response.IsSuccess = false;
                    response.Message =
                        "An account with this username already exists.";

                    await transaction.RollbackAsync();
                    return response;
                }

                // ---------------------------------------------------------
                // 2. Validate duplicate Identity email
                // ---------------------------------------------------------
                var existingUserByEmail =
                    await _userManager.FindByEmailAsync(email);

                if (existingUserByEmail != null)
                {
                    response.IsSuccess = false;
                    response.Message =
                        "An account with this email address already exists.";

                    await transaction.RollbackAsync();
                    return response;
                }

                // ---------------------------------------------------------
                // 3. Prevent duplicate pending registration
                // ---------------------------------------------------------
                var existingRequest =
                    await _db.Tbl_RegistrationRequest
                        .FirstOrDefaultAsync(x =>
                            x.RequestedUserName == username &&
                            x.RequestStatus ==
                                Enums.RegistrationRequestStatus.Pending);

                if (existingRequest != null)
                {
                    response.IsSuccess = false;
                    response.Message =
                        "A registration request for this username is already pending.";

                    await transaction.RollbackAsync();
                    return response;
                }

                // ---------------------------------------------------------
                // 4. Validate requested department if supplied
                // ---------------------------------------------------------
                if (newRequest.RequestedDepartmentId.HasValue)
                {
                    var departmentExists =
                        await _db.Tbl_Department.AnyAsync(x =>
                            x.DepartmentId ==
                                newRequest.RequestedDepartmentId.Value &&
                            x.IsActive);

                    if (!departmentExists)
                    {
                        response.IsSuccess = false;
                        response.Message =
                            "The selected department is not active.";

                        await transaction.RollbackAsync();
                        return response;
                    }
                }

                // ---------------------------------------------------------
                // 5. CREATE ASP.NET IDENTITY USER NOW
                // ---------------------------------------------------------
                var user = new ApplicationUser
                {
                    UserName = username,
                    Email = email,
                    PhoneNumber = newRequest.MobileNumber.Trim(),
                    EmailConfirmed = false,
                    PhoneNumberConfirmed = false,
                    LockoutEnabled = true
                };

                var createUserResult =
                    await _userManager.CreateAsync(
                        user,
                        newRequest.Password);

                if (!createUserResult.Succeeded)
                {
                    response.IsSuccess = false;
                    response.Message =
                        string.Join(
                            "; ",
                            createUserResult.Errors.Select(x => x.Description));

                    await transaction.RollbackAsync();
                    return response;
                }

                // ---------------------------------------------------------
                // 6. CREATE USER PROFILE
                //    User exists, but is NOT active yet.
                // ---------------------------------------------------------
                var userProfile = new UserProfile
                {
                    UserId = user.Id,
                    Username = username,
                    DisplayName = newRequest.DisplayName.Trim(),
                    Designation = newRequest.Designation.Trim(),
                    EmailAddress = email,
                    MobileNumber = newRequest.MobileNumber.Trim(),
                    DeskPhone = newRequest.DeskPhone.Trim(),
                    DepartmentId = newRequest.RequestedDepartmentId,
                    IsActive = false,
                    CreatedAtUtc = DateTime.UtcNow
                };

                await _db.Tbl_UserProfile.AddAsync(userProfile);

                // ---------------------------------------------------------
                // 7. CREATE REGISTRATION REQUEST
                // ---------------------------------------------------------
                var registrationRequest = new RegistrationRequest
                {
                    RequestedUserName = username,
                    RequestedDisplayName =
                        newRequest.DisplayName.Trim(),

                    RequestedDesignation =
                        newRequest.Designation.Trim(),

                    RequestedEmail = email,

                    RequestedMobile =
                        newRequest.MobileNumber.Trim(),

                    RequestedDeskPhone =
                        newRequest.DeskPhone.Trim(),

                    RequestedDepartmentId =
                        newRequest.RequestedDepartmentId,

                    RequestedRoleCode = null,

                    RequestStatus =
                        Enums.RegistrationRequestStatus.Pending,

                    SubmittedAtUtc = DateTime.UtcNow
                };

                await _db.Tbl_RegistrationRequest
                    .AddAsync(registrationRequest);

                // ---------------------------------------------------------
                // 8. SAVE EVERYTHING
                // ---------------------------------------------------------
                await _db.SaveChangesAsync();

                // ---------------------------------------------------------
                // 9. COMMIT
                // ---------------------------------------------------------
                await transaction.CommitAsync();

                response.IsSuccess = true;
                response.Message =
                    "Registration request submitted successfully.";

                response.Result =
                    new
                    {
                        RegistrationRequestId =
                            registrationRequest.RegistrationRequestId,

                        UserId = user.Id,

                        UserName = user.UserName,

                        Status =
                            Enums.RegistrationRequestStatus.Pending
                    };

                return response;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                response.IsSuccess = false;
                response.Message =
                    "Registration could not be completed." + ex.Message;

                return response;
            }
        }

        public async Task<ResponseDto> ApproveRegistration(
       ApproveRegistrationDto approveRequest)
        {
            var response = new ResponseDto();

            if (approveRequest == null)
            {
                response.IsSuccess = false;
                response.Message =
                    "Approval request is required.";

                return response;
            }

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            try
            {
                // ---------------------------------------------------------
                // 1. Load registration request
                // ---------------------------------------------------------
                var existingRequest =
                    await _db.Tbl_RegistrationRequest
                        .FirstOrDefaultAsync(x =>
                            x.RegistrationRequestId ==
                                approveRequest.RegistrationRequestId);

                if (existingRequest == null)
                {
                    response.IsSuccess = false;
                    response.Message =
                        "Registration request not found.";

                    await transaction.RollbackAsync();
                    return response;
                }

                // ---------------------------------------------------------
                // 2. Must still be Pending
                // ---------------------------------------------------------
                if (existingRequest.RequestStatus !=
                    Enums.RegistrationRequestStatus.Pending)
                {
                    response.IsSuccess = false;
                    response.Message =
                        "This registration request has already been processed.";

                    await transaction.RollbackAsync();
                    return response;
                }

                // ---------------------------------------------------------
                // 3. Validate department
                // ---------------------------------------------------------
                var department =
                    await _db.Tbl_Department
                        .FirstOrDefaultAsync(x =>
                            x.DepartmentId ==
                                approveRequest.DepartmentId &&
                            x.IsActive);

                if (department == null)
                {
                    response.IsSuccess = false;
                    response.Message =
                        "The selected department is not active.";

                    await transaction.RollbackAsync();
                    return response;
                }

                // ---------------------------------------------------------
                // 4. Validate role
                // ---------------------------------------------------------
                var allowedRoles = new[]
                {
            "Admin",
            "SubAdmin",
            "HallManager",
            "LocalUser",
            "RemoteUser"
        };

                var roleName =
                    approveRequest.RoleName.Trim();

                if (!allowedRoles.Contains(
                        roleName,
                        StringComparer.OrdinalIgnoreCase))
                {
                    response.IsSuccess = false;
                    response.Message =
                        "Invalid role selected.";

                    await transaction.RollbackAsync();
                    return response;
                }

                // ---------------------------------------------------------
                // 5. Find Identity user created during registration
                // ---------------------------------------------------------
                var user =
                    await _userManager.FindByNameAsync(
                        existingRequest.RequestedUserName);

                if (user == null)
                {
                    response.IsSuccess = false;
                    response.Message =
                        "The Identity account associated with this request was not found.";

                    await transaction.RollbackAsync();
                    return response;
                }

                // ---------------------------------------------------------
                // 6. Find UserProfile
                // ---------------------------------------------------------
                var userProfile =
                    await _db.Tbl_UserProfile
                        .FirstOrDefaultAsync(x =>
                            x.UserId == user.Id);

                if (userProfile == null)
                {
                    response.IsSuccess = false;
                    response.Message =
                        "The user profile associated with this request was not found.";

                    await transaction.RollbackAsync();
                    return response;
                }

                // ---------------------------------------------------------
                // 7. Verify profile is still pending
                // ---------------------------------------------------------
                if (userProfile.IsActive)
                {
                    response.IsSuccess = false;
                    response.Message =
                        "The user profile is already active.";

                    await transaction.RollbackAsync();
                    return response;
                }

                // ---------------------------------------------------------
                // 8. Create role if it doesn't exist
                // ---------------------------------------------------------
                var role =
                    await _roleManager.FindByNameAsync(roleName);

                if (role == null)
                {
                    var createRoleResult =
                        await _roleManager.CreateAsync(
                            new IdentityRole(roleName));

                    if (!createRoleResult.Succeeded)
                    {
                        response.IsSuccess = false;
                        response.Message =
                            string.Join(
                                "; ",
                                createRoleResult.Errors
                                    .Select(x => x.Description));

                        await transaction.RollbackAsync();
                        return response;
                    }

                    role =
                        await _roleManager.FindByNameAsync(
                            roleName);

                    if (role == null)
                    {
                        throw new InvalidOperationException(
                            "Role creation succeeded but the role could not be retrieved.");
                    }
                }

                // ---------------------------------------------------------
                // 9. Remove any existing roles
                //    Normally there should be none for a pending user.
                // ---------------------------------------------------------
                var existingRoles =
                    await _userManager.GetRolesAsync(user);

                if (existingRoles.Count > 0)
                {
                    var removeRolesResult =
                        await _userManager.RemoveFromRolesAsync(
                            user,
                            existingRoles);

                    if (!removeRolesResult.Succeeded)
                    {
                        response.IsSuccess = false;
                        response.Message =
                            string.Join(
                                "; ",
                                removeRolesResult.Errors
                                    .Select(x => x.Description));

                        await transaction.RollbackAsync();
                        return response;
                    }
                }

                // ---------------------------------------------------------
                // 10. Assign selected role
                // ---------------------------------------------------------
                var addRoleResult =
                    await _userManager.AddToRoleAsync(
                        user,
                        roleName);

                if (!addRoleResult.Succeeded)
                {
                    response.IsSuccess = false;
                    response.Message =
                        string.Join(
                            "; ",
                            addRoleResult.Errors
                                .Select(x => x.Description));

                    await transaction.RollbackAsync();
                    return response;
                }

                // ---------------------------------------------------------
                // 11. Activate UserProfile
                // ---------------------------------------------------------
                userProfile.DepartmentId =
                    approveRequest.DepartmentId;

                userProfile.IsActive = true;
                userProfile.UpdatedAtUtc = DateTime.UtcNow;

                _db.Tbl_UserProfile.Update(userProfile);

                // ---------------------------------------------------------
                // 12. Update registration request
                // ---------------------------------------------------------
                existingRequest.RequestedDepartmentId =
                    approveRequest.DepartmentId;

                existingRequest.RequestedRoleCode =
                    roleName;

                existingRequest.RequestStatus =
                    Enums.RegistrationRequestStatus.Approved;

                existingRequest.ReviewedByUserId =
                    approveRequest.ReviewedByUserId;

                existingRequest.ReviewedAtUtc =
                    DateTime.UtcNow;

                existingRequest.ReviewRemarks =
                    approveRequest.Remarks;

                _db.Tbl_RegistrationRequest
                    .Update(existingRequest);

                // ---------------------------------------------------------
                // 13. Save all changes
                // ---------------------------------------------------------
                await _db.SaveChangesAsync();

                // ---------------------------------------------------------
                // 14. COMMIT
                // ---------------------------------------------------------
                await transaction.CommitAsync();

                response.IsSuccess = true;
                response.Message =
                    "Registration approved successfully.";

                response.Result =
                    new
                    {
                        UserId = user.Id,
                        UserName = user.UserName,
                        DisplayName = userProfile.DisplayName,
                        DepartmentId =
                            userProfile.DepartmentId,
                        DepartmentName =
                            department.DepartmentName,
                        Role = roleName
                    };

                return response;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                response.IsSuccess = false;
                response.Message =
                    "Registration approval failed." + ex.Message;

                return response;
            }
        }

        public async Task<ResponseDto> RejectRegistration(
    RejectRegistrationDto rejectRequest)
        {
            var response = new ResponseDto();

            if (rejectRequest == null)
            {
                response.IsSuccess = false;
                response.Message =
                    "Rejection request is required.";

                return response;
            }

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            try
            {
                // ---------------------------------------------------------
                // 1. Find registration request
                // ---------------------------------------------------------
                var existingRequest =
                    await _db.Tbl_RegistrationRequest
                        .FirstOrDefaultAsync(x =>
                            x.RegistrationRequestId ==
                                rejectRequest.RegistrationRequestId);

                if (existingRequest == null)
                {
                    response.IsSuccess = false;
                    response.Message =
                        "Registration request not found.";

                    await transaction.RollbackAsync();
                    return response;
                }

                // ---------------------------------------------------------
                // 2. Only Pending request can be rejected
                // ---------------------------------------------------------
                if (existingRequest.RequestStatus !=
                    Enums.RegistrationRequestStatus.Pending)
                {
                    response.IsSuccess = false;
                    response.Message =
                        "This registration request has already been processed.";

                    await transaction.RollbackAsync();
                    return response;
                }

                // ---------------------------------------------------------
                // 3. Find Identity user
                // ---------------------------------------------------------
                var user =
                    await _userManager.FindByNameAsync(
                        existingRequest.RequestedUserName);

                if (user != null)
                {
                    var userId = user.Id;


                    // -----------------------------------------------------
                    // 5. Delete UserProfile
                    // -----------------------------------------------------
                    var userProfile =
                        await _db.Tbl_UserProfile
                            .FirstOrDefaultAsync(x =>
                                x.UserId == userId);

                    if (userProfile != null)
                    {
                        _db.Tbl_UserProfile
                            .Remove(userProfile);
                    }


                    await _userManager.DeleteAsync(user);
                }

                // ---------------------------------------------------------
                // 7. KEEP RegistrationRequest for audit/history
                // ---------------------------------------------------------
                existingRequest.RequestStatus =
                    Enums.RegistrationRequestStatus.Rejected;

                existingRequest.ReviewedByUserId =
                    rejectRequest.ReviewedByUserId;

                existingRequest.ReviewedAtUtc =
                    DateTime.UtcNow;

                existingRequest.ReviewRemarks =
                    rejectRequest.Remarks;

                _db.Tbl_RegistrationRequest
                    .Update(existingRequest);

                // ---------------------------------------------------------
                // 8. Save EVERYTHING
                // ---------------------------------------------------------
                await _db.SaveChangesAsync();

                // ---------------------------------------------------------
                // 9. COMMIT
                // ---------------------------------------------------------
                await transaction.CommitAsync();

                response.IsSuccess = true;
                response.Message =
                    "Registration rejected successfully.";

                response.Result =
                    new
                    {
                        RegistrationRequestId =
                            existingRequest.RegistrationRequestId,

                        Status =
                            Enums.RegistrationRequestStatus.Rejected
                    };

                return response;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                response.IsSuccess = false;
                response.Message =
                    "Registration rejection failed." + ex.Message;



                return response;
            }
        }
        public async Task<bool> AssignRole(string username, string roleName)
        {
            var user = _db.ApplicationUsers.FirstOrDefault(u => u.UserName.ToLower() == username.ToLower());
            if (user != null)
            {
                var role = await _roleManager.FindByNameAsync(roleName);

                if (role == null)
                {
                    var roleResult =
                        await _roleManager.CreateAsync(
                            new IdentityRole(roleName));

                    if (!roleResult.Succeeded)
                    {
                        throw new InvalidOperationException();
                    }

                    role = await _roleManager.FindByNameAsync(roleName);
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
                _response.IsSuccess = false;
                _response.Message = "No user found!!!";
            }
            return _response;
        }

        public async Task<ResponseDto> ChangePassword(PasswordChangeDto model)
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
                        var obj = _db.Tbl_UserProfile.First(i => i.UserId == userid);
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
            var expiredSession = _db.Tbl_UserLoginSessions.Where(x => x.LogoutTime == null && x.LastActivity.Add(_timeout) < DateTime.Now).ToList();
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