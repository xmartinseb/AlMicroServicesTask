using Alza.ProductService.Config;
using Alza.ProductService.Data;
using Caches;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Context;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddScoped<IProductDb, PseudoProductDb>();
builder.Services.AddEndpointsApiExplorer(); // TODO: k cemu je toto
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Product microservice API",
        Version = "v1",
        Description = "Provides details about products. It it used by the aggregation service"
    });
});
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection("Cache"));
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 256000; // Note: Max amount of items
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(10);
});
builder.Services.AddSingleton<InMemoryCacheWithSemaphores>();

builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));
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

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.UseRateLimiter();
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
//app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();
app.Run();