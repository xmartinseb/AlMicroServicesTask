using Alza.AggregationBackendService.Clients;
using Alza.HttpExtensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Context;
using System.Text;
using System.Threading.RateLimiting;

const string KEY = "super_secret_dev_key_12345"; // pouze pro demo

var builder = WebApplication.CreateBuilder(args);

// Add services to the container

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer(); // TODO: k cemu je toto
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdHandler>();

builder.Services.Configure<ProductClientOptions>(builder.Configuration.GetSection("Clients:ProductClient"));
builder.Services.Configure<PricingClientOptions>(builder.Configuration.GetSection("Clients:PricingClient"));
builder.Services.Configure<StockClientOptions>(builder.Configuration.GetSection("Clients:StockClient"));

builder.Services.AddHttpClient<IProductClient, ProductClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<ProductClientOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = options.HttpRetryStrategy.RequestTimeout;
}).AddHttpMessageHandler<CorrelationIdHandler>();

builder.Services.AddHttpClient<IPricingClient, PricingClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<PricingClientOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = options.HttpRetryStrategy.RequestTimeout;
}).AddHttpMessageHandler<CorrelationIdHandler>();

builder.Services.AddHttpClient<IStockClient, StockClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<StockClientOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = options.HttpRetryStrategy.RequestTimeout;
}).AddHttpMessageHandler<CorrelationIdHandler>();

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


//builder.Services.AddSwaggerGen(options =>
//{
//    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
//    {
//        Name = "Authorization",
//        Type = SecuritySchemeType.Http,
//        Scheme = "bearer",
//        BearerFormat = "JWT",
//        In = ParameterLocation.Header,
//        Description = "Enter JWT token"
//    });

//    options.AddSecurityRequirement(new OpenApiSecurityRequirement
//    {
//        {
//            new OpenApiSecurityScheme
//            {
//                Reference = new Microsoft.OpenApi.Models.OpenApiReference
//                {
//                    Type = ReferenceType.SecurityScheme,
//                    Id = "Bearer"
//                }
//            },
//            Array.Empty<string>()
//        }
//    });
//});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddOpenApi();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "demo-auth-server",

            ValidateAudience = true,
            ValidAudience = "alza-api",

            ValidateLifetime = true,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(KEY))
        };
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