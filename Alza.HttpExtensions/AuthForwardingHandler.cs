using Microsoft.AspNetCore.Http;

namespace Alza.HttpExtensions;

public sealed class AuthForwardingHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = accessor.HttpContext?
            .Request
            .Headers["Authorization"]
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(token))
            request.Headers.TryAddWithoutValidation("Authorization", token);
        
        return base.SendAsync(request, cancellationToken);
    }
}