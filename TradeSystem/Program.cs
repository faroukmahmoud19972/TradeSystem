using Lamar.Microsoft.DependencyInjection;
using TradeSystem.Strapping;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// Use Lamar for register IOC
builder.Host.UseLamar((context, registry) =>
{
    // register services using Lamar
    //registry.For<InterFace>().Use<Service>();
    registry.IncludeRegistry<TradeSystemRegistry>();
    registry.IncludeRegistry<SqlHelperRegistry>();
    registry.IncludeRegistry<ShardRegistry>();
    //registry.IncludeRegistry<ElasticRegistry>();
});




var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
