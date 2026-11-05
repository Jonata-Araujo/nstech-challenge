using OrderService.Api.Endpoints;
using OrderService.Api.Infrastructure;
using OrderService.Application;
using OrderService.Infrastructure;
using OrderService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapAuthEndpoints();
app.MapOrderEndpoints();

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
    await app.Services.ApplyMigrationsAsync();

await app.RunAsync();

public partial class Program;
