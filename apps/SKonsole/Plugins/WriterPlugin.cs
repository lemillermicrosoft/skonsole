using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;

namespace SKonsole.Plugins;

internal sealed class WriterPlugin
{
    private const int MaxTokens = 1024;

    private readonly ISKFunction _funnyPoemFunction;

    public WriterPlugin(IKernel kernel)
    {
        this._funnyPoemFunction = kernel.CreateSemanticFunction(
            FunnyPoemDefinition,
            pluginName: nameof(WriterPlugin),
            description: "Given a input topic or description or list, write a funny poem.",
            requestSettings: new AIRequestSettings()
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
