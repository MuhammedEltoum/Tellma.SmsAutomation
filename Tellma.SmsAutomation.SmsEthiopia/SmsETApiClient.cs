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
        private readonly JsonSerializerOptions _jsonOptions;

        public string SmsProvider => "SmsEthiopia";

        public SmsETApiClient(HttpClient httpClient, IOptions<ServiceOptions> options, ILogger<SmsETApiClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure JSON options for better debugging
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };

            // Check options first
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            // Check Value
            if (options.Value == null)
                throw new ArgumentNullException($"{nameof(options)}.{nameof(options.Value)}");

            // Check ApiKey specifically
            _apiKey = options.Value.ApiKey;
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new ArgumentNullException($"{nameof(options)}.{nameof(options.Value)}.{nameof(options.Value.ApiKey)}");

            // Set the API key header
            _httpClient.DefaultRequestHeaders.Add("KEY", _apiKey);

            // Optionally set base address if not already set
            _httpClient.BaseAddress ??= new Uri("https://smsethiopia.et/api/");

            // Set a reasonable timeout
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task SendMessageAsync(SmsRequest smsRequest, CancellationToken token)
        {
            string? requestJson = null;
            string? responseContent = null;

            try
            {
                // Validate input
                if (smsRequest == null)
                {
                    _logger.LogError("SmsRequest is null");
                    return;
                }

                if (string.IsNullOrWhiteSpace(smsRequest.PhoneNumber))
                {
                    _logger.LogError("Phone number is null or empty");
                    return;
                }

                if (string.IsNullOrWhiteSpace(smsRequest.Message))
                {
                    _logger.LogError("Message content is null or empty for phone: {PhoneNumber}",
                        smsRequest.PhoneNumber);
                    return;
                }

                // Log the attempt
                _logger.LogDebug("Attempting to send SMS to: {PhoneNumber}, Message length: {MessageLength}",
                    smsRequest.PhoneNumber, smsRequest.Message.Length);

                // Prepare the request body with debugging info
                var requestBody = new
                {
                    // Use actual phone number - remove hardcoded test number
                    // Make sure to format phone number correctly (remove leading + if present)
                    msisdn = "251916394483",
                    //msisdn = FormatPhoneNumber(smsRequest.PhoneNumber),
                    text = smsRequest.Message
                };

                // Log request details
                requestJson = JsonSerializer.Serialize(requestBody, _jsonOptions);
                _logger.LogDebug("SMS Request: {RequestJson}", requestJson);

                // Post the request with detailed logging
                _logger.LogInformation("Sending SMS to {PhoneNumber} via {Provider}",
                    smsRequest.PhoneNumber, SmsProvider);

                var response = await _httpClient.PostAsJsonAsync("sms/send", requestBody, token);

                // Log response status
                _logger.LogDebug("Response Status: {StatusCode} - {ReasonPhrase}",
                    (int)response.StatusCode, response.ReasonPhrase);

                // Check for success
                if (!response.IsSuccessStatusCode)
                {
                    responseContent = await response.Content.ReadAsStringAsync(token);
                    _logger.LogError("HTTP Error sending SMS to {PhoneNumber}. Status: {StatusCode}, Response: {Response}",
                        smsRequest.PhoneNumber, (int)response.StatusCode, responseContent);
                    return;
                }

                // Read and parse response
                responseContent = await response.Content.ReadAsStringAsync(token);
                _logger.LogDebug("Raw Response: {ResponseContent}", responseContent);

                SmsResponse? smsResponse;
                try
                {
                    smsResponse = JsonSerializer.Deserialize<SmsResponse>(responseContent, _jsonOptions);
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Failed to parse response JSON for {PhoneNumber}. Response: {Response}",
                        smsRequest.PhoneNumber, responseContent);
                    return;
                }

                if (smsResponse == null)
                {
                    _logger.LogError("Empty response from SMS provider for {PhoneNumber}", smsRequest.PhoneNumber);
                    return;
                }

                // Log the complete response for debugging
                _logger.LogDebug("Parsed Response: Sent={Sent}, Description={Description}",
                    smsResponse.sent, smsResponse.description);

                if (smsResponse.sent != true)
                {
                    _logger.LogError("Failed to send SMS to {PhoneNumber}. Provider Response: {Description}",
                        smsRequest.PhoneNumber, smsResponse.description ?? "No description provided");
                    return;
                }

                _logger.LogInformation("Successfully sent SMS to {PhoneNumber}. Response: {Description}",
                    smsRequest.PhoneNumber, smsResponse.description ?? "Success");

            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP request failed while sending SMS to {PhoneNumber}. Request: {Request}, Response: {Response}",
                    smsRequest.PhoneNumber, requestJson, responseContent);
            }
            catch (TaskCanceledException taskEx)
            {
                if (taskEx.CancellationToken == token)
                {
                    _logger.LogWarning("SMS sending to {PhoneNumber} was cancelled", smsRequest.PhoneNumber);
                }
                else
                {
                    _logger.LogError(taskEx, "Timeout occurred while sending SMS to {PhoneNumber}", smsRequest.PhoneNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception while sending SMS to {PhoneNumber}. Request: {Request}, Response: {Response}",
                    smsRequest.PhoneNumber, requestJson, responseContent);
            }
        }

        private string FormatPhoneNumber(string phoneNumber)
        {
            // Remove any non-digit characters
            var digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());

            // Ensure it starts with country code
            if (digitsOnly.StartsWith("251"))
            {
                // Already in correct format
                return digitsOnly;
            }
            else if (digitsOnly.StartsWith("0"))
            {
                // Replace leading 0 with 251
                return "251" + digitsOnly.Substring(1);
            }
            else if (digitsOnly.StartsWith("9"))
            {
                // Assume it's a local number without country code
                return "251" + digitsOnly;
            }

            // Return as-is if we can't determine format
            _logger.LogWarning("Unexpected phone number format: {PhoneNumber}, using as-is: {DigitsOnly}",
                phoneNumber, digitsOnly);
            return digitsOnly;
        }
    }
}