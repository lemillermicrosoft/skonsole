using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;

namespace SKonsole.Skills;

internal sealed class WriterSkill
{
    private const int MaxTokens = 1024;

    private readonly ISKFunction _funnyPoemFunction;

    public WriterSkill(IKernel kernel)
    {
        this._funnyPoemFunction = kernel.CreateSemanticFunction(
            FunnyPoemDefinition,
            pluginName: nameof(WriterSkill),
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
