namespace PRSkill.Utils;
public class FormatInstructionsProvider
{
    private const string OUTPUT_FORMAT_INSTRUCTIONS_JSON = @"
Output the result as JSON with fields for Title, Summary, and a list of Changes. 
For example, result:
{""Title"": ""My Title"", ""Summary"": ""My Summary"", ""Changes"": [""Change 1"", ""Change 2""]}
";

    private const string OUTPUT_FORMAT_INSTRUCTIONS_TEXT = @"
Output the result as plain text with the title, summary, and changes separated by newlines.
";

    private const string OUTPUT_FORMAT_INSTRUCTIONS_MARKDOWN = @"
Output the result as markdown with the title, summary, and changes separated by newlines.
For example, result:
# My Title
My Summary
- Change 1
- Change 2
";

    private static readonly Dictionary<string, string> _outputFormatInstructions = new()
    {
        { "json", OUTPUT_FORMAT_INSTRUCTIONS_JSON },
        { "text", OUTPUT_FORMAT_INSTRUCTIONS_TEXT },
        { string.Empty, OUTPUT_FORMAT_INSTRUCTIONS_TEXT },
        { "markdown", OUTPUT_FORMAT_INSTRUCTIONS_MARKDOWN }
    };

    public static string GetOutputFormatInstructions(string outputFormat)
    {
        if (_outputFormatInstructions.TryGetValue(outputFormat, out string instructions))
        {
            return instructions;
        }
        else
        {
            throw new ArgumentException($"Unsupported output format: {outputFormat}");
        }
    }
}