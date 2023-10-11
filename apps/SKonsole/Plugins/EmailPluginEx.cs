using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Orchestration;

namespace SKonsole.Plugins;

internal static class EmailPluginEx
{
    public static ILogger Logger(this SKContext context)
    {
        return context.LoggerFactory.CreateLogger<EmailPlugin>();
    }
}
