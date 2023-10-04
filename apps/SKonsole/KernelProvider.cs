using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SKonsole.Utils;

namespace SKonsole;

public class KernelProvider
{
    public static KernelProvider Instance = new();

    private static readonly ILoggerFactory s_loggerFactory = Logging.GetFactory();

    public IKernel Get()
    {
        var kernelBuilder = Kernel.Builder;

        switch (Configuration.ConfigOption(ConfigConstants.LLM_PROVIDER))
        {
            case ConfigConstants.OpenAI:
                kernelBuilder = kernelBuilder.WithOpenAIChatCompletionService(
                                       Configuration.ConfigVar(ConfigConstants.OPENAI_CHAT_MODEL_ID),
                                       Configuration.ConfigVar(ConfigConstants.OPENAI_API_KEY));
                break;
            case ConfigConstants.AzureOpenAI:
            default:
                kernelBuilder = kernelBuilder.WithAzureChatCompletionService(
                                       Configuration.ConfigVar(ConfigConstants.AZURE_OPENAI_CHAT_DEPLOYMENT_NAME),
                                       Configuration.ConfigVar(ConfigConstants.AZURE_OPENAI_API_ENDPOINT),
                                       Configuration.ConfigVar(ConfigConstants.AZURE_OPENAI_API_KEY),
                                       serviceId: Configuration.ConfigVar(ConfigConstants.AZURE_OPENAI_CHAT_DEPLOYMENT_NAME));
                break;
        }

        var _kernel = kernelBuilder
            .WithRetryBasic(new()
            {
                MaxRetryCount = 3,
                MinRetryDelay = TimeSpan.FromSeconds(8),
                UseExponentialBackoff = true,
            })
            .WithLoggerFactory(s_loggerFactory)
            .Build();

        _kernel.LoggerFactory.CreateLogger<KernelProvider>().LogTrace("KernelProvider.Instance: Added Azure OpenAI backends");

        return _kernel;
    }
}
