using Alza.AggregationBackendService;
using Alza.AggregationBackendService.Clients;
using Alza.HttpExtensions;
using Caches;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using Serilog;
using Serilog.Context;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services
    .AddAuthentication("Bearer")
    .AddScheme<AuthenticationSchemeOptions,
        FakeJwtAuthHandler>(
            "Bearer",
            null);

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Aggregation API",
        Version = "v1",
        Description = "Aggregates product, pricing and stock services"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("CacheHitMiss")
            .AddMeter("DownstreamLatency")
            .AddPrometheusExporter();
    });

builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 256000;
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(10);
});
builder.Services.AddSingleton<InMemoryCacheWithSemaphores>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdHandler>();
builder.Services.AddTransient<AuthForwardingHandler>();
builder.Services.AddScoped<IProductAggregatedInfoService, ProductAggregatedInfoService>();

builder.Services.Configure<ProductClientOptions>(builder.Configuration.GetSection("Clients:ProductClient"));
builder.Services.Configure<PricingClientOptions>(builder.Configuration.GetSection("Clients:PricingClient"));
builder.Services.Configure<StockClientOptions>(builder.Configuration.GetSection("Clients:StockClient"));

builder.Services.AddHttpClient<IProductClient, ResilientProductClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<ProductClientOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = options.HttpRetryStrategy.RequestTimeout;
})
    .AddHttpMessageHandler<CorrelationIdHandler>()
    .AddHttpMessageHandler<AuthForwardingHandler>();

builder.Services.AddHttpClient<IPricingClient, ResilientPricingClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<PricingClientOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = options.HttpRetryStrategy.RequestTimeout;
})
    .AddHttpMessageHandler<CorrelationIdHandler>()
    .AddHttpMessageHandler<AuthForwardingHandler>();

builder.Services.AddHttpClient<IStockClient, ResilientStockClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<StockClientOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = options.HttpRetryStrategy.RequestTimeout;
})
    .AddHttpMessageHandler<CorrelationIdHandler>()
    .AddHttpMessageHandler<AuthForwardingHandler>();

builder.Services.AddSingleton<ResilientProductClientCircuitBreaker>();
builder.Services.AddSingleton<ResilientPricingClientCircuitBreaker>();
builder.Services.AddSingleton<ResilientStockClientCircuitBreaker>();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("default", httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString(),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromSeconds(10)
        }));
});

builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddScoped<CachedProductClient>();
builder.Services.AddScoped<CachedPricingClient>();
builder.Services.AddScoped<CachedStockClient>();
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    const string headerName = "X-Correlation-ID";

    var correlationId =
        context.Request.Headers[headerName].FirstOrDefault()
        ?? Guid.NewGuid().ToString();
    Console.WriteLine($"Correlation ID = {correlationId}");

    // přidej do response (debugging)
    context.Request.Headers[headerName] = correlationId;
    context.Response.Headers[headerName] = correlationId;

    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.MapPrometheusScrapingEndpoint();
app.MapControllers();
app.Run();


/// <summary>
/// JUST FOR DEMO PURPOSES!
/// Accepts any request with ANY Authorization header.
/// </summary>
public sealed class FakeJwtAuthHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public FakeJwtAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock)
    { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (string.IsNullOrWhiteSpace(Request.Headers.Authorization.ToString()))
            return Task.FromResult(AuthenticateResult.Fail("Missing token"));
        Claim[] claims = [new Claim(ClaimTypes.Name, "demo-user")];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}