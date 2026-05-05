using Alza.PricingService.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddScoped<IPricingDb, PseudoPricingDb>();
builder.Services.AddEndpointsApiExplorer(); // TODO: k cemu je toto
builder.Services.AddSwaggerGen();
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
