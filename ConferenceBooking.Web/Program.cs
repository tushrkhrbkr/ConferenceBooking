using ConferenceBooking.Web.Helpers;
using ConferenceBooking.Web.Service.IService;
using ConferenceBooking.Web.Utility;
using Microsoft.AspNetCore.Authentication.Cookies;
using ConferenceBooking.Web.Service;
using ConferenceBooking.Web.Utility;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IAuthService, AuthService>();

SD.AuthAPIBase = builder.Configuration["ServiceUrls:AuthAPI"];

builder.Services.AddScoped<IBaseService, BaseService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenProvider, TokenProvider>();


builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromMinutes(100);
    options.SlidingExpiration = true;
    //options.ExpireTimeSpan = TimeSpan.FromHours(10);
    options.LoginPath = "/AuthLogin/Login";
    options.AccessDeniedPath = "/AuthLogin/AccessDenied";
})
.AddNegotiate();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    //options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.IdleTimeout = TimeSpan.MaxValue;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.UseMiddleware<SessionTrackingMiddleware>(builder.Configuration.GetValue<int>("TimeOut:Minutes"));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=AuthLogin}/{action=Login}/{id?}");

app.Run();
