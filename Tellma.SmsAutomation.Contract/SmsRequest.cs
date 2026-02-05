using System;
using System.Collections.Generic;
using System.Text;

namespace Tellma.SmsAutomation.Contract
{
    public class SmsRequest
    {
        public string PhoneNumber { get; set; } = String.Empty;
        public string Message { get; set; } = String.Empty;
    }
}
