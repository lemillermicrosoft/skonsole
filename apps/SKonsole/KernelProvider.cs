using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Reliability;
using SKonsole.Utils;

namespace SKonsole;

public class KernelProvider
{
    public static KernelProvider Instance = new();
    private readonly ILogger _logger;

    public KernelProvider()
    {
        using var factory = Logging.GetFactory();
        this._logger = factory.CreateLogger<Kernel>();
    }

    public IKernel Get()
    {
        var _kernel = Kernel.Builder
            .Configure((config) =>
            {
                config.SetDefaultHttpRetryConfig(new HttpRetryConfig
                {
                    MaxRetryCount = 3,
                    MinRetryDelay = TimeSpan.FromSeconds(8),
                    UseExponentialBackoff = true,
                });
            })
            .WithAzureChatCompletionService(
                Configuration.ConfigVar("AZURE_OPENAI_CHAT_DEPLOYMENT_NAME"),
                Configuration.ConfigVar("AZURE_OPENAI_API_ENDPOINT"),
                Configuration.ConfigVar("AZURE_OPENAI_API_KEY"))
            .WithLogger(this._logger)
            .Build();
        return _kernel;
    }
}
