using AutoMapper;
using ConferenceBooking.Services.AuthAPI.Data;
using System.Diagnostics.CodeAnalysis;
using ConferenceBooking.Services.AuthAPI.Models;

namespace ConferenceBooking.Services.AuthAPI.Service
{
    public class SessionTimeoutService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _timeout;
        private readonly int _timeoutMinutes;
        private readonly IMapper _mapper;
        private readonly IConfiguration _configuration;

        public SessionTimeoutService(IServiceScopeFactory scopeFactory, IMapper mapper, IConfiguration configuration)
        {
            _mapper = mapper;
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _timeoutMinutes = Convert.ToInt32(_configuration["TimeOut:Minutes"]);
            _timeout = TimeSpan.FromMinutes(_timeoutMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var _db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var now = DateTime.Now;
                    var cutoff = now - _timeout;
                    var expiredSession = _db.Tbl_UserLoginSessions.Where(x => x.LogoutTime == null && x.LastActivity < cutoff).ToList();
                    if (expiredSession.Count != 0)
                    {
                        foreach (var item in expiredSession)
                        {
                            if (item != null)
                            {
                                item.LogoutTime = DateTime.Now;
                                item.LogoutReason = "SessionTimeout";
                                UserLoginSessions obj = _mapper.Map<UserLoginSessions>(item);
                                _db.Tbl_UserLoginSessions.Update(obj);
                                _db.SaveChanges();
                            }
                        }
                    }
                }
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
