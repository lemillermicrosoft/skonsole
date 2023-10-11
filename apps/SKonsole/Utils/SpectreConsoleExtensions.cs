using Spectre.Console;

namespace SKonsole.Utils;
internal static class SpectreConsoleExtensions
{
    public static TextPrompt<T> IsSecret<T>(this TextPrompt<T> obj, bool IsSecret, char? mask = '*')
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        obj.IsSecret = IsSecret;
        obj.Mask = mask;
        return obj;
    }
}
