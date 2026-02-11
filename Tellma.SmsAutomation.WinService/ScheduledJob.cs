// ScheduledJob.cs
using Cronos;

namespace Tellma.SMSAutomation.WinService
{
    public partial class SmsScheduler
    {
        public class ScheduledJob
        {
            public string Name { get; set; } = string.Empty;
            public CronExpression CronExpression { get; set; } = null!;
            public List<string> Templates { get; set; } = new List<string>();
            public DateTime? NextRun { get; set; }
        }
    }
}