using Scalar.AspNetCore;
using TeacherOS.Api;
using TeacherOS.Api.Authentication;
using TeacherOS.Api.Invitations;
using TeacherOS.Api.Memberships;
using TeacherOS.Api.Observability;
using TeacherOS.Api.Students;
using TeacherOS.Api.Tenancy;
using TeacherOS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiServices();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();
app.UseAntiforgery();

app.MapOpenApi();
app.MapScalarApiReference("");

app.MapAuthenticationEndpoints();
app.MapMembershipEndpoints();
app.MapInvitationEndpoints();
app.MapStudentEndpoints();

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
