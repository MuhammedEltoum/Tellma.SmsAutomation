using Microsoft.Extensions.Options;
using Tellma.SmsAutomation;

namespace Tellma.SMSAutomation.WinService
{
    public class Worker : BackgroundService
    {
        // DI container
        private readonly IServiceProvider _serviceProvider;


        public Worker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Worker>>();
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                try
                {
                    var importer = scope.ServiceProvider.GetRequiredService<TellmaSmsAutomation>();
                    await importer.RunSmsAutomation(stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled Error");
                    // Don't throw. Instead, wait for period then try again
                }

                await Task.Delay(60 * 1000, stoppingToken);
            }
        }
    }
}