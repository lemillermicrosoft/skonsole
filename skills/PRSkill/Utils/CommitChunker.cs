// Copyright (c) Microsoft. All rights reserved.

namespace PRSkill.Utils;

public static class CommitChunker
{
    public static List<string> ChunkCommitInfo(string input, int chunkSize)
    {
        var commits = input.SplitCommitInfo();

        var chunkedInput = new List<string>();
        var currChunk = string.Empty;

        foreach (var commit in commits)
        {
            if (currChunk.Length + commit.commit.Length > chunkSize)
            {
                chunkedInput.Add(currChunk);
                currChunk = string.Empty;
            }

            currChunk += commit.commit;

            if (currChunk.Length > chunkSize)
            {
                // Something is wrong, we should never have a commit message this long.
                throw new Exception("Commit message too long.");
            }

            foreach (var fd in commit.fileDiffs)
            {
                var fileDiff = fd.fileDiff;
                var fileDiffMetadata = fd.fileDiffMetadata;

                if (currChunk.Length + fileDiffMetadata.Length > chunkSize)
                {
                    chunkedInput.Add(currChunk);
                    currChunk = commit.commit;
                }

                currChunk += fileDiffMetadata;

                if (currChunk.Length > chunkSize)
                {
                    throw new Exception("FileDiffMetadata too long.");
                }

                while (fileDiff.Length > 0)
                {
                    var limit = chunkSize - currChunk.Length;
                    if (limit > fileDiff.Length)
                    {
                        currChunk += fileDiff;
                        fileDiff = string.Empty;
                    }
                    else
                    {
                        currChunk += fileDiff[..Math.Max(limit, 0)];
                        fileDiff = fileDiff[Math.Max(limit, 0)..];
                        chunkedInput.Add(currChunk);
                        currChunk = commit.commit + fd.fileDiffMetadata;
                    }
                }
            }
        }
        if (currChunk.Length > 0)
        {
            chunkedInput.Add(currChunk);
        }

        return chunkedInput;
    }
}
