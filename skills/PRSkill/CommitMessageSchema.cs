using System.Text.Json.Serialization;
using Microsoft.TypeChat;
using Microsoft.TypeChat.Schema;

namespace PRSkill;

[Comment("A basic commit message with a title and body.")]
public partial class BasicCommitMessage
{
    [Comment("The title of the commit message. Should be less than 50 characters.")]
    public string? Title { get; set; }
    [Comment("The body of the commit message. Should be formatted with newlines every 72 characters.")]
    public string? Body { get; set; } // todo even this is hallucinating the #123 related-by

    public override string ToString()
    {
        return
@$"{this.Title}

{this.Body}";
    }
}

[Comment("A basic commit message with a title and body.")]
public partial class EmojiCommitMessage : BasicCommitMessage
{
    [JsonVocab(CommitMessageVocabs.Emoji, PropertyName = "emoji")]
    [Comment("The emoji to prefix the title with.")]
    public string? Emoji { get; set; }

    public override string ToString()
    {
        return $"{this.Emoji} {base.ToString()}";
    }
}

[Comment("A conventional commit message with a type, scope, subject, optional body, and optional footer.")]
public partial class ConventionalCommitMessage
{
    [JsonVocab(CommitMessageVocabs.Type, PropertyName = "type")]
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    [JsonPropertyName("scope")]
    [Comment("The scope of the commit. This is usually the name of the package or module that was changed.A scope MAY be provided after a type. A scope MUST consist of a noun describing a section of the codebase.")]
    public string? Scope { get; set; }

    [Comment("A short description of the change. The description MUST begin with a capital letter and end with a period. The description MUST be written in the imperative, present tense: “change” not “changed” nor “changes”. The description MUST be no longer than 50 characters.")]
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    [Comment("A longer commit body MAY be provided after the short description, providing additional contextual information about the code changes. The body MUST begin one blank line after the description. A commit body is free-form and MAY consist of any number of newline separated paragraphs.")]
    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [Comment("One or more footers MAY be provided. Each footer MUST consist of a word token, followed by either a :<space> or <space># separator, followed by a string value. A footer’s token MUST use - in place of whitespace characters, e.g., Acked-by (this helps differentiate the footer section from a multi-paragraph body). An exception is made for BREAKING CHANGE, which MAY also be used as a token. A footer’s value MAY contain spaces and newlines, and parsing MUST terminate when the next valid footer token/separator pair is observed.")]
    [JsonPropertyName("footer")]
    public string? Footer { get; set; } // TODO -- Why is it always populating 'Related-Issue: #123" here?

    [JsonVocab(CommitMessageVocabs.BreakingChange, PropertyName = "breaking_change")]
    [Comment("Text to indicate breaking change")]
    public string? BreakingChange { get; set; }

    public override string ToString()
    {
        return
@$"{this.Type}({this.Scope}){this.BreakingChange}: {this.Subject}

{this.Body}

{this.Footer}";
    }
}

public enum CommitMessageType
{
    Default,
    Conventional,
    Emoji,
}

public static class CommitMessageVocabs
{
    public const string BreakingChange = "!";
    public const string Type = "fix | feat | build | chore | ci | docs | style | refactor | perf | test";
    public const string Emoji = "🐛 | 🎉 | 💥 | 🏗️ | 🧹 | 📦 | 📝 | 🎨 | ♻️ | 🚀 | 🧪";
}

public class CommitMessageTranslatorPrompts : IJsonTranslatorPrompts
{
    internal static readonly CommitMessageTranslatorPrompts Default = new();

    public string CreateRepairPrompt(TypeSchema schema, string json, string validationError)
    {
        throw new NotImplementedException();
    }

    public Prompt CreateRequestPrompt(TypeSchema schema, Prompt request, IList<IPromptSection>? preamble = null)
    {
        // ArgumentVerify.ThrowIfNull(request, nameof(request));
        Prompt prompt = new();

        prompt += IntroSection(schema.TypeFullName, schema.Schema);
        // AddContextAndRequest(prompt, request, context);

        return prompt;
    }

    /// <summary>
    /// Adds a section that tells the model that its task to is translate requests into JSON matching the
    /// given schema
    /// </summary>
    /// <param name="typeName"></param>
    /// <param name="schema"></param>
    /// <returns></returns>
    public static PromptSection IntroSection(string typeName, string schema)
    {
        PromptSection introSection = new();
        introSection += $"The result format should be a JSON object of type \"{typeName}\" according to the following TypeScript definitions:\n";
        introSection += $"###\n{schema}###\n";
        return introSection;
    }
}
