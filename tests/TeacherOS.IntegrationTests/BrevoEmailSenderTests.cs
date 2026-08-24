using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TeacherOS.Application.Abstractions.Email;
using TeacherOS.Infrastructure.Configuration;
using TeacherOS.Infrastructure.Email;
using Xunit;

namespace TeacherOS.IntegrationTests;

public sealed class BrevoEmailSenderTests
{
    [Fact]
    public async Task Brevo_sender_constructs_correct_request_and_parses_success_response()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new DelegateHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { messageId = "<test-brevo-id@smtp-relay.mailin.fr>" }),
                    System.Text.Encoding.UTF8,
                    "application/json"),
            };
            return response;
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.brevo.com/"),
        };

        var options = Options.Create(new EmailOptions
        {
            Provider = "Brevo",
            BrevoApiKey = "xkeysib-test-dummy-api-key",
            FromName = "TeacherOS Test",
            FromAddress = "noreply@teachos.test",
            InvitationBaseUrl = "https://app.teachos.test",
        });

        var sender = new BrevoEmailSender(httpClient, options, NullLogger<BrevoEmailSender>.Instance);

        var request = new InvitationEmailRequest(
            "recipient@example.com",
            "Awesome Academy",
            "Teacher",
            "test-token-123",
            DateTimeOffset.UtcNow.AddDays(7),
            null);

        var result = await sender.SendInvitationEmailAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("<test-brevo-id@smtp-relay.mailin.fr>", result.ProviderMessageId);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.EndsWith("v3/smtp/email", capturedRequest.RequestUri?.AbsolutePath);
        Assert.True(capturedRequest.Headers.Contains("api-key"));
        Assert.Equal("xkeysib-test-dummy-api-key", capturedRequest.Headers.GetValues("api-key").First());

        Assert.NotNull(capturedBody);
        using var jsonDoc = JsonDocument.Parse(capturedBody);
        var root = jsonDoc.RootElement;
        Assert.Equal("TeacherOS Test", root.GetProperty("sender").GetProperty("name").GetString());
        Assert.Equal("noreply@teachos.test", root.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("recipient@example.com", root.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Contains("Awesome Academy", root.GetProperty("subject").GetString());
        Assert.Contains("test-token-123", root.GetProperty("htmlContent").GetString());
        Assert.Contains("test-token-123", root.GetProperty("textContent").GetString());
        Assert.Contains("Teacher", root.GetProperty("htmlContent").GetString());
    }

    [Fact]
    public async Task Rate_limit_429_response_is_mapped_to_transient_failure_with_retry_after()
    {
        var handler = new DelegateHttpMessageHandler((request, cancellationToken) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(60));
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") };
        var options = Options.Create(new EmailOptions
        {
            BrevoApiKey = "dummy-key",
            FromName = "TeacherOS",
            FromAddress = "noreply@teachos.test",
        });

        var sender = new BrevoEmailSender(httpClient, options, NullLogger<BrevoEmailSender>.Instance);
        var request = new InvitationEmailRequest("r@example.com", "Tenant", null, "tok", DateTimeOffset.UtcNow.AddDays(7), null);

        var result = await sender.SendInvitationEmailAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsTransient);
        Assert.Equal("RateLimitExceeded", result.ErrorCode);
        Assert.Equal(TimeSpan.FromSeconds(60), result.RetryAfter);
    }

    [Fact]
    public async Task Server_500_response_is_mapped_to_transient_failure()
    {
        var handler = new DelegateHttpMessageHandler((request, cancellationToken) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") };
        var options = Options.Create(new EmailOptions
        {
            BrevoApiKey = "dummy-key",
            FromName = "TeacherOS",
            FromAddress = "noreply@teachos.test",
        });

        var sender = new BrevoEmailSender(httpClient, options, NullLogger<BrevoEmailSender>.Instance);
        var request = new InvitationEmailRequest("r@example.com", "Tenant", null, "tok", DateTimeOffset.UtcNow.AddDays(7), null);

        var result = await sender.SendInvitationEmailAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsTransient);
        Assert.Equal("ServerError", result.ErrorCode);
    }

    [Fact]
    public async Task Client_400_response_is_mapped_to_permanent_failure()
    {
        var handler = new DelegateHttpMessageHandler((request, cancellationToken) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") };
        var options = Options.Create(new EmailOptions
        {
            BrevoApiKey = "dummy-key",
            FromName = "TeacherOS",
            FromAddress = "noreply@teachos.test",
        });

        var sender = new BrevoEmailSender(httpClient, options, NullLogger<BrevoEmailSender>.Instance);
        var request = new InvitationEmailRequest("r@example.com", "Tenant", null, "tok", DateTimeOffset.UtcNow.AddDays(7), null);

        var result = await sender.SendInvitationEmailAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsTransient);
        Assert.Equal("ClientError", result.ErrorCode);
    }

    [Fact]
    public async Task Missing_api_key_returns_permanent_failure()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("https://api.brevo.com/") };
        var options = Options.Create(new EmailOptions
        {
            BrevoApiKey = null,
            FromName = "TeacherOS",
            FromAddress = "noreply@teachos.test",
        });

        var sender = new BrevoEmailSender(httpClient, options, NullLogger<BrevoEmailSender>.Instance);
        var request = new InvitationEmailRequest("r@example.com", "Tenant", null, "tok", DateTimeOffset.UtcNow.AddDays(7), null);

        var result = await sender.SendInvitationEmailAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsTransient);
        Assert.Equal("ConfigurationError", result.ErrorCode);
    }

    private sealed class DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
