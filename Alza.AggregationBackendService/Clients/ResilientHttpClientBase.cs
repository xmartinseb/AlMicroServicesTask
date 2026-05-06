using Alza.HttpExtensions;
using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Alza.AggregationBackendService.Clients;

public abstract class ResilientHttpClientBase
{
    private readonly HttpRetryStrategy httpRetryStrategy;
    private readonly HttpClient client;

    protected ResilientHttpClientBase(HttpClient client, HttpRetryStrategy httpRetryStrategy)
    {
        httpRetryStrategy.ThrowIfInvalid();
        this.httpRetryStrategy = httpRetryStrategy;
        this.client = client;
    }

    protected async Task<T> ExecuteRetryStrategy<T>(Func<HttpClient, Task<T>> httpGetTask, CancellationToken cancellationToken)
    {
        ExponentialBackoff? backoff = httpRetryStrategy.UseExponentialBackoff ? new ExponentialBackoff() : null;

        foreach (int attempt in Enumerable.Range(0, httpRetryStrategy.MaxRetryAttempts))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await httpGetTask(client);
            }
            catch (ExternalServiceTimeoutException ex) 
            {
                await Delay(backoff, cancellationToken);
            }
            catch (ExternalServiceHttpException ex) when (ex.StatusCode.HasValue && IsErrorTransient(ex.StatusCode.Value))
            {
                await Delay(backoff, cancellationToken);
            }
        }

        throw new ExternalServiceException("Http retry strategy failed to retrieve data");
    }

    private async Task Delay(ExponentialBackoff? backoff, CancellationToken cancellationToken) 
        => await Task.Delay(backoff?.GetNextDelay() ?? TimeSpan.FromSeconds(1), cancellationToken);

    private static bool IsErrorTransient(HttpStatusCode statusCode)
    => statusCode is HttpStatusCode.RequestTimeout
        or HttpStatusCode.InternalServerError
        or HttpStatusCode.BadGateway
        or HttpStatusCode.ServiceUnavailable
        or HttpStatusCode.TooManyRequests
        or HttpStatusCode.GatewayTimeout;

    sealed class ExponentialBackoff
    {
        private TimeSpan delay = TimeSpan.FromMilliseconds(500);
        public TimeSpan GetNextDelay()
        {
            delay = delay * 2 + GetRandomJitter();
            return delay;
        }

        private static TimeSpan GetRandomJitter()
            => TimeSpan.FromMilliseconds(Random.Shared.Next(0, 300));
    }
}

public sealed class HttpRetryStrategy
{
    public int MaxRetryAttempts { get; init; } = 3;
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public bool UseExponentialBackoff { get; init; } = true;

    public void ThrowIfInvalid()
    {
        Guard.Against.Negative(MaxRetryAttempts);
        Guard.Against.NegativeOrZero(RequestTimeout);
    }
}