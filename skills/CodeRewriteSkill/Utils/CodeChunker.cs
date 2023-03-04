// Copyright (c) Microsoft. All rights reserved.

namespace CodeRewriteSkillLib.Utils;

public static class CodeChunker
{
    public static List<(string _namespace, string imports, string className, string methodChunk)> ChunkCodeFile(string input, int chunkSize)
    {
        var (_namespace, imports, className, methodDefinitions) = input.SplitCodeFile();

        // generate a list of chunks of methods
        var methodChunks = new List<(string _namespace, string imports, string className, string methodChunk)>();
        var currChunk = string.Empty;

        foreach (var method in methodDefinitions)
        {
            if (currChunk.Length + method.Length > chunkSize)
            {
                methodChunks.Add((_namespace, imports, className, currChunk));
                currChunk = string.Empty;
            }

            currChunk += method;

            if (currChunk.Length > chunkSize)
            {
                // Something is wrong, we should never have a method this long.
                throw new Exception("Method too long.");
            }
        }

        if (currChunk.Length > 0)
        {
            methodChunks.Add((_namespace, imports, className, currChunk));
        }

        return methodChunks;
    }
}
