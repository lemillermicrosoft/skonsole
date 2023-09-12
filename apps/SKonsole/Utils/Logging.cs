using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace SKonsole.Utils;

internal static class Logging
{
    internal static ILoggerFactory GetFactory()
    {
        return LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Information)
                    // .AddFilter("Microsoft.SemanticKernel.Planning", LogLevel.Information)
                    // .AddFilter("Microsoft.SemanticKernel.TemplateEngine.Prompt", LogLevel.Trace)
                    .AddFilter("Microsoft", LogLevel.Error)
                    .AddFilter("AzureChatCompletion", LogLevel.Error)
                    .AddFilter("System", LogLevel.Error)
                    .AddFilter("SKonsole", LogLevel.Information)
                    // .AddFilter("Microsoft.SemanticKernel.Planning.StepwisePlanner", LogLevel.Information)
                    .AddConsole();// todo instead of Console use Spectre.Console | AnsiConsole
            });
    }
}
