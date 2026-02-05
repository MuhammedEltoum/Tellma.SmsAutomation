using System;
using System.Collections.Generic;
using System.Text;

namespace Tellma.SMSAutomation.Contract
{
    public class SmsResponse
    {
        public bool? sent { get; set; } = false;
        public int? id { get; set; } = 0;
        public string? description { get; set; } = "Failed to send";
    }
}
