using Alza.HttpExtensions;
using Ardalis.GuardClauses;
using System.Net;

namespace Alza.AggregationBackendService.Clients;

/// <summary>
/// Defines base retry logic for all HTTP clients. (max attempts, single request timeout, exponential backoff, etc.)
/// It detects transient errors (timeouts, HTTP 5xx, HTTP 429) and retries the request according to the strategy.
/// </summary>
public abstract class ResilientHttpClientBase
{
    private readonly HttpRetryStrategy httpRetryStrategy;
    private readonly HttpClient client;
    private readonly ILogger logger;

    protected ResilientHttpClientBase(HttpClient client, ILogger logger, HttpRetryStrategy httpRetryStrategy)
    {
        httpRetryStrategy.ThrowIfInvalid();
        this.httpRetryStrategy = httpRetryStrategy;
        this.client = client;
        this.logger = logger;
    }

    protected async Task<T> ExecuteRetryStrategy<T>(Func<HttpClient, Task<T>> httpGetTask, CancellationToken cancellationToken)
    {
        ExponentialBackoff? backoff = httpRetryStrategy.UseExponentialBackoff ? new ExponentialBackoff() : null;

        foreach (int attempt in Enumerable.Range(0, httpRetryStrategy.MaxAttempts))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await httpGetTask(client);
            }
            catch (ExternalServiceTimeoutException)
            {
                if (await Log_IsFinalAttempt(attempt, "Request timeout", statusCode: null))
                    throw;
            }
            catch (ExternalServiceHttpException ex) when (ex.StatusCode.HasValue && IsErrorTransient(ex.StatusCode.Value))
            {
                if (await Log_IsFinalAttempt(attempt, "HTTP response error", ex.StatusCode.Value))
                    throw;
            }
        }

        throw new ExternalServiceException("Http retry strategy failed to retrieve data");

        async Task<bool> Log_IsFinalAttempt(int attempt, string errorKind, HttpStatusCode? statusCode)
        {
            var isFinalAttempt = attempt + 1 >= httpRetryStrategy.MaxAttempts;
            var delay = isFinalAttempt ? TimeSpan.Zero : GetBackoffOrJitterDelay(backoff);

            logger.LogWarning(
                "Attempt {Attempt}/{MaxAttempts}: {ErrorKind}; Status code = {StatusCode}; Delay: {Delay}; Is final attempt: {IsFinalAttempt}",
                attempt + 1,
                httpRetryStrategy.MaxAttempts,
                errorKind,
                statusCode,
                delay,
                isFinalAttempt);

            if (!isFinalAttempt)
                await Task.Delay(delay, cancellationToken);

            return isFinalAttempt;
        }
    }

    private static TimeSpan GetBackoffOrJitterDelay(ExponentialBackoff? backoff)
        => backoff?.GetNextDelay() ?? TimeSpan.FromMilliseconds(Random.Shared.Next(100, 500));

    /// <summary>
    /// Determines whether the specified HTTP status code represents a transient error that may succeed if retried.
    /// </summary>
    /// <remarks>
    /// Transient errors are typically temporary conditions such as timeouts or server
    /// </remarks>
    private static bool IsErrorTransient(HttpStatusCode statusCode)
    => statusCode is HttpStatusCode.RequestTimeout
        or HttpStatusCode.InternalServerError
        or HttpStatusCode.BadGateway
        or HttpStatusCode.ServiceUnavailable
        or HttpStatusCode.TooManyRequests
        or HttpStatusCode.GatewayTimeout;

    sealed class ExponentialBackoff
    {
        private static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(2);

        private TimeSpan delay = TimeSpan.FromMilliseconds(500);
        public TimeSpan GetNextDelay()
        {
            delay *= 2;
            var delayWithJitter = delay + GetRandomJitter();
            return delayWithJitter > MaxDelay 
                ? MaxDelay 
                : delayWithJitter;
        }

        private static TimeSpan GetRandomJitter()
            => TimeSpan.FromMilliseconds(Random.Shared.Next(0, 300));
    }
}

public sealed class HttpRetryStrategy
{
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public bool UseExponentialBackoff { get; init; } = true;

    public void ThrowIfInvalid()
    {
        Guard.Against.Negative(MaxAttempts);
        Guard.Against.NegativeOrZero(RequestTimeout);
    }
}