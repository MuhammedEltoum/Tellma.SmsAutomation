using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tellma.Utilities.EmailLogger;

public class EmailLoggerProvider : ILoggerProvider
{
    private readonly EmailLogger _logger;

    public EmailLoggerProvider(IOptions<EmailOptions> options)
    {
        _logger = new EmailLogger(options);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _logger;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}