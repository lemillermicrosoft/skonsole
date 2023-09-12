using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Text;

namespace SKonsole.Skills;

public class CustomConversationSummarySkill
{
    /// <summary>
    /// The max tokens to process in a single semantic function call.
    /// </summary>
    private const int MaxTokens = 1024;

    private readonly ISKFunction _summarizeConversationFunction;
    private readonly ISKFunction _conversationActionItemsFunction;
    private readonly ISKFunction _conversationTopicsFunction;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomConversationSummarySkill"/> class.
    /// </summary>
    /// <param name="kernel">Kernel instance</param>
    public CustomConversationSummarySkill(IKernel kernel, int maxTokens = MaxTokens)
    {
        this._summarizeConversationFunction = kernel.CreateSemanticFunction(
            "BEGIN CONTENT TO SUMMARIZE:\n{{$INPUT}}\n\nEND CONTENT TO SUMMARIZE.\n\nSummarize the conversation in 'CONTENT TO SUMMARIZE', identifying main points of discussion and any conclusions that were reached.\nDo not incorporate other general knowledge.\nSummary is in plain text, in complete sentences, with no markup or tags.\n\nBEGIN SUMMARY:\n",
            skillName: nameof(CustomConversationSummarySkill),
            description: "Given a section of a conversation transcript, summarize the part of the conversation.",
            maxTokens: maxTokens,
            temperature: 0.1,
            topP: 0.5);

        this._conversationActionItemsFunction = kernel.CreateSemanticFunction(
            "You are an action item extractor. You will be given chat history and need to make note of action items mentioned in the chat.\nExtract action items from the content if there are any. If there are no action, return nothing. If a single field is missing, use an empty string.\nReturn the action items in json.\n\nPossible statuses for action items are: Open, Closed, In Progress.\n\nEXAMPLE INPUT WITH ACTION ITEMS:\n\nJohn Doe said: \"I will record a demo for the new feature by Friday\"\nI said: \"Great, thanks John. We may not use all of it but it's good to get it out there.\"\n\nEXAMPLE OUTPUT:\n{\n    \"actionItems\": [\n        {\n            \"owner\": \"John Doe\",\n            \"actionItem\": \"Record a demo for the new feature\",\n            \"dueDate\": \"Friday\",\n            \"status\": \"Open\",\n            \"notes\": \"\"\n        }\n    ]\n}\n\nEXAMPLE INPUT WITHOUT ACTION ITEMS:\n\nJohn Doe said: \"Hey I'm going to the store, do you need anything?\"\nI said: \"No thanks, I'm good.\"\n\nEXAMPLE OUTPUT:\n{\n    \"action_items\": []\n}\n\nCONTENT STARTS HERE.\n\n{{$INPUT}}\n\nCONTENT STOPS HERE.\n\nOUTPUT:",
            skillName: nameof(CustomConversationSummarySkill),
            description: "Given a section of a conversation transcript, identify action items.",
            maxTokens: maxTokens,
            temperature: 0.1,
            topP: 0.5);

        this._conversationTopicsFunction = kernel.CreateSemanticFunction(
            "Analyze the following extract taken from a conversation transcript and extract key topics.\n - Topics only worth remembering.\n - Be brief.Short phrases.\n - Can use broken English.\n - Conciseness is very important.\n - Topics can include names of memories you want to recall.\n - NO LONG SENTENCES.SHORT PHRASES.\n - Return in JSON\n[Input]\nMy name is Macbeth.I used to be King of Scotland, but I died.My wife's name is Lady Macbeth and we were married for 15 years. We had no children. Our beloved dog Toby McDuff was a famous hunter of rats in the forest.\nMy tragic story was immortalized by Shakespeare in a play.\n[Output]\n{\n  \"topics\": [\n    \"Macbeth\",\n    \"King of Scotland\",\n    \"Lady Macbeth\",\n    \"Dog\",\n    \"Toby McDuff\",\n    \"Shakespeare\",\n    \"Play\",\n    \"Tragedy\"\n  ]\n}\n+++++\n[Input]\n{{$INPUT}}\n[Output]",
            skillName: nameof(CustomConversationSummarySkill),
            description: "Analyze a conversation transcript and extract key topics worth remembering.",
            maxTokens: maxTokens,
            temperature: 0.1,
            topP: 0.5);
    }

    /// <summary>
    /// Given a long conversation transcript, summarize the conversation.
    /// </summary>
    /// <param name="input">A long conversation transcript.</param>
    /// <param name="context">The SKContext for function execution.</param>
    [SKFunction, Description("Given a long conversation transcript, summarize the conversation.")]
    public Task<SKContext> SummarizeConversationAsync(
        [Description("A long conversation transcript.")] string input,
        SKContext context)
    {
        List<string> lines = TextChunker.SplitPlainTextLines(input, MaxTokens);
        List<string> paragraphs = TextChunker.SplitPlainTextParagraphs(lines, MaxTokens);

        return this._summarizeConversationFunction
            .AggregatePartitionedResultsAsync(paragraphs, context);
    }

    /// <summary>
    /// Given a long conversation transcript, identify action items.
    /// </summary>

    /// <param name="input">A long conversation transcript.</param>
    /// <param name="context">The SKContext for function execution.</param>
    [SKFunction, Description("Given a long conversation transcript, identify action items.")]
    public Task<SKContext> GetConversationActionItemsAsync(
        [Description("A long conversation transcript.")] string input,
        SKContext context)
    {
        List<string> lines = TextChunker.SplitPlainTextLines(input, MaxTokens);
        List<string> paragraphs = TextChunker.SplitPlainTextParagraphs(lines, MaxTokens);

        return this._conversationActionItemsFunction
            .AggregatePartitionedResultsAsync(paragraphs, context);
    }

    /// <summary>
    /// Given a long conversation transcript, identify topics.
    /// </summary>
    /// <param name="input">A long conversation transcript.</param>
    /// <param name="context">The SKContext for function execution.</param>
    [SKFunction, Description("Given a long conversation transcript, identify topics worth remembering.")]
    public Task<SKContext> GetConversationTopicsAsync(
        [Description("A long conversation transcript.")] string input,
        SKContext context)
    {
        List<string> lines = TextChunker.SplitPlainTextLines(input, MaxTokens);
        List<string> paragraphs = TextChunker.SplitPlainTextParagraphs(lines, MaxTokens);

        return this._conversationTopicsFunction
            .AggregatePartitionedResultsAsync(paragraphs, context);
    }
}
