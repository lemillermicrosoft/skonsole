using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.KernelExtensions;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace SKonsole.Skills;

internal class CodeGenSkill
{
    private const int MaxTokens = 1024;

    private readonly ISKFunction codeGenFunction;

    public CodeGenSkill(IKernel kernel)
    {
        this.codeGenFunction = kernel.CreateSemanticFunction(
            CodeGenDefinition,
            skillName: nameof(CodeGenSkill),
            description: "Convert the following csharp code to typescript code that performs the same function.",
            maxTokens: MaxTokens,
            temperature: 0.0,
            topP: 0.5);
    }

    [SKFunction(description: "Convert the following csharp code to typescript code that performs the same function.")]
    [SKFunctionInput(Description = "The csharp code to convert to typescript.")]
    public Task<SKContext> CodeGen(string input, SKContext context)
    {
        // var chunkedInput = CommitChunker.ChunkCommitInfo(context.Variables.Input, CHUNK_SIZE);
        return this.codeGenFunction.InvokeAsync(input, context);
    }

    private const string CodeGenDefinition =
        @"Convert the following csharp code to typescript code that performs the same function. This is a piece in the conversion of the complete Semantic Kernel SDK to create and call functions that leverage the power of LLM models. Assume that the typescript code will be part of a library that exports the Semantic Kernel class and its methods, objects, and interfaces. Comment your code to explain the main steps and logic.
[CSHARP CODE]
{{$input}}
[END CSHARP CODE]

[TYPESCRIPT CODE]
";
}