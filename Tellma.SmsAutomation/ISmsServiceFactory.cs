using System;
using System.Collections.Generic;
using System.Text;
using Tellma.SmsAutomation.Contract;

namespace Tellma.SMSAutomation
{
    public interface ISmsServiceFactory
    {
        ISmsService Create(string serviceProvier);
    }
}
