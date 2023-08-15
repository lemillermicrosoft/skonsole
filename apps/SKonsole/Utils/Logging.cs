using Microsoft.Extensions.Logging;

namespace SKonsole.Utils;

internal static class Logging
{
    internal static ILoggerFactory GetFactory()
    {
        return LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Error)
                    .AddFilter("System", LogLevel.Error)
                    .AddFilter("Program", LogLevel.Information)
                    .AddConsole();
            });
    }
}
