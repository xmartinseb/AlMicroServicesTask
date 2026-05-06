using Alza.ProductService.Config;
using Alza.ProductService.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddScoped<IProductDb, PseudoProductDb>();
builder.Services.AddEndpointsApiExplorer(); // TODO: k cemu je toto
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
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


app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();