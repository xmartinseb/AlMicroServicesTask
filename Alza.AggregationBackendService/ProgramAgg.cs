using Alza.AggregationBackendService;
using Alza.AggregationBackendService.Clients;
using Alza.AggregationBackendService.Controllers;
using Alza.HttpExtensions;
using Caches;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using Serilog;
using Serilog.Context;
using System.Threading.RateLimiting;

const string KEY = DevAuthController.KEY; // pouze pro demo

var builder = WebApplication.CreateBuilder(args);

// Add services to the container

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer(); // TODO: k cemu je toto
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
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });

    //options.AddSecurityRequirement(openApiDocument => new Microsoft.OpenApi.OpenApiSecurityRequirement
    //{
    //    {
    //        new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", openApiDocument),
    //        new List<string>()
    //    }
    //});

    //options.AddSecurityRequirement(openApiDoc => new OpenApiSecurityRequirement
    //{
    //    {
    //        new OpenApiSecurityScheme
    //        {
    //            Reference = new OpenApiReference
    //            {
    //                Type = ReferenceType.SecurityScheme,
    //                Id = "Bearer"
    //            }
    //        },
    //        Array.Empty<string>()
    //    }
    //});
});

//{
//    //Reference = new OpenApiReference
//    //{
//    //    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
//    //    Id = "Bearer"
//    //}
//},

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
    options.SizeLimit = 256000; // Note: Max amount of items
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
        partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString(),
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

builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = "https://your-idp"; // Keycloak / Entra / Auth0
        options.Audience = "aggregation-api";   // nebo shared audience
        options.RequireHttpsMetadata = true;
    });
builder.Services.AddAuthorization();

//Debug.WriteLine("VALID OAUTH TOKEN: {0}", TokenGenerator.Generate(KEY));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    //app.MapOpenApi();
}

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
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapPrometheusScrapingEndpoint();
app.MapControllers();
app.Run();



//public static class TokenGenerator
//{
//    public static string Generate(string key)
//    {
//        var secKey = new SymmetricSecurityKey(
//            Encoding.UTF8.GetBytes(key));

//        var creds = new SigningCredentials(secKey, SecurityAlgorithms.HmacSha256);

//        var token = new JwtSecurityToken(
//            issuer: "demo-auth-server",
//            audience: "alza-api",
//            claims:
//            [
//                new Claim(ClaimTypes.Name, "test-user"),
//                new Claim("scope", "product.read")
//            ],
//            expires: DateTime.UtcNow.AddHours(1),
//            signingCredentials: creds);

//        return new JwtSecurityTokenHandler().WriteToken(token);
//    }
//}