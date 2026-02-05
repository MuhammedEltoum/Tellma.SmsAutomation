using Tellma.SmsAutomation.Contract;
using Tellma.SmsAutomation.SmsEthiopia;

namespace Tellma.SMSAutomation.WinService
{
    public class SmsServiceFactory : ISmsServiceFactory
    {
        private readonly Dictionary<string, ISmsService> _smsServiceDictionary;
        public SmsServiceFactory(SmsETApiClient smsETApiClient)
        {
            _smsServiceDictionary = new Dictionary<string, ISmsService>(StringComparer.OrdinalIgnoreCase)
            {
                { smsETApiClient.SmsProvider, smsETApiClient }
            };
        }
        public ISmsService Create(string serviceProvier)
        {
            if (_smsServiceDictionary.TryGetValue(serviceProvier, out ISmsService? result))
                return result;
            else
                throw new ArgumentException($"Unsupported Sms provider {serviceProvier}");
        }
    }
}
