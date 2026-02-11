using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

        public TellmaSmsAutomation(
            ITellmaService tellmaService,
            ISmsServiceFactory smsServiceFactory,
            IOptions<TellmaOptions> options,
            ILogger<TellmaSmsAutomation> logger)
        {
            _tellmaService = tellmaService;
            _smsApiClientFactory = smsServiceFactory;
            _logger = logger;

            _tenantIds = (options.Value.TenantIds ?? "")
                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                .Select(s =>
                {
                    if (int.TryParse(s, out int result))
                        return result;
                    else if (string.IsNullOrWhiteSpace(s))
                        throw new ArgumentException($"Error parsing TenantIds config value, the TenantIds list is empty or the service account is unable to see the secrets file.");
                    else
                        throw new ArgumentException($"Error parsing TenantIds config value, {s} is not a valid integer.");
                })
                .ToList();

            _smsProvider = options.Value.SmsProvider ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task RunSmsAutomation(List<string> templates, CancellationToken token)
        {
            try
            {
                _logger.LogInformation("Running SMS automation for templates: {Templates}",
                    string.Join(", ", templates));

                foreach (int tenantId in _tenantIds)
                {
                    var tenantMessages = new List<SmsRequest>();

                    // Execute only the specified templates
                    foreach (var template in templates)
                    {
                        var messages = template switch
                        {
                            "PreReminder" => await _tellmaService.PreReminder(tenantId, token),
                            "TodayReminder" => await _tellmaService.TodayReminder(tenantId, token),
                            "OverdueReminder" => await _tellmaService.OverdueReminder(tenantId, token),
                            "AdvancePaymentConfirmation" => await _tellmaService.AdvancePaymentConfirmation(tenantId, token),
                            "PenaltyNotice" => await _tellmaService.PenaltyNotice(tenantId, token),
                            "ContractExpiryNotice" => await _tellmaService.ContractExpiryNotice(tenantId, token),
                            _ => Enumerable.Empty<SmsRequest>()
                        };

                        tenantMessages.AddRange(messages);
                        _logger.LogDebug("Template '{Template}' generated {Count} messages for tenant {TenantId}",
                            template, messages.Count(), tenantId);
                    }

                    if (tenantMessages.Any())
                    {
                        ISmsService smsService = _smsApiClientFactory.Create(_smsProvider);
                        foreach (var messageRequest in tenantMessages)
                        {
                            await smsService.SendMessageAsync(messageRequest, token);
                            _logger.LogInformation("Sent SMS to {PhoneNumber}", messageRequest.PhoneNumber);
                        }

                        _logger.LogInformation("Sent {Count} messages for tenant {TenantId}",
                            tenantMessages.Count, tenantId);
                    }
                    else
                    {
                        _logger.LogInformation("No messages to send for tenant {TenantId}", tenantId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while running SMS automation");
                throw;
            }
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

                    messages = await _tellmaService.TodayReminder(tenantId, token);
                    tenantMessages.AddRange(messages);

                    messages = await _tellmaService.OverdueReminder(tenantId, token);
                    tenantMessages.AddRange(messages);

                    messages = await _tellmaService.AdvancePaymentConfirmation(tenantId, token);
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