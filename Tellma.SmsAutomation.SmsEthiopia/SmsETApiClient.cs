using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using Tellma.SmsAutomation.Contract;
using Tellma.SMSAutomation.Contract;

namespace Tellma.SmsAutomation.SmsEthiopia
{
    public class SmsETApiClient : ISmsService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<SmsETApiClient> _logger;

        public string SmsProvider => "SmsEthiopia";

        public SmsETApiClient(HttpClient httpClient, IOptions<ServiceOptions> options, ILogger<SmsETApiClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Check options first
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            // Check Value
            if (options.Value == null)
                throw new ArgumentNullException($"{nameof(options)}.{nameof(options.Value)}");

            // Check ApiKey specifically
            _apiKey = options.Value.ApiKey;
            if (_apiKey == null)
                throw new ArgumentNullException($"{nameof(options)}.{nameof(options.Value)}.{nameof(options.Value.ApiKey)}");

            // Set the API key header
            _httpClient.DefaultRequestHeaders.Add("KEY", _apiKey);

            // Optionally set base address if not already set
            _httpClient.BaseAddress ??= new Uri("https://smsethiopia.et/api/");
        }

        public async Task SendMessageAsync(SmsRequest smsRequest, CancellationToken token)
        {
            try
            {
                var requestBody = new
                {
                    //msisdn = smsRequest.PhoneNumber,
                    msisdn = 251975123630, // For testing purposes only
                    text = smsRequest.Message
                };

                // PostAsJsonAsync automatically sets Content-Type: application/json
                var response = await _httpClient.PostAsJsonAsync("sms/send", requestBody, token);
                response.EnsureSuccessStatusCode(); // Throws exception for non-success status codes

                var responseBody = await response.Content.ReadAsStringAsync(token);
                var smsResponse = JsonSerializer.Deserialize<SmsResponse>(responseBody);

                if (smsResponse == null || smsResponse.sent != true)
                {
                    _logger.LogError("Failed to send SMS to {PhoneNumber}. Response: {Response}",
                        smsRequest.PhoneNumber, smsResponse?.description ?? "No response");
                    return;
                }

                _logger.LogInformation("SMS sent to {PhoneNumber}. Response Status: {Status}",
                    smsRequest.PhoneNumber, smsResponse.sent);

            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP request failed while sending SMS to {PhoneNumber}",
                    smsRequest.PhoneNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending SMS to {PhoneNumber}",
                    smsRequest.PhoneNumber);
            }
        }
    }
}