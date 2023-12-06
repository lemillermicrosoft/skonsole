using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

using SKonsole.Utils;

namespace SKonsole;

public class KernelProvider
{
    public static KernelProvider Instance = new();

    private static readonly ILoggerFactory s_loggerFactory = Logging.GetFactory();

    public Kernel Get()
    {
        var kernelBuilder = new KernelBuilder();

        kernelBuilder = Configuration.ConfigOption(ConfigConstants.LLM_PROVIDER) switch
        {
            ConfigConstants.OpenAI => kernelBuilder.WithOpenAIChatCompletion(
                                                   Configuration.ConfigVar(ConfigConstants.OPENAI_CHAT_MODEL_ID),
                                                   Configuration.ConfigVar(ConfigConstants.OPENAI_API_KEY)),
            _ => kernelBuilder.WithAzureOpenAIChatCompletion(
                                                   Configuration.ConfigVar(ConfigConstants.AZURE_OPENAI_CHAT_DEPLOYMENT_NAME),
                                                   "gpt-4", // functioncalling planner needs this to resolve execution_settings. Kind of mysterious, how would we enable users to configure this?
                                                   Configuration.ConfigVar(ConfigConstants.AZURE_OPENAI_API_ENDPOINT),
                                                   Configuration.ConfigVar(ConfigConstants.AZURE_OPENAI_API_KEY)),
        };
        var _kernel = kernelBuilder
            .WithLoggerFactory(s_loggerFactory)
            .Build();

        _kernel.LoggerFactory.CreateLogger(this.GetType()).LogTrace("KernelProvider.Instance: Added Azure OpenAI backends");

        return _kernel;
    }
}
