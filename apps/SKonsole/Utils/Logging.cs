using Microsoft.Extensions.Logging;

namespace SKonsole.Utils;

internal static class Logging
{
    internal static ILoggerFactory GetFactory()
    {
        return LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Information)
                    .AddFilter("Microsoft", LogLevel.Error)
                    .AddFilter("System", LogLevel.Error)
                    .AddConsole();
            });
    }
}
