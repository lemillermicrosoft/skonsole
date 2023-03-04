// Copyright (c) Microsoft. All rights reserved.

using System.Text.RegularExpressions;

namespace CodeRewriteSkillLib.Utils;

internal static class StringEx
{
    internal static (string _namespace, string imports, string className, List<string> methodDefinitions) SplitCodeFile(this string input)
    {
        // get the metadata like namespace, imports and then class/interfance + methods of the input file

        (string _namespace, string imports, string className, List<string> methodDefinitions) chunks = default;

        // var namespaceMatch = Regex.Match(input, "namespace [a-zA-Z0-9.]+");
        // do this but capture the namespace name
        var namespaceMatch = Regex.Match(input, "namespace ([a-zA-Z0-9.]+)");
        if (namespaceMatch.Success)
        {
            chunks._namespace = namespaceMatch.Groups[1].Value;
        }

        // var importsMatch = Regex.Match(input, "using [a-zA-Z0-9.]+");
        // do this but capture each of the using names
        // var importsMatch = Regex.Match(input, "using ([a-zA-Z0-9.]+)");
        // This doesn't capture all of the usings.  It only captures the first one.
        // var importsMatch = Regex.Match(input, "using ([a-zA-Z0-9.]+);");
        // Capture all of the usings
        var importsMatch = Regex.Matches(input, "using ([a-zA-Z0-9.]+);");
        if (importsMatch.Count > 0)
        {
            // chunks.imports = importsMatch.Groups[1].Value;
            // join all groups after 0
            chunks.imports = string.Join(" ", importsMatch.Skip(1).Select(g => g.Value));
        }

        // var classMatch = Regex.Match(input, "class [a-zA-Z0-9.]+");
        // do this but capture the class name
        // var classMatch = Regex.Match(input, "class ([a-zA-Z0-9.]+)");
        // that doesn't work, need to ignore lines that are comments or start with '//'
        // if (classMatch.Success)
        // {
        //     chunks.className = classMatch.Groups[1].Value;
        // }
        // else
        // {
        //     // var interfaceMatch = Regex.Match(input, "interface [a-zA-Z0-9.]+");
        //     // do this but capture the interface name
        //     var interfaceMatch = Regex.Match(input, "interface ([a-zA-Z0-9.]+)");
        //     if (interfaceMatch.Success)
        //     {
        //         chunks.className = interfaceMatch.Groups[1].Value;
        //     }
        // }

        var classOrInterfaceMatch = Regex.Match(input, @"\b(public|private|internal)\s+(?:abstract|sealed|partial|static)?\s*(interface|class) (\w+)\b");
        if (classOrInterfaceMatch.Success)
        {
            chunks.className = classOrInterfaceMatch.Groups[3].Value;
        }

        // var publicMethodMatches = Regex.Matches(input, "public [a-zA-Z0-9.]+ [a-zA-Z0-9.]+");
        // if (publicMethodMatches.Count > 0)
        // {
        //     chunks.methodDefinitions = new List<string>();
        //     foreach (Match match in publicMethodMatches)
        //     {
        //         chunks.methodDefinitions.Add(match.Value);
        //     }
        // }

        // var privateMethodMatches = Regex.Matches(input, "private [a-zA-Z0-9.]+ [a-zA-Z0-9.]+");
        // if (privateMethodMatches.Count > 0)
        // {
        //     chunks.methodDefinitions = new List<string>();
        //     foreach (Match match in privateMethodMatches)
        //     {
        //         chunks.methodDefinitions.Add(match.Value);
        //     }
        // }

        // var internalMethodMatches = Regex.Matches(input, "internal [a-zA-Z0-9.]+ [a-zA-Z0-9.]+");
        // if (internalMethodMatches.Count > 0)
        // {
        //     chunks.methodDefinitions = new List<string>();
        //     foreach (Match match in internalMethodMatches)
        //     {
        //         chunks.methodDefinitions.Add(match.Value);
        //     }
        // }

        // var methodMatches = Regex.Matches(
        //     input,
        //     @"\b(public|private|internal)?\s*(\w+) (\w+)\((.*?)\)(\s*\{([\s\S]*?)\})?(?=\s*(\b(public|private|internal)?\s*(\w+) (\w+)\((.*?)\)|$))",
        //     RegexOptions.Multiline);
        // The regex pattern to match the class or interface definitions
        string pattern = @"^\s*(?:public|private|protected|internal)\s+(?:abstract|sealed|partial|static)?\s*(?:class|interface)\s+\w+\s*(?:\:\s*\w+(?:\s*,\s*\w+)*)?\s*\{((?:.*)\s*)\}$";

        // The regex options to enable global, multiline, and singleline modes
        RegexOptions options = RegexOptions.Multiline | RegexOptions.Singleline;

        // The regex object to perform the matching
        Regex regex = new Regex(pattern, options);

        // The match collection to store the results
        MatchCollection methodMatches = regex.Matches(input);

        if (methodMatches.Count > 0)
        {
            chunks.methodDefinitions = new List<string>();
            foreach (Match match in methodMatches)
            {
                chunks.methodDefinitions.Add(match.Groups[1].Value);
            }
        }

        return chunks;
    }

    internal static List<(string commit, List<(string fileDiffMetadata, string fileDiff)> fileDiffs)> SplitCommitInfo(this string input)
    {
        var commitMatches = Regex.Matches(input, "^commit [a-z0-9]+", RegexOptions.Multiline);

        var chunks = new List<(string commit, List<(string fileDiffMetadata, string fileDiff)> fileDiffs)>();

        for (var i = 0; i < commitMatches.Count; i++)
        {
            var commitMatch = commitMatches[i];
            var commit = commitMatch.Value;
            var end = i == commitMatches.Count - 1 ? input.Length : commitMatches[i + 1].Index;
            var fileDiffChunks = input.SplitFileInfo(commitMatch.Index, end);

            chunks.Add((commit, fileDiffChunks));
        }

        // if there are no commit matches, assume all a single commit.
        if (chunks.Count == 0)
        {
            var fileDiffChunks = input.SplitFileInfo(0, input.Length);
            chunks.Add(("", fileDiffChunks));
        }

        return chunks;
    }


    internal static List<(string fileDiffMetadata, string fileDiff)> SplitFileInfo(this string input, int inputStart, int inputEnd)//, object commitMatch, object end)
    {
        var fileDiffMatches = Regex.Matches(input, "(^|\n)diff --git[^\n]+\n([^\n]+\n|)index[^\n]+\n---[^\n]+\n\\+\\+\\+[^\n]+\n", RegexOptions.Multiline | RegexOptions.Singleline);

        var fileDiffChunks = new List<(string fileDiffMetadata, string fileDiff)>();

        for (var j = 0; j < fileDiffMatches.Count; j++)
        {
            var fileDiffMatch = fileDiffMatches[j];
            if (fileDiffMatch.Index < inputStart || fileDiffMatch.Index >= inputEnd)
            {
                continue;
            }

            var fileDiffMetadata = fileDiffMatch.Value;
            var nextMatch = j == fileDiffMatches.Count - 1 || (fileDiffMatches[j + 1].Index) >= inputEnd ? inputEnd : fileDiffMatches[j + 1].Index;
            var fileDiff = input[(fileDiffMatch.Index + fileDiffMatch.Length)..nextMatch];
            fileDiffChunks.Add((fileDiffMetadata, fileDiff));
        }

        return fileDiffChunks;
    }


    internal static List<string> SplitByRegex(this string input, string pattern, RegexOptions options)
    {
        var matches = Regex.Matches(input, pattern, options);
        var chunks = new List<string>();
        var start = 0;

        // a chunk is the match and all text after the match before the next match.
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var end = match.Index;// + match.Length;
            if (start == match.Index)
            {
                // leading text before the match or the first match, skip
                continue;
            }
            chunks.Add(input[start..end]);
            start = end;
        }

        // add remaining
        if (start < input.Length)
        {
            chunks.Add(input[start..]);
        }

        return chunks;
    }

}
