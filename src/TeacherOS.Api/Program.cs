using TeacherOS.Api;
using TeacherOS.Api.Observability;
using TeacherOS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiServices();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseStatusCodePages();
app.UseHttpsRedirection();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
}).ExcludeFromDescription();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
}).ExcludeFromDescription();

app.Run();

public partial class Program
{
}
