// Copyright (c) Microsoft. All rights reserved.

using System.Text.RegularExpressions;

namespace PRSkill.Utils;

internal static class StringEx
{
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
