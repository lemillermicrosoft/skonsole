using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.Reliability;
using Polly;
using Polly.Retry;

namespace Reliability;

/// <summary>
/// An example of a retry mechanism that retries three times with backoff.
/// </summary>
public class PollyRetryMechanism : IRetryMechanism
{
    public Task ExecuteWithRetryAsync(Func<Task> action, ILogger log)
    {
        var policy = GetPolicy(log);
        return policy.ExecuteAsync(action);
    }

    private static AsyncRetryPolicy GetPolicy(ILogger log)
    {
        return Policy
            .Handle<AIException>(ex => ex.ErrorCode == AIException.ErrorCodes.Throttling)
            .WaitAndRetryAsync(new[]
                {
                    TimeSpan.FromSeconds(8),
                    TimeSpan.FromSeconds(16),
                    TimeSpan.FromSeconds(32)
                },
                (ex, timespan, retryCount, _) => log.LogWarning(ex,
                    "Error executing action [attempt {0} of 3], pausing {1} msecs", retryCount, timespan.TotalMilliseconds));
    }
}
