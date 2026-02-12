namespace Declarify.Services.Scheduling
{
    public sealed class ReminderSchedulerHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReminderSchedulerHostedService> _logger;

        // Run time: 08:00 South Africa time = 06:00 UTC
        private const int RunHourUtc = 6;
        private const int RunMinuteUtc = 0;

        public ReminderSchedulerHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<ReminderSchedulerHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReminderSchedulerHostedService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var delay = GetDelayUntilNextRunUtc();
                    _logger.LogInformation("Next reminder run in {Delay}.", delay);

                    await Task.Delay(delay, stoppingToken);

                    using var scope = _scopeFactory.CreateScope();
                    var tasks = scope.ServiceProvider.GetRequiredService<IFormTaskService>();

                    // ✅ Keep statuses accurate before reminders
                    await tasks.UpdateOverdueTasksAsync();

                    // ✅ FR 4.3.4 reminders (7 days before + on due date)
                    await tasks.SendRemindersAsync();

                    _logger.LogInformation("ReminderSchedulerHostedService completed reminder run.");
                }
                catch (TaskCanceledException)
                {
                    // normal shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ReminderSchedulerHostedService error during reminder run.");

                    // small backoff to avoid tight loop if something breaks
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }

            _logger.LogInformation("ReminderSchedulerHostedService stopped.");
        }

        private static TimeSpan GetDelayUntilNextRunUtc()
        {
            var nowUtc = DateTime.UtcNow;

            var nextRunUtc = new DateTime(
                nowUtc.Year,
                nowUtc.Month,
                nowUtc.Day,
                RunHourUtc,
                RunMinuteUtc,
                0,
                DateTimeKind.Utc);

            if (nextRunUtc <= nowUtc)
                nextRunUtc = nextRunUtc.AddDays(1);

            return nextRunUtc - nowUtc;
        }
    }
}
