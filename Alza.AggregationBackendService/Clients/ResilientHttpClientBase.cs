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
    private readonly CircuitBreakerBase circuitBreaker;
    private readonly HttpClient client;
    private readonly ILogger logger;

    protected ResilientHttpClientBase(HttpClient client, ILogger logger, HttpRetryStrategy httpRetryStrategy, CircuitBreakerBase circuitBreaker)
    {
        httpRetryStrategy.ThrowIfInvalid();
        this.httpRetryStrategy = httpRetryStrategy;
        this.circuitBreaker = circuitBreaker;
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
                return await circuitBreaker.TryExecuteHttpCallAsync(() => httpGetTask(client));
            }
            catch (CircuitBreakerBlocksException ex)
            {
                logger.LogError(ex, "Service has been blocked by its circuit breaker due to recent failures");
                throw;
            }
            catch (ExternalServiceTimeoutException ex)
            {
                if (await Log_IsFinalAttempt(ex, attempt, "Request timeout", statusCode: null))
                    throw;
            }
            catch (ExternalServiceHttpException ex) when (ex.StatusCode.HasValue && IsErrorTransient(ex.StatusCode.Value))
            {
                if (await Log_IsFinalAttempt(ex, attempt, "HTTP response error", ex.StatusCode.Value))
                    throw;
            }
        }

        throw new ExternalServiceException("Http retry strategy failed to retrieve data");

        async Task<bool> Log_IsFinalAttempt(Exception ex, int attempt, string errorKind, HttpStatusCode? statusCode)
        {
            var isFinalAttempt = attempt + 1 >= httpRetryStrategy.MaxAttempts;
            var delay = isFinalAttempt ? TimeSpan.Zero : GetBackoffOrJitterDelay(backoff);

            logger.LogWarning(ex,
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


public abstract class CircuitBreakerBase
{
    private readonly Queue<DateTime> failures = new();
    private DateTime? blockedUntilUtc;
    private readonly Lock sync = new();
    
    public const int FailuresBeforeBreak = 1;
    public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(30);

    public async Task<TEntity> TryExecuteHttpCallAsync<TEntity>(
        Func<Task<TEntity>> httpCall)
    {
        ThrowIfBlocked();

        try
        {
            return await httpCall();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            RegisterFailure();
            throw;
        }
    }

    private void ThrowIfBlocked()
    {
        lock (sync)
        {
            if (blockedUntilUtc is not null && blockedUntilUtc > DateTime.UtcNow)
                throw new CircuitBreakerBlocksException($"Circuit breaker blocks until {blockedUntilUtc:O}");

            if (blockedUntilUtc <= DateTime.UtcNow)
                blockedUntilUtc = null;
        }
    }

    private void RegisterFailure()
    {
        lock (sync)
        {
            var now = DateTime.UtcNow;
            failures.Enqueue(now);

            while (failures.Count > 0 && now - failures.Peek() > FailureWindow)
            {
                failures.Dequeue();
            }

            if (failures.Count >= FailuresBeforeBreak)
            {
                blockedUntilUtc = now + BreakDuration;
                failures.Clear();
            }
        }
    }
}

/// <summary>
/// Is thrown when the circuit breaker is currently blocking calls to the external service due to recent failures.
/// </summary>
public class CircuitBreakerBlocksException(string message) : Exception(message);