using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;

namespace SKonsole.Plugins;

internal sealed class WriterPlugin
{
    private const int MaxTokens = 1024;

    private readonly KernelFunction _funnyPoemFunction;

    public WriterPlugin(Kernel kernel)
    {
        this._funnyPoemFunction = kernel.CreateFunctionFromPrompt(
            FunnyPoemDefinition,
            // pluginName: nameof(WriterPlugin),
            description: "Given a input topic or description or list, write a funny poem.",
            executionSettings: new PromptExecutionSettings()
            {
                ExtensionData = new Dictionary<string, object>()
                {
                    { "Temperature", 0.1 },
                    { "TopP", 0.5 },
                    { "MaxTokens", MaxTokens }
                }
            });
    }

    private const string FunnyPoemDefinition =
        @"
Generate a funny poem or limerick for the given context. Be creative and be funny. Crazy is good.
Context:
{{$input}}

Poem:

";
}
