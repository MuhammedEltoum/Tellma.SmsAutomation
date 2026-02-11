// SmsScheduler.cs
using Cronos;
using Microsoft.Extensions.Options;

namespace Tellma.SMSAutomation.WinService
{
    public partial class SmsScheduler
    {
        private readonly ILogger<SmsScheduler> _logger;
        private readonly List<ScheduledJob> _scheduledJobs;

        public SmsScheduler(ILogger<SmsScheduler> logger, IOptions<List<SmsSchedule>> schedulesConfig)
        {
            _logger = logger;
            _scheduledJobs = new List<ScheduledJob>();

            var schedules = schedulesConfig.Value ?? new List<SmsSchedule>();

            foreach (var schedule in schedules)
            {
                try
                {
                    var cronExpression = CronExpression.Parse(schedule.CRON);
                    _scheduledJobs.Add(new ScheduledJob
                    {
                        Name = schedule.Name,
                        CronExpression = cronExpression,
                        Templates = schedule.Templates,
                        NextRun = cronExpression.GetNextOccurrence(DateTime.UtcNow)
                    });

                    _logger.LogInformation("Registered schedule '{Name}' with CRON '{Cron}'",
                        schedule.Name, schedule.CRON);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse CRON expression '{Cron}' for schedule '{Name}'",
                        schedule.CRON, schedule.Name);
                }
            }
        }

        public IEnumerable<ScheduledJob> GetDueJobs()
        {
            var now = DateTime.UtcNow;
            var dueJobs = new List<ScheduledJob>();

            foreach (var job in _scheduledJobs)
            {
                if (job.NextRun.HasValue && now >= job.NextRun.Value)
                {
                    dueJobs.Add(job);
                    // Update next run time immediately
                    job.NextRun = job.CronExpression.GetNextOccurrence(DateTime.UtcNow);
                    _logger.LogDebug("Job '{Name}' is due, next run will be at {NextRun}",
                        job.Name, job.NextRun);
                }
            }

            return dueJobs;
        }

        // New method to get all jobs for delay calculation
        public IEnumerable<ScheduledJob> GetAllJobs()
        {
            return _scheduledJobs;
        }
    }
}