using Alza.StockService.Config;
using Alza.StockService.Data;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddScoped<IStockDb, PseudoStockDb>();
builder.Services.AddEndpointsApiExplorer(); // TODO: k cemu je toto
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Stock microservice API",
        Version = "v1",
        Description = "Provides information about product availability. It it used by the aggregation service"
    });
});

builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 256000; // Note: Max amount of items
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(10);
});
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection("Cache"));

builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    //app.MapOpenApi();
}

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
app.UseHttpsRedirection();
app.UseAuthorization(); 
app.MapControllers();
app.Run();