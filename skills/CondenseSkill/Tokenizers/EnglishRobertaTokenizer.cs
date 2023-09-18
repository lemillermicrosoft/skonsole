using System.Reflection;
using Microsoft.ML.Tokenizers;
using static Microsoft.SemanticKernel.Text.TextChunker;

namespace CondenseSkillLib.Tokenizers;

internal class EnglishRobertaTokenizer
{
    internal static TokenCounter Counter => (string input) =>
    {
        Assembly assembly = typeof(EnglishRobertaTokenizer).Assembly;
        var encoder = assembly.GetManifestResourceStream("encoder.json");
        var vocab = assembly.GetManifestResourceStream("vocab.bpe");
        var dict = assembly.GetManifestResourceStream("dict.txt");

        if (encoder is null || vocab is null || dict is null)
        {
            throw new FileNotFoundException("Missing required resources");
        }

        EnglishRoberta model = new(encoder, vocab, dict);

        model.AddMaskSymbol();

        Tokenizer tokenizer = new(model, new RobertaPreTokenizer());
        var tokens = tokenizer.Encode(input).Tokens;

        return tokens.Count;
    };
}
