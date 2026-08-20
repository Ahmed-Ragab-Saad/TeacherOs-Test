namespace TeacherOS.IntegrationTests;

public sealed class ApiFoundationTests : IClassFixture<TeacherOSApiFactory>
{
    private readonly HttpClient _client;

    public ApiFoundationTests(TeacherOSApiFactory factory)
    {
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
    public async Task Unknown_route_returns_standard_problem_details()
    {
        using var response = await _client.GetAsync("/route-that-does-not-exist", TestContext.Current.CancellationToken);

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
}
