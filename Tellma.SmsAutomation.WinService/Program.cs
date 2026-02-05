using Microsoft.Extensions.Options;
using Tellma.Client;
using Tellma.SmsAutomation;
using Tellma.SmsAutomation.Contract;
using Tellma.SmsAutomation.SmsEthiopia;
using Tellma.SMSAutomation;
using Tellma.SMSAutomation.WinService;
using Tellma.Utilities.EmailLogger;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(config =>
    {
        config.ServiceName = "Tellma Attendance Importer";
    })
    .ConfigureServices((hostContext, services) =>
    {
        // Configuration
        services.Configure<TellmaOptions>(hostContext.Configuration.GetSection("Tellma"));
        services.Configure<ServiceOptions>(hostContext.Configuration.GetSection("ServiceSettings"));
        services.Configure<EmailOptions>(hostContext.Configuration.GetSection("Email"));

        // HttpClient registrations
        services.AddHttpClient<ISmsService, SmsETApiClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<ServiceOptions>>();
            client.BaseAddress = new Uri(options.Value.BaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // Tellma Client - single instance for the entire application
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<TellmaOptions>>();
            return new TellmaClient(
                baseUrl: "https://web.tellma.com",
                authorityUrl: "https://web.tellma.com",
                clientId: options.Value.ClientId,
                clientSecret: options.Value.ClientSecret);
        });

        services.AddScoped<TellmaSmsAutomation>();
        services.AddScoped<ITellmaService, TellmaService>();
        services.AddScoped<ISmsServiceFactory, SmsServiceFactory>();
        services.AddScoped<SmsETApiClient>();


        // Email services
        services.AddScoped<EmailLogger>();  // Changed from AddSingleton to AddScoped if used in scoped services

        services.AddHostedService<Worker>();
    })
    .ConfigureLogging((hostContext, loggingBuilder) =>
    {
        loggingBuilder.AddDebug();
        loggingBuilder.AddEmail(hostContext.Configuration);
    })
    .Build();

host.Run();