using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Tellma.SmsAutomation.Contract;
using Tellma.SMSAutomation;

namespace Tellma.SmsAutomation.Tests
{
    public class TellmaSmsAutomationTests
    {
        private readonly Mock<ITellmaService> _mockTellmaService;
        private readonly Mock<ISmsServiceFactory> _mockSmsServiceFactory;
        private readonly Mock<ISmsService> _mockSmsService;
        private readonly Mock<ILogger<TellmaSmsAutomation>> _mockLogger;
        private readonly Mock<IOptions<TellmaOptions>> _mockOptions;
        private readonly TellmaSmsAutomation _smsAutomation;

        public TellmaSmsAutomationTests()
        {
            _mockTellmaService = new Mock<ITellmaService>();
            _mockSmsServiceFactory = new Mock<ISmsServiceFactory>();
            _mockSmsService = new Mock<ISmsService>();
            _mockLogger = new Mock<ILogger<TellmaSmsAutomation>>();
            _mockOptions = new Mock<IOptions<TellmaOptions>>();

            var optionsValue = new TellmaOptions
            {
                TenantIds = "1,2,3",
                SmsProvider = "TestProvider"
            };

            _mockOptions.Setup(o => o.Value).Returns(optionsValue);

            // Setup the factory to return the same mock service each time
            _mockSmsServiceFactory.Setup(f => f.Create("TestProvider"))
                .Returns(_mockSmsService.Object);

            _smsAutomation = new TellmaSmsAutomation(
                _mockTellmaService.Object,
                _mockSmsServiceFactory.Object,
                _mockOptions.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public void Constructor_WithEmptyTenantIds_ThrowsArgumentException()
        {
            // Arrange
            var emptyOptions = new Mock<IOptions<TellmaOptions>>();
            emptyOptions.Setup(o => o.Value).Returns(new TellmaOptions { TenantIds = "" });

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                new TellmaSmsAutomation(
                    _mockTellmaService.Object,
                    _mockSmsServiceFactory.Object,
                    emptyOptions.Object,
                    _mockLogger.Object
                ));

            Assert.Contains("TenantIds", exception.Message);
        }

        [Fact]
        public void Constructor_WithInvalidTenantId_ThrowsArgumentException()
        {
            // Arrange
            var invalidOptions = new Mock<IOptions<TellmaOptions>>();
            invalidOptions.Setup(o => o.Value).Returns(new TellmaOptions { TenantIds = "1,invalid,3" });

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                new TellmaSmsAutomation(
                    _mockTellmaService.Object,
                    _mockSmsServiceFactory.Object,
                    invalidOptions.Object,
                    _mockLogger.Object
                ));

            Assert.Contains("not a valid integer", exception.Message);
        }

        [Fact]
        public void Constructor_WithNullSmsProvider_ThrowsArgumentNullException()
        {
            // Arrange
            var nullProviderOptions = new Mock<IOptions<TellmaOptions>>();
            nullProviderOptions.Setup(o => o.Value).Returns(new TellmaOptions
            {
                TenantIds = "1",
                SmsProvider = null
            });

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TellmaSmsAutomation(
                    _mockTellmaService.Object,
                    _mockSmsServiceFactory.Object,
                    nullProviderOptions.Object,
                    _mockLogger.Object
                ));
        }

        [Fact]
        public async Task RunSmsAutomation_CallsAllServiceMethodsForEachTenant()
        {
            // Arrange
            var cancellationToken = CancellationToken.None;

            // Setup mock to return empty collections for all service methods
            _mockTellmaService.Setup(s => s.PreReminder(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.TodayReminder(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.OverdueReminder(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.AdvancePaymentConfirmation(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.PenaltyNotice(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.ContractExpiryNotice(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());

            // Act
            await _smsAutomation.RunSmsAutomation(cancellationToken);

            // Assert
            // Verify each service method was called for each tenant (3 tenants)
            _mockTellmaService.Verify(s => s.PreReminder(1, cancellationToken), Times.Once);
            _mockTellmaService.Verify(s => s.PreReminder(2, cancellationToken), Times.Once);
            _mockTellmaService.Verify(s => s.PreReminder(3, cancellationToken), Times.Once);

            _mockTellmaService.Verify(s => s.TodayReminder(1, cancellationToken), Times.Once);
            _mockTellmaService.Verify(s => s.TodayReminder(2, cancellationToken), Times.Once);
            _mockTellmaService.Verify(s => s.TodayReminder(3, cancellationToken), Times.Once);

            // Verify SMS service factory was called once per tenant (3 times total)
            _mockSmsServiceFactory.Verify(f => f.Create("TestProvider"), Times.Exactly(3));

            // Verify SMS service methods were called (0 times since no messages)
            _mockSmsService.Verify(s => s.SendMessageAsync(It.IsAny<SmsRequest>(), cancellationToken), Times.Never);
        }

        [Fact]
        public async Task RunSmsAutomation_SendsMessagesForAllTenants()
        {
            // Arrange
            var cancellationToken = CancellationToken.None;

            var smsRequests = new List<SmsRequest>
            {
                new SmsRequest { PhoneNumber = "+1234567890", Message = "Test message 1" },
                new SmsRequest { PhoneNumber = "+0987654321", Message = "Test message 2" }
            };

            // Setup mock to return test messages for first tenant only
            _mockTellmaService.Setup(s => s.PreReminder(1, cancellationToken))
                .ReturnsAsync(smsRequests);
            _mockTellmaService.Setup(s => s.PreReminder(It.Is<int>(id => id != 1), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());

            // Setup other methods to return empty
            _mockTellmaService.Setup(s => s.TodayReminder(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.OverdueReminder(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.AdvancePaymentConfirmation(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.PenaltyNotice(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.ContractExpiryNotice(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());

            // Act
            await _smsAutomation.RunSmsAutomation(cancellationToken);

            // Assert
            // Verify SMS service factory was called for each tenant
            _mockSmsServiceFactory.Verify(f => f.Create("TestProvider"), Times.Exactly(3));

            // Verify SMS service was called for each message (2 messages)
            _mockSmsService.Verify(s => s.SendMessageAsync(smsRequests[0], cancellationToken), Times.Once);
            _mockSmsService.Verify(s => s.SendMessageAsync(smsRequests[1], cancellationToken), Times.Once);
        }

        [Fact]
        public async Task RunSmsAutomation_WhenExceptionOccurs_LogsError()
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var exception = new InvalidOperationException("Test exception");

            _mockTellmaService.Setup(s => s.PreReminder(It.IsAny<int>(), cancellationToken))
                .ThrowsAsync(exception);

            // Act
            await _smsAutomation.RunSmsAutomation(cancellationToken);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains("An error occured")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Verify SMS service factory was not called due to exception
            _mockSmsServiceFactory.Verify(f => f.Create(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RunSmsAutomation_CombinesMessagesFromAllServiceMethods()
        {
            // Arrange
            var cancellationToken = CancellationToken.None;

            // Setup messages for tenant 1 only
            var preReminderMessages = new List<SmsRequest>
            {
                new SmsRequest { PhoneNumber = "+111", Message = "Pre-reminder" }
            };

            var todayReminderMessages = new List<SmsRequest>
            {
                new SmsRequest { PhoneNumber = "+222", Message = "Today reminder" }
            };

            var overdueMessages = new List<SmsRequest>
            {
                new SmsRequest { PhoneNumber = "+333", Message = "Overdue" }
            };

            // Setup different messages from different service methods for tenant 1
            _mockTellmaService.Setup(s => s.PreReminder(1, cancellationToken))
                .ReturnsAsync(preReminderMessages);
            _mockTellmaService.Setup(s => s.TodayReminder(1, cancellationToken))
                .ReturnsAsync(todayReminderMessages);
            _mockTellmaService.Setup(s => s.OverdueReminder(1, cancellationToken))
                .ReturnsAsync(overdueMessages);

            // Setup tenant 2 and 3 to return empty lists
            _mockTellmaService.Setup(s => s.PreReminder(It.Is<int>(id => id != 1), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.TodayReminder(It.Is<int>(id => id != 1), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.OverdueReminder(It.Is<int>(id => id != 1), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());

            // Setup all other methods to return empty for all tenants
            _mockTellmaService.Setup(s => s.AdvancePaymentConfirmation(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.PenaltyNotice(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.ContractExpiryNotice(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());

            // Act
            await _smsAutomation.RunSmsAutomation(cancellationToken);

            // Assert
            // Verify SMS service factory was called for each tenant (3 times)
            _mockSmsServiceFactory.Verify(f => f.Create("TestProvider"), Times.Exactly(3));

            // Verify each message was sent exactly once (only for tenant 1)
            _mockSmsService.Verify(s => s.SendMessageAsync(
                It.Is<SmsRequest>(r => r.PhoneNumber == "+111" && r.Message == "Pre-reminder"),
                cancellationToken), Times.Once);
            _mockSmsService.Verify(s => s.SendMessageAsync(
                It.Is<SmsRequest>(r => r.PhoneNumber == "+222" && r.Message == "Today reminder"),
                cancellationToken), Times.Once);
            _mockSmsService.Verify(s => s.SendMessageAsync(
                It.Is<SmsRequest>(r => r.PhoneNumber == "+333" && r.Message == "Overdue"),
                cancellationToken), Times.Once);
        }

        [Fact]
        public async Task RunSmsAutomation_CreatesNewSmsServiceForEachTenant()
        {
            // Arrange
            var cancellationToken = CancellationToken.None;

            // Create a list to track created SMS service instances
            var createdServices = new List<ISmsService>();
            var mockSmsService1 = new Mock<ISmsService>();
            var mockSmsService2 = new Mock<ISmsService>();
            var mockSmsService3 = new Mock<ISmsService>();

            // Setup factory to return different instances in order
            _mockSmsServiceFactory.SetupSequence(f => f.Create("TestProvider"))
                .Returns(mockSmsService1.Object)
                .Returns(mockSmsService2.Object)
                .Returns(mockSmsService3.Object);

            // Setup some messages for each tenant
            _mockTellmaService.Setup(s => s.PreReminder(1, cancellationToken))
                .ReturnsAsync(new List<SmsRequest> { new SmsRequest { PhoneNumber = "+111", Message = "Test1" } });
            _mockTellmaService.Setup(s => s.PreReminder(2, cancellationToken))
                .ReturnsAsync(new List<SmsRequest> { new SmsRequest { PhoneNumber = "+222", Message = "Test2" } });
            _mockTellmaService.Setup(s => s.PreReminder(3, cancellationToken))
                .ReturnsAsync(new List<SmsRequest> { new SmsRequest { PhoneNumber = "+333", Message = "Test3" } });

            // Setup other methods to return empty
            _mockTellmaService.Setup(s => s.TodayReminder(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.OverdueReminder(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.AdvancePaymentConfirmation(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.PenaltyNotice(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());
            _mockTellmaService.Setup(s => s.ContractExpiryNotice(It.IsAny<int>(), cancellationToken))
                .ReturnsAsync(new List<SmsRequest>());

            // Act
            await _smsAutomation.RunSmsAutomation(cancellationToken);

            // Assert
            // Verify factory was called 3 times
            _mockSmsServiceFactory.Verify(f => f.Create("TestProvider"), Times.Exactly(3));

            // Verify each SMS service received its messages
            mockSmsService1.Verify(s => s.SendMessageAsync(
                It.Is<SmsRequest>(r => r.PhoneNumber == "+111"), cancellationToken), Times.Once);
            mockSmsService2.Verify(s => s.SendMessageAsync(
                It.Is<SmsRequest>(r => r.PhoneNumber == "+222"), cancellationToken), Times.Once);
            mockSmsService3.Verify(s => s.SendMessageAsync(
                It.Is<SmsRequest>(r => r.PhoneNumber == "+333"), cancellationToken), Times.Once);
        }
    }
}