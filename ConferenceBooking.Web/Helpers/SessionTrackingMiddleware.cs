using Newtonsoft.Json;
using ConferenceBooking.Web.Service.IService;
using ConferenceBooking.Web.Models.AuthLogin;
using ConferenceBooking.Web.Models;

namespace ConferenceBooking.Web.Helpers
{
    public class SessionTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly int _timeoutMinutes;
        //private readonly TimeSpan _sessionIdleTimeout = TimeSpan.FromMinutes(1);
        public SessionTrackingMiddleware(RequestDelegate next, int timeoutMinutes)
        {
            _next = next;
            _timeoutMinutes = timeoutMinutes;
        }
        public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory)
        {
            using var scope = scopeFactory.CreateScope();

            var _authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

            if (context.Session != null)
            {
                var lastActivity = context.Session.GetString("LastActivity");
                var sessionIdStr = context.Session.GetString("SessionId");
                if (!string.IsNullOrEmpty(sessionIdStr) && Guid.TryParse(sessionIdStr, out var sessionId))
                {
                    UserLoginSessionsDto userLoginSessionsDto = new UserLoginSessionsDto();
                    ResponseDto sessionResponseDto = await _authService.GetUserSession(sessionId);
                    if (sessionResponseDto != null && sessionResponseDto.IsSuccess)
                    {
                        userLoginSessionsDto = JsonConvert.DeserializeObject<UserLoginSessionsDto>(Convert.ToString(sessionResponseDto.Result));
                    }
                    if (userLoginSessionsDto != null)
                    {
                        if (!string.IsNullOrEmpty(lastActivity))
                        {
                            var lastActivityTime = DateTime.Parse(lastActivity);
                            var currentTime = DateTime.Now;
                            if ((currentTime - lastActivityTime).TotalMinutes > _timeoutMinutes)
                            {
                                userLoginSessionsDto.LogoutTime = DateTime.Now;
                                userLoginSessionsDto.LogoutReason = "SessionTimeout";
                                await _authService.UpdateUserSession(userLoginSessionsDto);
                                context.Session.Clear();
                            }
                        }
                        var isAjaxRequest = context.Request.Headers["X-Is-AJAX"] == "true";
                        if (!isAjaxRequest)
                        {
                            context.Session.SetString("LastActivity", DateTime.Now.ToString());
                            userLoginSessionsDto.LastActivity = DateTime.Now;
                            await _authService.UpdateUserSession(userLoginSessionsDto);
                        }
                    }
                }
            }
            await _next(context);
        }
    }
}
