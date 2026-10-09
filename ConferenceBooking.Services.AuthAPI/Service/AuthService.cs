using AutoMapper;
using Azure;
using Azure.Core;
using ConferenceBooking.Services.AuthAPI.Data;
using ConferenceBooking.Services.AuthAPI.Enums;
using ConferenceBooking.Services.AuthAPI.Models;
using ConferenceBooking.Services.AuthAPI.Models.Dto;
using ConferenceBooking.Services.AuthAPI.Service.IService;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
        private readonly IOtpService _otpService;
        private readonly IPasswordResetTokenService _passwordResetTokenService;
        private readonly IEmailNotificationService _emailNotificationService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(ApplicationDbContext db,UserManager<ApplicationUser> userManager,SignInManager<ApplicationUser> signInManager,RoleManager<IdentityRole>roleManager, IJwtTokenGenerator jwtTokenGenerator, IMapper mapper, IOtpService otpService, IPasswordResetTokenService passwordResetTokenService, IEmailNotificationService emailNotificationService, ILogger<AuthService> logger)
        {
            _db = db;
            _response = new ResponseDto();
            _userManager = userManager;
            _signinManager = signInManager;
            _roleManager = roleManager;
            _jwtTokenGenerator = jwtTokenGenerator;
            _mapper = mapper;
            _otpService = otpService;
            _passwordResetTokenService = passwordResetTokenService;
            _emailNotificationService = emailNotificationService;
            _logger = logger;
        }

        public async Task<LoginResponseDto> Login(LoginRequestDto loginRequestDto)
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
            if (newRequest == null)
            {
                _response.IsSuccess = false;
                _response.Message = "Registration request is required.";
                return _response;
            }

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            try
            {
                var email = newRequest.Email.Trim().ToLowerInvariant();

                var atIndex = email.IndexOf('@');

                if (atIndex <= 0 || atIndex == email.Length - 1)
                {
                    _response.IsSuccess = false;
                    _response.Message = "A valid email address is required.";
                    return _response;
                }

                // Username is ALWAYS derived from email.
                var username = email[..atIndex];

                // ---------------------------------------------------------
                // 1. Validate duplicate Identity username
                // ---------------------------------------------------------
                var existingUserByUsername =
                    await _userManager.FindByNameAsync(username);

                if (existingUserByUsername != null)
                {
                    _response.IsSuccess = false;
                    _response.Message =
                        "An account with this username already exists.";

                    await transaction.RollbackAsync();
                    return _response;
                }

                // ---------------------------------------------------------
                // 2. Validate duplicate Identity email
                // ---------------------------------------------------------
                var existingUserByEmail =
                    await _userManager.FindByEmailAsync(email);

                if (existingUserByEmail != null)
                {
                    _response.IsSuccess = false;
                    _response.Message =
                        "An account with this email address already exists.";

                    await transaction.RollbackAsync();
                    return _response;
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
                    _response.IsSuccess = false;
                    _response.Message =
                        "A registration request for this username is already pending.";

                    await transaction.RollbackAsync();
                    return _response;
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
                        _response.IsSuccess = false;
                        _response.Message =
                            "The selected department is not active.";

                        await transaction.RollbackAsync();
                        return _response;
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
                    _response.IsSuccess = false;
                    _response.Message =
                        string.Join(
                            "; ",
                            createUserResult.Errors.Select(x => x.Description));

                    await transaction.RollbackAsync();
                    return _response;
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

                _response.IsSuccess = true;
                _response.Message =
                    "Registration request submitted successfully.";

                _response.Result =
                    new
                    {
                        RegistrationRequestId =
                            registrationRequest.RegistrationRequestId,

                        UserId = user.Id,

                        UserName = user.UserName,

                        Status =
                            Enums.RegistrationRequestStatus.Pending
                    };

                return _response;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _response.IsSuccess = false;
                _response.Message =
                    "Registration could not be completed." + ex.Message;

                return _response;
            }
        }

        public async Task<ResponseDto> CheckRegistrationAsync(RegistrationDuplicateCheckDto newRequest)
        {
            try
            {
                var Result = new RegistrationDuplicateResultDto();

                if (newRequest == null || string.IsNullOrWhiteSpace(newRequest.Email))
                {
                    Result = new RegistrationDuplicateResultDto
                    {
                        Status = RegistrationDuplicateStatus.RegistrationBlocked,
                        CanRegister = false,
                        Message = "Email address is required."
                    };

                    _response.Result = Result;
                    _response.Message = Result.Message;
                    return _response;
                }

                var email = newRequest.Email.Trim().ToLowerInvariant();

                var atIndex = email.IndexOf('@');

                if (atIndex <= 0 || atIndex == email.Length - 1)
                {
                    Result = new RegistrationDuplicateResultDto
                    {
                        Status = RegistrationDuplicateStatus.RegistrationBlocked,
                        CanRegister = false,
                        Message = "A valid email address is required."
                    };
                    _response.Result = Result;
                    _response.Message = Result.Message;
                    return _response;
                }

                // Username is ALWAYS derived from email.
                var username = email[..atIndex];

                // ---------------------------------------------------------
                // 1. Check existing Identity user by username
                // ---------------------------------------------------------
                var userByUsername = await _userManager.FindByNameAsync(username);

                // ---------------------------------------------------------
                // 2. Check existing Identity user by email
                // ---------------------------------------------------------
                var userByEmail = await _userManager.FindByEmailAsync(email);

                // Either username or email identifies an existing account.
                var existingUser = userByUsername ?? userByEmail;

                // ---------------------------------------------------------
                // 3. Check pending registration request
                // ---------------------------------------------------------
                var pendingRequest =
                    await _db.Tbl_RegistrationRequest
                        .AsNoTracking()
                        .Where(x =>
                            x.RequestStatus ==
                                RegistrationRequestStatus.Pending &&
                            (
                                x.RequestedUserName == username ||
                                x.RequestedEmail == email
                            ))
                        .OrderByDescending(x => x.SubmittedAtUtc)
                        .FirstOrDefaultAsync();

                // ---------------------------------------------------------
                // 4. Existing Identity account
                // ---------------------------------------------------------
                if (existingUser != null)
                {
                    var profile =
                        await _db.Tbl_UserProfile
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x =>
                                x.UserId == existingUser.Id);

                    // -----------------------------------------------------
                    // Existing account is active
                    // -----------------------------------------------------
                    if (profile != null && profile.IsActive)
                    {
                        Result = new RegistrationDuplicateResultDto
                        {
                            Status = RegistrationDuplicateStatus.AccountExists,

                            UserName = existingUser.UserName ?? username,

                            CanRegister = false,

                            RequiresPasswordReset = true,

                            Message = "An account with this email address already exists. " +
                                      "Please use the Forgot Password option."
                        };
                        _response.Result = Result;
                        _response.Message = Result.Message;
                        return _response;
                    }

                    // -----------------------------------------------------
                    // Existing account + pending request
                    // -----------------------------------------------------
                    if (pendingRequest != null)
                    {
                        Result = new RegistrationDuplicateResultDto
                        {
                            Status = RegistrationDuplicateStatus.RegistrationPending,

                            UserName = username,

                            CanRegister = false,

                            RequiresPasswordReset = false,

                            RegistrationRequestId = pendingRequest.RegistrationRequestId,

                            Message = "A registration request for this account is already pending."
                        };
                        _response.Result = Result;
                        _response.Message = Result.Message;
                        return _response;
                    }


                    // -----------------------------------------------------
                    // Identity user exists but profile/request state
                    // is inconsistent.
                    // Do NOT allow another account to be created.
                    // -----------------------------------------------------

                    Result = new RegistrationDuplicateResultDto
                    {
                        Status = RegistrationDuplicateStatus.RegistrationBlocked,

                        UserName = username,

                        CanRegister = false,

                        RequiresPasswordReset = false,

                        Message =
                            "An existing account record was found, but its " +
                            "registration state could not be determined. " +
                            "Please contact the Administrator."
                    };
                    _response.Result = Result;
                    _response.Message = Result.Message;
                    return _response;

                }
                // ---------------------------------------------------------
                // 5. No Identity user, but pending request exists.
                // This protects against partially inconsistent data.
                // ---------------------------------------------------------
                if (pendingRequest != null)
                {
                    Result = new RegistrationDuplicateResultDto
                    {
                        Status = RegistrationDuplicateStatus.RegistrationPending,

                        UserName = username,

                        CanRegister = false,

                        RequiresPasswordReset = false,

                        RegistrationRequestId = pendingRequest.RegistrationRequestId,

                        Message = "A registration request for this email address is already pending."
                    };
                    _response.Result = Result;
                    _response.Message = Result.Message;
                    return _response;
                }


                // ---------------------------------------------------------
                // 6. No duplicate
                // ---------------------------------------------------------
                Result = new RegistrationDuplicateResultDto
                {
                    Status = RegistrationDuplicateStatus.None,

                    UserName = username,

                    CanRegister = true,

                    RequiresPasswordReset = false,

                    Message = "Registration is available."
                };
                _response.Result = Result;
                _response.Message = Result.Message;
                return _response;


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
            if (approveRequest == null)
            {
                _response.IsSuccess = false;
                _response.Message =
                    "Approval request is required.";

                return _response;
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
                    _response.IsSuccess = false;
                    _response.Message =
                        "Registration request not found.";

                    await transaction.RollbackAsync();
                    return _response;
                }

                // ---------------------------------------------------------
                // 2. Must still be Pending
                // ---------------------------------------------------------
                if (existingRequest.RequestStatus !=
                    Enums.RegistrationRequestStatus.Pending)
                {
                    _response.IsSuccess = false;
                    _response.Message =
                        "This registration request has already been processed.";

                    await transaction.RollbackAsync();
                    return _response;
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
                    _response.IsSuccess = false;
                    _response.Message =
                        "The selected department is not active.";

                    await transaction.RollbackAsync();
                    return _response;
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
                    _response.IsSuccess = false;
                    _response.Message =
                        "Invalid role selected.";

                    await transaction.RollbackAsync();
                    return _response;
                }

                // ---------------------------------------------------------
                // 5. Find Identity user created during registration
                // ---------------------------------------------------------
                var user =
                    await _userManager.FindByNameAsync(
                        existingRequest.RequestedUserName);

                if (user == null)
                {
                    _response.IsSuccess = false;
                    _response.Message =
                        "The Identity account associated with this request was not found.";

                    await transaction.RollbackAsync();
                    return _response;
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
                    _response.IsSuccess = false;
                    _response.Message =
                        "The user profile associated with this request was not found.";

                    await transaction.RollbackAsync();
                    return _response;
                }

                // ---------------------------------------------------------
                // 7. Verify profile is still pending
                // ---------------------------------------------------------
                if (userProfile.IsActive)
                {
                    _response.IsSuccess = false;
                    _response.Message =
                        "The user profile is already active.";

                    await transaction.RollbackAsync();
                    return _response;
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
                        _response.IsSuccess = false;
                        _response.Message =
                            string.Join(
                                "; ",
                                createRoleResult.Errors
                                    .Select(x => x.Description));

                        await transaction.RollbackAsync();
                        return _response;
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
                        _response.IsSuccess = false;
                        _response.Message =
                            string.Join(
                                "; ",
                                removeRolesResult.Errors
                                    .Select(x => x.Description));

                        await transaction.RollbackAsync();
                        return _response;
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
                    _response.IsSuccess = false;
                    _response.Message =
                        string.Join(
                            "; ",
                            addRoleResult.Errors
                                .Select(x => x.Description));

                    await transaction.RollbackAsync();
                    return _response;
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

                _response.IsSuccess = true;
                _response.Message =
                    "Registration approved successfully.";

                _response.Result =
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

                return _response;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _response.IsSuccess = false;
                _response.Message =
                    "Registration approval failed." + ex.Message;

                return _response;
            }
        }

        public async Task<ResponseDto> RejectRegistration(RejectRegistrationDto rejectRequest)
        {
            if (rejectRequest == null)
            {
                _response.IsSuccess = false;
                _response.Message =
                    "Rejection request is required.";

                return _response;
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
                    _response.IsSuccess = false;
                    _response.Message =
                        "Registration request not found.";

                    await transaction.RollbackAsync();
                    return _response;
                }

                // ---------------------------------------------------------
                // 2. Only Pending request can be rejected
                // ---------------------------------------------------------
                if (existingRequest.RequestStatus !=
                    Enums.RegistrationRequestStatus.Pending)
                {
                    _response.IsSuccess = false;
                    _response.Message =
                        "This registration request has already been processed.";

                    await transaction.RollbackAsync();
                    return _response;
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

                _response.IsSuccess = true;
                _response.Message =
                    "Registration rejected successfully.";

                _response.Result =
                    new
                    {
                        RegistrationRequestId =
                            existingRequest.RegistrationRequestId,

                        Status =
                            Enums.RegistrationRequestStatus.Rejected
                    };

                return _response;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _response.IsSuccess = false;
                _response.Message =
                    "Registration rejection failed." + ex.Message;



                return _response;
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

        public async Task<ResponseDto> ForgotPasswordAsync(PasswordResetRequestDto request)
        {
            const string genericMessage =
                "If an account exists for this email address, " +
                "an OTP has been sent.";

            // Always return a generic response to prevent account enumeration.
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Email))
            {
                _response.IsSuccess = true;
                _response.Message = genericMessage;
                _response.Result = null;

                return _response;
            }

            var email = request.Email.Trim().ToLowerInvariant();
            var atIndex = email.IndexOf('@');

            if (atIndex <= 0 || atIndex == email.Length - 1)
            {
                _response.IsSuccess = true;
                _response.Message = genericMessage;
                _response.Result = null;

                return _response;
            }

            // Find the Identity user.
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                _response.IsSuccess = true;
                _response.Message = genericMessage;
                _response.Result = null;

                return _response;
            }

            // Serialize concurrent OTP requests for the same user.
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            try
            {
                await AcquirePasswordResetLockAsync(user.Id);

                // Confirm that the application profile exists and is active.
                var profile = await _db.Tbl_UserProfile
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.UserId == user.Id &&
                        x.EmailAddress == email);

                if (profile == null || !profile.IsActive)
                {
                    await transaction.RollbackAsync();

                    _response.IsSuccess = true;
                    _response.Message = genericMessage;
                    _response.Result = null;

                    return _response;
                }

                var nowUtc = DateTime.UtcNow;

                // Rate limit: maximum five requests in the previous hour.
                var oneHourAgoUtc = nowUtc.AddHours(-1);

                var otpRequestCount = await _db.Tbl_PasswordReset
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.UserId == user.Id &&
                        x.CreatedAtUtc >= oneHourAgoUtc);

                if (otpRequestCount >= 5)
                {
                    await transaction.RollbackAsync();

                    _response.IsSuccess = true;
                    _response.Message = genericMessage;
                    _response.Result = null;

                    return _response;
                }

                // Resend cooldown: at least 60 seconds between requests.
                var cooldownFromUtc = nowUtc.AddSeconds(-60);

                var recentRequest = await _db.Tbl_PasswordReset
                    .AsNoTracking()
                    .Where(x =>
                        x.UserId == user.Id &&
                        x.CreatedAtUtc >= cooldownFromUtc)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .FirstOrDefaultAsync();

                if (recentRequest != null)
                {
                    await transaction.RollbackAsync();

                    _response.IsSuccess = true;
                    _response.Message = genericMessage;
                    _response.Result = null;

                    return _response;
                }

                // Generate the OTP and store only its hash in PasswordReset.
                var otp = _otpService.GenerateOtp();
                var otpHash = _otpService.HashOtp(otp);

                const int otpValidityMinutes = 5;

                var passwordReset = new PasswordReset
                {
                    UserId = user.Id,
                    EmailAddress = email,
                    OtpHash = otpHash,
                    OtpExpiresAtUtc =
                        nowUtc.AddMinutes(otpValidityMinutes),
                    OtpAttemptCount = 0,
                    OtpMaxAttempts = 5,
                    CreatedAtUtc = nowUtc,
                    RequestedIpAddress = request.IpAddress
                };

                await _db.Tbl_PasswordReset.AddAsync(passwordReset);

                // Queue the email in the SAME DbContext and transaction.
                // The OTP is included only in the protected outbox payload.
                var emailPayload = new PasswordResetOtpEmailPayloadDto
                {
                    Otp = otp,
                    ValidityMinutes = otpValidityMinutes
                };

                await _emailNotificationService.EnqueueAsync(
                    eventType: EmailEventTypes.PasswordResetOtp,
                    toEmail: email,
                    payload: emailPayload,
                    subject: "CONVENE - Password Reset OTP");

                // EnqueueAsync calls SaveChangesAsync using this DbContext.
                // Both PasswordReset and EmailNotificationOutbox are still
                // inside this transaction until CommitAsync succeeds.
                await transaction.CommitAsync();

                _response.IsSuccess = true;
                _response.Message = genericMessage;
                _response.Result = null;

                return _response;
            }
            catch
            {
                // If the reset record or outbox insert fails, do not commit
                // either operation.
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to roll back the password-reset transaction.");
                }

                // Do not return exception details, OTP, or payload to WebApp.
                _response.IsSuccess = true;
                _response.Message = genericMessage;
                _response.Result = null;

                return _response;
            }
        }

        public async Task<ResponseDto> VerifyPasswordResetOtpAsync(PasswordResetVerifyOtpDto request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Otp))
            {
                _response.IsSuccess = false;
                _response.Message =
                    "Email address and OTP are required.";

                return _response;
            }

            var email = request.Email
                .Trim()
                .ToLowerInvariant();

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                _response.IsSuccess = false;
                _response.Message = "Invalid or expired OTP.";

                return _response;
            }


            var otp = request.Otp.Trim();

            if (otp.Length != 6 ||
                !otp.All(char.IsDigit))
            {
                _response.IsSuccess = false;
                _response.Message =
                    "Invalid OTP.";

                return _response;
            }

            await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await AcquirePasswordResetLockAsync(user.Id);

            var resetRequest =
                await _db.Tbl_PasswordReset
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .FirstOrDefaultAsync(x =>
                        x.EmailAddress == email &&
                        x.ConsumedAtUtc == null);

            if (resetRequest == null)
            {
                await transaction.RollbackAsync();
                _response.IsSuccess = false;
                _response.Message =
                    "Invalid or expired OTP.";

                return _response;
            }

            

            if (resetRequest.OtpVerifiedAtUtc != null)
            {
                await transaction.RollbackAsync();
                _response.IsSuccess = false;
                _response.Message =
                    "OTP has already been verified.";

                return _response;
            }

            if (resetRequest.OtpExpiresAtUtc <= DateTime.UtcNow)
            {
                await transaction.RollbackAsync();
                _response.IsSuccess = false;
                _response.Message =
                    "OTP has expired.";

                return _response;
            }

            if (resetRequest.OtpAttemptCount >=
                resetRequest.OtpMaxAttempts)
            {
                await transaction.RollbackAsync();
                _response.IsSuccess = false;
                _response.Message =
                    "Maximum OTP attempts exceeded.";

                return _response;
            }

            // ---------------------------------------------------------
            // Increment attempt BEFORE comparison
            // ---------------------------------------------------------

            resetRequest.OtpAttemptCount++;

            var suppliedHash =
                _otpService.HashOtp(otp);

            if (!string.Equals(
                    suppliedHash,
                    resetRequest.OtpHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                await _db.SaveChangesAsync();

                await transaction.CommitAsync();

                _response.IsSuccess = false;
                _response.Message =
                    "Invalid or expired OTP.";

                return _response;
            }

            // ---------------------------------------------------------
            // OTP correct
            // ---------------------------------------------------------

            resetRequest.OtpVerifiedAtUtc =
                DateTime.UtcNow;

            resetRequest.VerifiedIpAddress =
                request.IpAddress;

            var resetToken =
                _passwordResetTokenService.GenerateToken();

            resetRequest.ResetTokenHash =
                _passwordResetTokenService
                    .HashToken(resetToken);

            resetRequest.ResetTokenExpiresAtUtc =
                DateTime.UtcNow.AddMinutes(10);

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            _response.IsSuccess = true;
            _response.Message =
                "OTP verified successfully.";

            _response.Result = new
            {
                ResetToken = resetToken
            };

            return _response;
        }

        public async Task<ResponseDto> ResetPasswordAsync(PasswordResetCompleteDto request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.ResetToken) ||
                string.IsNullOrWhiteSpace(request.NewPassword) ||
                string.IsNullOrWhiteSpace(request.ConfirmPassword))
            {
                _response.IsSuccess = false;
                _response.Message =
                    "All password reset fields are required.";

                return _response;
            }

            if (request.NewPassword !=
                request.ConfirmPassword)
            {
                _response.IsSuccess = false;
                _response.Message =
                    "Passwords do not match.";

                return _response;
            }

            var tokenHash =
                _passwordResetTokenService
                    .HashToken(request.ResetToken);

            await using var transaction =  await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var resetRequest =
                await _db.Tbl_PasswordReset
                    .FirstOrDefaultAsync(x =>
                        x.ResetTokenHash == tokenHash);

            if (resetRequest == null)
            {
                await transaction.RollbackAsync();

                _response.IsSuccess = false;
                _response.Message =
                    "Invalid or expired reset token.";

                return _response;
            }

            await AcquirePasswordResetLockAsync(
                resetRequest.UserId);

            // Re-read after acquiring the per-user lock.
            // This guarantees that a concurrent reset operation
            // cannot consume the token between the initial lookup
            // and the protected validation.
            resetRequest =
                await _db.Tbl_PasswordReset
                    .FirstOrDefaultAsync(x =>
                        x.ResetTokenHash == tokenHash);

            if (resetRequest == null ||
                resetRequest.ConsumedAtUtc != null)
            {
                await transaction.RollbackAsync();

                _response.IsSuccess = false;
                _response.Message =
                    "Invalid or expired reset token.";

                return _response;
            }

            if (resetRequest.OtpVerifiedAtUtc == null)
            {
                await transaction.RollbackAsync();
                _response.IsSuccess = false;
                _response.Message =
                    "OTP verification is required.";

                return _response;
            }

            if (resetRequest.ResetTokenExpiresAtUtc == null ||
                resetRequest.ResetTokenExpiresAtUtc <= DateTime.UtcNow)
            {
                await transaction.RollbackAsync();
                _response.IsSuccess = false;
                _response.Message =
                    "Invalid or expired reset token.";

                return _response;
            }

            var user =
                await _userManager.FindByIdAsync(
                    resetRequest.UserId);

            if (user == null)
            {
                await transaction.RollbackAsync();
                _response.IsSuccess = false;
                _response.Message =
                    "Password reset could not be completed.";

                return _response;
            }

            // ---------------------------------------------------------
            // Generate ASP.NET Identity password-reset token
            // ---------------------------------------------------------

            var identityResetToken =
                await _userManager.GeneratePasswordResetTokenAsync(user);


            // ---------------------------------------------------------
            // Reset Identity password atomically through Identity
            // ---------------------------------------------------------

            var resetPasswordResult =
                await _userManager.ResetPasswordAsync(
                    user,
                    identityResetToken,
                    request.NewPassword);

            if (!resetPasswordResult.Succeeded)
            {
                await transaction.RollbackAsync();
                _response.IsSuccess = false;

                _response.Message =
                    string.Join(
                        "; ",
                        resetPasswordResult.Errors
                            .Select(x => x.Description));

                return _response;
            }

            // ---------------------------------------------------------
            // Consume reset token
            // ---------------------------------------------------------

            resetRequest.ConsumedAtUtc =
                DateTime.UtcNow;

            resetRequest.ResetIpAddress =
                request.IpAddress;

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            _response.IsSuccess = true;
            _response.Message =
                "Password has been reset successfully.";

            _response.Result = null;

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

        private async Task AcquirePasswordResetLockAsync(string userId)
        {
            var resource =
                $"PasswordReset:{userId}";

            await _db.Database.ExecuteSqlRawAsync(
                "EXEC sp_getapplock " +
                "@Resource={0}, " +
                "@LockMode='Exclusive', " +
                "@LockOwner='Transaction', " +
                "@LockTimeout=5000",
                resource);
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