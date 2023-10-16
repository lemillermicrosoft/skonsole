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

        kernelBuilder = Configuration.ConfigOption(ConfigConstants.LLM_PROVIDER) switch
        {
            ConfigConstants.OpenAI => kernelBuilder.WithOpenAIChatCompletionService(
                                                   Configuration.ConfigVar(ConfigConstants.OPENAI_CHAT_MODEL_ID),
                                                   Configuration.ConfigVar(ConfigConstants.OPENAI_API_KEY)),
            _ => kernelBuilder.WithAzureChatCompletionService(
                                                   Configuration.ConfigVar(ConfigConstants.AZURE_OPENAI_CHAT_DEPLOYMENT_NAME),
                                                   Configuration.ConfigVar(ConfigConstants.AZURE_OPENAI_API_ENDPOINT),
                                                   Configuration.ConfigVar(ConfigConstants.AZURE_OPENAI_API_KEY)),
        };
        var _kernel = kernelBuilder
            .WithRetryBasic(new()
            {
                MaxRetryCount = 3,
                MinRetryDelay = TimeSpan.FromSeconds(8),
                UseExponentialBackoff = true,
            })
            .WithLoggerFactory(s_loggerFactory)
            .Build();

        _kernel.LoggerFactory.CreateLogger(this.GetType()).LogTrace("KernelProvider.Instance: Added Azure OpenAI backends");

        return _kernel;
    }
}
