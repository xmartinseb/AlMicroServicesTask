using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;

namespace Alza.HttpExtensions;

public static class HttpClientExtensions
{
    public static async Task<T> UserFriendlyGetObjectAsync<T>(this HttpClient httpClient, string url, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(url, cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;
            else
                throw new ExternalServiceTimeoutException(ex);
        }
        catch (HttpRequestException ex)
        {
            if (ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused or SocketError.TimedOut})
                throw new ExternalServiceException("External service is unavailable", ex);
            if (ex.StatusCode.HasValue)
                throw new ExternalServiceHttpException($"An error occured during the http connection. Http status code: {ex.StatusCode}", ex) { StatusCode = ex.StatusCode.Value };
            
            throw new ExternalServiceException("An error occured during the http connection.", ex);
        }

        if (!response.IsSuccessStatusCode)
            throw new ExternalServiceHttpException($"External service returned error status code: {response.StatusCode}") { StatusCode = response.StatusCode };

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new ExternalServiceException("Read value is NULL");
    }
}

public class ExternalServiceTimeoutException(Exception? inner = null)
    : ExternalServiceException("Timeout", inner);

public class ExternalServiceHttpException(string message, Exception? inner = null)
    : ExternalServiceException(message, inner)
{
    public required HttpStatusCode? StatusCode { get; init; }
}

public class ExternalServiceException(string message, Exception? inner = null)
    : Exception(message, inner);