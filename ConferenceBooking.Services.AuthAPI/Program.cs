using AutoMapper;
using ConferenceBooking.Services.AuthAPI;
using ConferenceBooking.Services.AuthAPI.Data;
using ConferenceBooking.Services.AuthAPI.Models;
using ConferenceBooking.Services.AuthAPI.Service;
using ConferenceBooking.Services.AuthAPI.Service.IService;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(Options => Options.UseSqlServer(
builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("ApiSettings:JwtOptions"));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;

    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;

    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan =
        TimeSpan.FromMinutes(15);
}
).AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IPasswordResetTokenService, PasswordResetTokenService>();
builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();
builder.Services.Configure<EmailNotificationDispatcherOptions>(
    builder.Configuration.GetSection("EmailNotificationDispatcher"));

builder.Services.AddHttpClient<
    IEmailNotificationDispatcher,
    EmailNotificationDispatcher>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(60);
    });

builder.Services.Configure<EmailNotificationWorkerOptions>(
    builder.Configuration.GetSection("EmailNotificationWorker"));

builder.Services.AddHostedService<EmailNotificationWorker>();

builder.Services.AddHostedService<SessionTimeoutService>();
builder.Services.AddControllers();
IMapper mapper = MappingConfig.RegisterMaps().CreateMapper();
builder.Services.AddSingleton(mapper);
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


if (app.Environment.IsDevelopment() &&
    app.Configuration.GetValue<bool>("BootstrapAdmin:Enabled"))
{
    using var scope = app.Services.CreateScope();

    var services = scope.ServiceProvider;
    var userManager =
        services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager =
        services.GetRequiredService<RoleManager<IdentityRole>>();
    var db =
        services.GetRequiredService<ApplicationDbContext>();
    var configuration = services.GetRequiredService<IConfiguration>();

    var username = configuration["BootstrapAdmin:UserName"];
    var email = configuration["BootstrapAdmin:Email"];
    var password = configuration["BootstrapAdmin:Password"];
    var displayName =
        configuration["BootstrapAdmin:DisplayName"]
        ?? "CONVENE Administrator";
    var designation =
        configuration["BootstrapAdmin:Designation"]
        ?? "System Administrator";
    var departmentId =
        configuration.GetValue<int>("BootstrapAdmin:DepartmentId");

    if (string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrWhiteSpace(email) ||
        string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException(
            "Bootstrap Admin configuration is incomplete.");
    }

    const string adminRole = "Admin";

    // 1. Ensure the Admin role exists.
    if (!await roleManager.RoleExistsAsync(adminRole))
    {
        var roleResult = await roleManager.CreateAsync(
            new IdentityRole(adminRole));

        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Could not create Admin role: " +
                string.Join("; ",
                    roleResult.Errors.Select(e => e.Code)));
        }
    }

    // 2. Find or create the Identity user.
    var user = await userManager.FindByNameAsync(username);

    if (user == null)
    {
        var emailOwner = await userManager.FindByEmailAsync(email);

        if (emailOwner != null)
        {
            throw new InvalidOperationException(
                "The bootstrap email already belongs to another user.");
        }

        user = new ApplicationUser
        {
            UserName = username,
            Email = email,
            EmailConfirmed = true
        };

        var createResult =
            await userManager.CreateAsync(user, password);

        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Could not create bootstrap Admin: " +
                string.Join("; ",
                    createResult.Errors.Select(e => e.Code)));
        }
    }
    else if (!string.Equals(
        user.Email, email,
        StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "The existing bootstrap username has a different email.");
    }

    // 3. Assign the Admin role.
    if (!await userManager.IsInRoleAsync(user, adminRole))
    {
        var roleResult =
            await userManager.AddToRoleAsync(user, adminRole);

        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Could not assign Admin role: " +
                string.Join("; ",
                    roleResult.Errors.Select(e => e.Code)));
        }
    }

    // 4. Validate the configured department.
    var department = await db.Tbl_Department
        .FirstOrDefaultAsync(d =>
            d.DepartmentId == departmentId && d.IsActive);

    if (department == null)
    {
        throw new InvalidOperationException(
            "Bootstrap DepartmentId must identify an active department.");
    }

    // 5. Create or activate the application profile.
    var profile = await db.Tbl_UserProfile
        .FirstOrDefaultAsync(p => p.UserId == user.Id);

    if (profile == null)
    {
        profile = new UserProfile
        {
            UserId = user.Id,
            Username = user.UserName!,
            DisplayName = displayName,
            Designation = designation,
            EmailAddress = email,
            MobileNumber = string.Empty,
            DeskPhone = string.Empty,
            DepartmentId = department.DepartmentId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Tbl_UserProfile.Add(profile);
    }
    else
    {
        profile.IsActive = true;
        profile.DepartmentId = department.DepartmentId;
        profile.UpdatedAtUtc = DateTime.UtcNow;
    }

    await db.SaveChangesAsync();
}


app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
