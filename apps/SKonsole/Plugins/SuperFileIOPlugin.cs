
using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;

namespace SKonsole.Plugins;

public class SuperFileIOPlugin
{
    /// <summary>
    /// Read a file
    /// </summary>
    /// <example>
    /// {{file.readAsync $path }} => "hello world"
    /// </example>
    /// <param name="path"> Source file </param>
    /// <returns> File content </returns>
    [SKFunction, Description("Read a file")]
    public async Task<string> ReadAsync([Description("Source file")] string path)
    {
        path = string.IsNullOrEmpty(path) ? Directory.GetCurrentDirectory() : path;
        using var reader = File.OpenText(path);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    [SKFunction, Description("List files in a directory")]
    public string List([Description("Source directory")] string path)
    {
        path = string.IsNullOrEmpty(path) ? Directory.GetCurrentDirectory() : path;
        var files = Directory.GetFiles(path);
        return string.Join("\n", files);
    }

    [SKFunction, Description("List directories in a directory")]
    public string ListDirs([Description("Source directory")] string path)
    {
        path = string.IsNullOrEmpty(path) ? Directory.GetCurrentDirectory() : path;
        var files = Directory.GetFiles(path);
        return string.Join("\n", files);
    }

    [SKFunction, Description("Search files in a directory")]
    public string Search([Description("Source directory")] string path, [Description("Search pattern")] string pattern)
    {
        path = string.IsNullOrEmpty(path) ? Directory.GetCurrentDirectory() : path;
        var files = Directory.GetFiles(path, pattern);
        return string.Join("\n", files);
    }

    [SKFunction, Description("Search files in a directory, recursively")]
    public string SearchAll([Description("Source directory")] string path, [Description("Search pattern")] string pattern)
    {
        path = string.IsNullOrEmpty(path) ? Directory.GetCurrentDirectory() : path;
        var files = Directory.GetFiles(path, pattern, SearchOption.AllDirectories);
        return string.Join("\n", files);
    }

    [SKFunction, Description("Get current directory")]
    public string CurrentDirectory()
    {
        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Write a file
    /// </summary>
    /// <example>
    /// {{file.writeAsync}}
    /// </example>
    /// <param name="path">The destination file path</param>
    /// <param name="content">The file content to write</param>
    /// <returns> An awaitable task </returns>
    [SKFunction, Description("Write a file")]
    public async Task WriteAsync(
        [Description("Destination file")] string path,
        [Description("File content")] string content)
    {
        path = string.IsNullOrEmpty(path) ? Directory.GetCurrentDirectory() : path;
        byte[] text = Encoding.UTF8.GetBytes(content);
        if (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly))
        {
            // Most environments will throw this with OpenWrite, but running inside docker on Linux will not.
            throw new UnauthorizedAccessException($"File is read-only: {path}");
        }

        using var writer = File.OpenWrite(path);
        await writer.WriteAsync(text, 0, text.Length).ConfigureAwait(false);
    }
}
