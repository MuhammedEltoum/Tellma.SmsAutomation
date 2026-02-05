using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using Tellma.SmsAutomation;
using Tellma.SmsAutomation.Contract;
using Tellma.SMSAutomation;

namespace Tellma.SmsAutomation
{
    public class TellmaSmsAutomation
    {
        private readonly ITellmaService _tellmaService;
        private readonly ISmsServiceFactory _smsApiClientFactory;
        private readonly ILogger<TellmaSmsAutomation> _logger;
        private readonly IEnumerable<int> _tenantIds;

        private readonly string _smsProvider;
        public TellmaSmsAutomation(ITellmaService tellmaService, ISmsServiceFactory smsServiceFactory, IOptions<TellmaOptions> options, ILogger<TellmaSmsAutomation> logger)
        {
            _tellmaService = tellmaService;
            _smsApiClientFactory = smsServiceFactory;
            _logger = logger;

            _tenantIds = (options.Value.TenantIds ?? "")
                .Split(",")
               .Select(s =>
                {
                    if (int.TryParse(s, out int result))
                        return result;
                    else if (string.IsNullOrWhiteSpace(s))
                        throw new ArgumentException($"Error parsing TenantIds config value, the TenantIds list is empty or the service account is unable to see the secrets file..");
                    else
                        throw new ArgumentException($"Error parsing TenantIds config value, {s} is not a valid integer.");
                })
                .ToList();

            _smsProvider = options.Value.SmsProvider ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task RunSmsAutomation(CancellationToken token)
        {
            try
            {
                foreach (int tenantId in _tenantIds)
                {
                    var tenantMessages = new List<SmsRequest>();
                    var messages = await _tellmaService.PreReminder(tenantId, token);
                    tenantMessages.AddRange(messages);

                    messages = await _tellmaService.todayReminder(tenantId, token);
                    tenantMessages.AddRange(messages);

                    messages = await _tellmaService.OverdueReminder(tenantId, token);
                    tenantMessages.AddRange(messages);

                    messages = await _tellmaService.PenaltyNotice(tenantId, token);
                    tenantMessages.AddRange(messages);

                    messages = await _tellmaService.ContractExpiryNotice(tenantId, token);
                    tenantMessages.AddRange(messages);

                    // Get Sms provider.
                    ISmsService smsService = _smsApiClientFactory.Create(_smsProvider);
                    foreach (var messageRequest in tenantMessages)
                    {
                        await smsService.SendMessageAsync(messageRequest, token);

                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occured while preparing messages.");
            }
        }
    }
}
