using System;
using System.Collections.Generic;
using System.Text;
using Tellma.SMSAutomation.Contract;

namespace Tellma.SmsAutomation.Contract
{
    public interface ISmsService
    {
        public string SmsProvider { get; }
        Task SendMessageAsync(SmsRequest smsRequest, CancellationToken token);
    }
}
