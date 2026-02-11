using Tellma.SmsAutomation;
using Tellma.SMSAutomation.WinService;

public class Worker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<Worker> _logger;
    private readonly SmsScheduler _scheduler;

    public Worker(
        IServiceProvider serviceProvider,
        ILogger<Worker> logger,
        SmsScheduler scheduler)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _scheduler = scheduler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Remove this to run scheduler normally.
        //using var scope = _serviceProvider.CreateScope();
        //var importer = scope.ServiceProvider.GetRequiredService<TellmaSmsAutomation>();
        //await importer.RunSmsAutomation(stoppingToken);
        //return;

        _logger.LogInformation("SMS Automation Worker started");

        // Create a periodic timer that we'll reset based on job schedules
        using var timer = new PeriodicTimer(Timeout.InfiniteTimeSpan);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Execute any due jobs immediately
                await ExecuteDueJobsAsync(stoppingToken);

                // Calculate next wait period
                var delay = CalculateDelayToNextJob();
                _logger.LogDebug("Next check in: {Delay:hh\\:mm\\:ss}", delay);

                // Set the timer to wake us up at the right time
                timer.Period = delay;

                // Wait for either the timer or cancellation
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SMS automation loop");
                // Wait a bit before retrying
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("SMS Automation Worker stopped");
    }

    private async Task ExecuteDueJobsAsync(CancellationToken token)
    {
        var dueJobs = _scheduler.GetDueJobs().ToList();

        if (!dueJobs.Any())
        {
            return;
        }

        _logger.LogInformation("Executing {Count} due job(s)", dueJobs.Count);

        foreach (var job in dueJobs)
        {
            _logger.LogInformation("Executing job: {JobName}", job.Name);

            using var scope = _serviceProvider.CreateScope();
            var automation = scope.ServiceProvider.GetRequiredService<TellmaSmsAutomation>();

            await automation.RunSmsAutomation(job.Templates, token);

            _logger.LogInformation("Completed job: {JobName}", job.Name);
        }
    }

    private TimeSpan CalculateDelayToNextJob()
    {
        var now = DateTime.UtcNow;
        var minDelay = TimeSpan.FromHours(1); // Default fallback

        foreach (var job in _scheduler.GetAllJobs())
        {
            if (job.NextRun.HasValue)
            {
                var delay = job.NextRun.Value - now;

                // If job is due now or overdue, return immediately
                if (delay <= TimeSpan.Zero)
                {
                    return TimeSpan.Zero;
                }

                // Find minimum positive delay
                if (delay < minDelay)
                {
                    minDelay = delay;
                }
            }
        }

        // Add small buffer and cap maximum wait
        var buffer = TimeSpan.FromMilliseconds(100);
        var maxWait = TimeSpan.FromDays(1);

        return TimeSpan.FromTicks(Math.Min(minDelay.Ticks + buffer.Ticks, maxWait.Ticks));
    }
}