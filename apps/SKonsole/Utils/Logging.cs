using Microsoft.Extensions.Logging;

namespace SKonsole.Utils;

internal static class Logging
{
    internal static ILoggerFactory GetFactory()
    {
        return LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Warning)
                    // .AddFilter("Microsoft.SemanticKernel.Planners.StepwisePlanner", LogLevel.Information) // Toggle to see chain of thought
                    // .AddFilter("SKonsole.Plugins.GitPlugin", LogLevel.Debug)
                    // .AddFilter("PRPlugin.PullRequestPlugin", LogLevel.Debug)
                    .AddFilter("Polly", LogLevel.Error)
                    .AddFilter("Microsoft", LogLevel.Error)
                    .AddFilter("AzureChatCompletion", LogLevel.Error)
                    .AddFilter("System", LogLevel.Error)
                    .AddFilter("SKonsole", LogLevel.Information)
                    .AddSpectreConsole();
            });
    }
}
