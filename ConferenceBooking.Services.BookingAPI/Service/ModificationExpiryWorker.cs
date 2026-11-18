
using ConferenceBooking.Services.BookingAPI.Service.IService;

namespace ConferenceBooking.Services.BookingAPI.Service
{
    public class ModificationExpiryWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ModificationExpiryWorker> _logger;
        private readonly IConfiguration _configuration;

        public ModificationExpiryWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<ModificationExpiryWorker> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            var pollingSeconds = _configuration.GetValue<int?>(
                "Booking:ModificationExpiryPollSeconds") ?? 60;

            if (pollingSeconds < 10)
                pollingSeconds = 10;

            _logger.LogInformation(
                "Modification expiry worker started. Polling interval: {Seconds} seconds.",
                pollingSeconds);

            using var timer = new PeriodicTimer(
                TimeSpan.FromSeconds(pollingSeconds));

            // Run once immediately at startup.
            do
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var service = scope.ServiceProvider
                        .GetRequiredService<IBookingModificationService>();

                    var expiredCount = await service.ExpirePendingRequests(
                        stoppingToken);

                    if (expiredCount > 0)
                    {
                        _logger.LogInformation(
                            "Automatically expired {Count} booking modification requests.",
                            expiredCount);
                    }
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error occurred in booking modification expiry worker.");
                }

            } while (await timer.WaitForNextTickAsync(stoppingToken));

            _logger.LogInformation(
                "Modification expiry worker stopped.");
        }
    }
}
