using System;
using System.Collections.Generic;
using System.Text;

namespace Tellma.SMSAutomation.WinService
{
    public class SmsSchedule
    {
        public string Name { get; set; } = string.Empty;
        public string CRON { get; set; } = string.Empty;
        public List<string> Templates { get; set; } = new List<string>();
        public DateTime? NextRun { get; set; }
    }
}
