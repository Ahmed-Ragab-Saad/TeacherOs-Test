using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TeacherOS.IntegrationTests;

public sealed class ApiFoundationTests : IClassFixture<TeacherOSApiFactory>
{
    private readonly TeacherOSApiFactory _factory;
    private readonly HttpClient _client;

    public ApiFoundationTests(TeacherOSApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
    }

    [Fact]
    public async Task Liveness_endpoint_is_healthy_and_returns_a_correlation_id()
    {
        using var response = await _client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Valid_incoming_correlation_id_is_echoed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-ID", "integration-test-123");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("integration-test-123", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task Unknown_api_route_returns_standard_problem_details()
    {
        using var response = await _client.GetAsync("/api/route-that-does-not-exist", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        await using var content = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var document = await JsonDocument.ParseAsync(
            content,
            cancellationToken: TestContext.Current.CancellationToken);

        var root = document.RootElement;
        Assert.Equal("Http.NotFound", root.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));
    }

    [Fact]
    public async Task Root_route_serves_scalar_documentation()
    {
        using var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("scalar", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenApi_document_endpoint_returns_valid_contract_with_auth_routes()
    {
        using var response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        await using var content = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var document = await JsonDocument.ParseAsync(
            content,
            cancellationToken: TestContext.Current.CancellationToken);

        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/auth/register", out var registerPath));
        Assert.True(registerPath.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty("/api/auth/login", out var loginPath));
        Assert.True(loginPath.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty("/api/auth/logout", out var logoutPath));
        Assert.True(logoutPath.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty("/api/auth/me", out var mePath));
        Assert.True(mePath.TryGetProperty("get", out _));

        Assert.True(paths.TryGetProperty("/api/auth/antiforgery", out var antiforgeryPath));
        Assert.True(antiforgeryPath.TryGetProperty("get", out _));

        Assert.False(paths.TryGetProperty("/health/live", out _));
        Assert.False(paths.TryGetProperty("/health/ready", out _));
    }

    [Fact]
    public async Task Documentation_endpoints_are_accessible_in_production_environment()
    {
        using var prodFactory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var prodClient = prodFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using var rootResponse = await prodClient.GetAsync("/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, rootResponse.StatusCode);
        Assert.Equal("text/html", rootResponse.Content.Headers.ContentType?.MediaType);

        using var openApiResponse = await prodClient.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
        Assert.Equal("application/json", openApiResponse.Content.Headers.ContentType?.MediaType);
    }
}
