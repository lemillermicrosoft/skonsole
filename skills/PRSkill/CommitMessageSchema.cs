using System.Text.Json.Serialization;
using Microsoft.TypeChat.Schema;

namespace PRSkill;

public abstract partial class CommitMessage
{
    [JsonPropertyName("message")]
    public CommitItem? Message { get; set; }
}

[JsonPolymorphic]
[JsonDerivedType(typeof(BasicCommitMessage), typeDiscriminator: nameof(BasicCommitMessage))]
[JsonDerivedType(typeof(ConventionalCommitMessage), typeDiscriminator: nameof(ConventionalCommitMessage))]
public abstract partial class CommitItem
{
}

[Comment("A basic commit message with a title and body.")]
public partial class BasicCommitMessage : CommitItem
{
    public string? Title { get; set; }
    public string? Body { get; set; }
}

[Comment("A basic commit message with a title and body.")]
public partial class EmojiCommitMessage : CommitItem
{
    public string? Emoji { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
}

public partial class ConventionalCommitMessage : CommitItem
{
    [JsonVocab(CommitMessageVocabs.Type, PropertyName = "type")]
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    public string? Scope { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public string? Footer { get; set; }

    public override string ToString()
    {
        return
@$"{this.Type}({this.Scope}): {this.Subject}

{this.Body}

{this.Footer}";
    }
}

public static class CommitMessageVocabs
{
    public const string Type = "fix | feat | BREAKING CHANGE | build | chore | ci | docs | style | refactor | perf | test";
}
