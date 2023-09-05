using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Reliability;
using SKonsole.Utils;

namespace SKonsole;

public class KernelProvider
{
    public static KernelProvider Instance = new();

    private static readonly ILoggerFactory s_loggerFactory = Logging.GetFactory();

    public IKernel Get()
    {
        var _kernel = Kernel.Builder
            .WithRetryBasic(new()
            {
                MaxRetryCount = 3,
                MinRetryDelay = TimeSpan.FromSeconds(8),
                UseExponentialBackoff = true,
            })
            .WithAzureChatCompletionService(
                Configuration.ConfigVar("AZURE_OPENAI_CHAT_DEPLOYMENT_NAME"),
                Configuration.ConfigVar("AZURE_OPENAI_API_ENDPOINT"),
                Configuration.ConfigVar("AZURE_OPENAI_API_KEY"))
            .WithLoggerFactory(s_loggerFactory)
            .Build();

        _kernel.LoggerFactory.CreateLogger<KernelProvider>().LogTrace("KernelProvider.Instance: Added Azure OpenAI backends");

        return _kernel;
    }
}
