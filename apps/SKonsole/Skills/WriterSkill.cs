using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.KernelExtensions;
using Microsoft.SemanticKernel.Orchestration;

namespace SKonsole.Skills;

internal class WriterSkill
{
    private const int MaxTokens = 1024;

    private readonly ISKFunction funnyPoemFunction;

    public WriterSkill(IKernel kernel)
    {
        this.funnyPoemFunction = kernel.CreateSemanticFunction(
            FunnyPoemDefinition,
            skillName: nameof(WriterSkill),
            description: "Given a input topic or description or list, write a funny poem.",
            maxTokens: MaxTokens,
            temperature: 0.1,
            topP: 0.5);
    }

    private const string FunnyPoemDefinition =
        @"
Generate a funny poem or limerick for the given context. Be creative and be funny. Crazy is good.
Context:
{{$input}}

Poem:

";
}