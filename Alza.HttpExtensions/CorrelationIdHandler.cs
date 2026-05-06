using Microsoft.AspNetCore.Http;

namespace Alza.HttpExtensions;

public class CorrelationIdHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        const string headerName = "X-Correlation-ID";

        var correlationId = httpContextAccessor.HttpContext?
            .Request.Headers[headerName]
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(correlationId))
            request.Headers.TryAddWithoutValidation(headerName, correlationId);

        return base.SendAsync(request, cancellationToken);
    }
}