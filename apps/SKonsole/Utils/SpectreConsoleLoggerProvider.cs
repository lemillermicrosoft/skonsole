using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace SKonsole.Utils;

public sealed class SpectreConsoleLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new SpectreConsoleLogger(categoryName);
    }

    public void Dispose() { }
}

public sealed class SpectreConsoleLogger : ILogger, IDisposable
{
    private readonly string _categoryName;

    public SpectreConsoleLogger(string categoryName)
    {
        this._categoryName = categoryName;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => this;

    public void Dispose()
    {
        return;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!this.IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);

        if (this._categoryName.EndsWith(".StepwisePlanner"))
        {
            var splitIndex = message.IndexOf(':');
            if (splitIndex == -1)
            {
                AnsiConsole.MarkupLineInterpolated($"[bold red]{message}[/]");
                return;
            }

            var label = message.Substring(0, splitIndex);
            var rest = message.Substring(splitIndex + 1);

            if (label == "Action")
            {
                if (rest.Contains("No action to take"))
                {
                    return;
                }

                AnsiConsole.MarkupLineInterpolated($"[bold blue]{label}:[/] {rest}");
                return;
            }

            // if final answer, color is green
            if (label == "Thought" && rest.StartsWith("Final answer:"))
            {
                AnsiConsole.MarkupLineInterpolated($"[bold green]{label}:[/] {rest}");
                return;
            }

            AnsiConsole.MarkupLineInterpolated($"[bold yellow]{label}:[/] {rest}");
            return;
        }

        AnsiConsole.MarkupLineInterpolated($"[bold]{logLevel}[/]: {this._categoryName}: {message}");
    }
}

public static class SpectreConsoleLoggingExtensions
{
    public static ILoggingBuilder AddSpectreConsole(this ILoggingBuilder builder)
    {
        builder.Services.AddSingleton<ILoggerProvider, SpectreConsoleLoggerProvider>();
        return builder;
    }
}
