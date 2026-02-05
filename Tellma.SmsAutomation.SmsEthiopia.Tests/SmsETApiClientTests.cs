using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using Tellma.SmsAutomation.Contract;
using Tellma.SmsAutomation.SmsEthiopia;
using Xunit;

namespace SmsAutomation.Tests
{
    public class SmsETApiClientTests
    {
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IOptions<ServiceOptions>> _mockOptions;
        private readonly Mock<ILogger<SmsETApiClient>> _mockLogger;
        private readonly ServiceOptions _serviceOptions;

        public SmsETApiClientTests()
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://smsethiopia.et/api/")
            };

            _serviceOptions = new ServiceOptions { ApiKey = "test-api-key-12345" };
            _mockOptions = new Mock<IOptions<ServiceOptions>>();
            _mockOptions.Setup(o => o.Value).Returns(_serviceOptions);

            _mockLogger = new Mock<ILogger<SmsETApiClient>>();
        }

        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var client = new SmsETApiClient(_httpClient, _mockOptions.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(client);
            Assert.Equal("SmsEthiopia", client.SmsProvider);
            Assert.True(_httpClient.DefaultRequestHeaders.Contains("KEY"));
        }

        [Fact]
        public void Constructor_WithNullHttpClient_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new SmsETApiClient(null!, _mockOptions.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new SmsETApiClient(_httpClient, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new SmsETApiClient(_httpClient, _mockOptions.Object, null!));
        }

        [Fact]
        public void Constructor_WithNullApiKey_ShouldThrowArgumentNullException()
        {
            // Arrange
            var invalidOptions = new ServiceOptions { ApiKey = null! };
            var mockInvalidOptions = new Mock<IOptions<ServiceOptions>>();
            mockInvalidOptions.Setup(o => o.Value).Returns(invalidOptions);

            // Act
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new SmsETApiClient(_httpClient, mockInvalidOptions.Object, _mockLogger.Object));

            // Assert
            Assert.Contains("ApiKey", exception.ParamName);
        }

        [Fact]
        public void Constructor_ShouldSetBaseAddress_WhenNotAlreadySet()
        {
            // Arrange
            var httpClientWithoutBaseAddress = new HttpClient(_mockHttpMessageHandler.Object);

            // Act
            var client = new SmsETApiClient(httpClientWithoutBaseAddress, _mockOptions.Object, _mockLogger.Object);

            // Assert
            Assert.Equal("https://smsethiopia.et/api/", httpClientWithoutBaseAddress.BaseAddress?.AbsoluteUri);
        }

        [Fact]
        public async Task SendMessageAsync_ShouldSendSuccessfully_WhenApiReturnsSuccess()
        {
            // Arrange
            var smsRequest = new SmsRequest
            {
                PhoneNumber = "+251975123630",
                Message = "Test message"
            };

            var expectedResponse = new
            {
                sent = true,
                description = "Message sent successfully"
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(expectedResponse))
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.AbsoluteUri.Contains("sms/send") &&
                        req.Headers.Contains("KEY")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var client = new SmsETApiClient(_httpClient, _mockOptions.Object, _mockLogger.Object);

            // Act
            await client.SendMessageAsync(smsRequest, CancellationToken.None);

            // Assert
            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.AbsoluteUri.Contains("sms/send")),
                ItExpr.IsAny<CancellationToken>());

            VerifyLog(LogLevel.Information, "SMS sent to");
        }

        [Fact]
        public async Task SendMessageAsync_ShouldLogError_WhenApiReturnsFailure()
        {
            // Arrange
            var smsRequest = new SmsRequest
            {
                PhoneNumber = "+251975123630",
                Message = "Test message"
            };

            var expectedResponse = new
            {
                sent = false,
                description = "Insufficient balance"
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(expectedResponse))
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var client = new SmsETApiClient(_httpClient, _mockOptions.Object, _mockLogger.Object);

            // Act
            await client.SendMessageAsync(smsRequest, CancellationToken.None);

            // Assert
            VerifyLog(LogLevel.Error, "Failed to send SMS");
        }

        [Fact]
        public async Task SendMessageAsync_ShouldLogError_WhenHttpRequestFails()
        {
            // Arrange
            var smsRequest = new SmsRequest
            {
                PhoneNumber = "+251975123630",
                Message = "Test message"
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            var client = new SmsETApiClient(_httpClient, _mockOptions.Object, _mockLogger.Object);

            // Act
            await client.SendMessageAsync(smsRequest, CancellationToken.None);

            // Assert
            VerifyLog(LogLevel.Error, "HTTP request failed");
        }

        [Fact]
        public async Task SendMessageAsync_ShouldLogError_WhenGeneralExceptionOccurs()
        {
            // Arrange
            var smsRequest = new SmsRequest
            {
                PhoneNumber = "+251975123630",
                Message = "Test message"
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Some other error"));

            var client = new SmsETApiClient(_httpClient, _mockOptions.Object, _mockLogger.Object);

            // Act
            await client.SendMessageAsync(smsRequest, CancellationToken.None);

            // Assert
            VerifyLog(LogLevel.Error, "Exception occurred while sending SMS");
        }

        [Fact]
        public async Task SendMessageAsync_ShouldUseHardcodedPhoneNumber_ForTesting()
        {
            // Arrange
            var smsRequest = new SmsRequest
            {
                PhoneNumber = "+251975123631",
                Message = "Test message"
            };

            var expectedResponse = new
            {
                sent = true,
                description = "Message sent successfully"
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(expectedResponse))
            };

            string capturedContent = string.Empty;
            var capturedRequest = new List<HttpRequestMessage>();

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
                {
                    capturedRequest.Add(request);
                    capturedContent = request.Content?.ReadAsStringAsync(token).Result ?? string.Empty;
                    return responseMessage;
                });

            var client = new SmsETApiClient(_httpClient, _mockOptions.Object, _mockLogger.Object);

            // Act
            await client.SendMessageAsync(smsRequest, CancellationToken.None);

            // Assert
            Assert.Single(capturedRequest);
            Assert.Contains("251975123630", capturedContent);
            Assert.DoesNotContain("251975123631", capturedContent);
        }

        [Fact]
        public async Task SendMessageAsync_ShouldIncludeApiKeyInHeaders()
        {
            // Arrange
            var smsRequest = new SmsRequest
            {
                PhoneNumber = "+251975123630",
                Message = "Test message"
            };

            var expectedResponse = new
            {
                sent = true,
                description = "Message sent successfully"
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(expectedResponse))
            };

            string? capturedApiKey = null;

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.AbsoluteUri.Contains("sms/send")),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
                {
                    capturedApiKey = req.Headers.GetValues("KEY").FirstOrDefault();
                })
                .ReturnsAsync(responseMessage);

            var client = new SmsETApiClient(_httpClient, _mockOptions.Object, _mockLogger.Object);

            // Act
            await client.SendMessageAsync(smsRequest, CancellationToken.None);

            // Assert
            Assert.Equal("test-api-key-12345", capturedApiKey);
        }

        private void VerifyLog(LogLevel logLevel, string expectedMessage)
        {
            _mockLogger.Verify(
                x => x.Log(
                    logLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }
    }
}