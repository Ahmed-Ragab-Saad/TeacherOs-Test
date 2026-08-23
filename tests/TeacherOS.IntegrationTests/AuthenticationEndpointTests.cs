using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TeacherOS.IntegrationTests;

public sealed class AuthenticationEndpointTests : IClassFixture<TeacherOSApiFactory>
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private readonly TeacherOSApiFactory _factory;

    public AuthenticationEndpointTests(TeacherOSApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unknown_user_and_wrong_password_return_the_same_generic_failure()
    {
        using var unknownClient = CreateClient();
        using var wrongPasswordClient = CreateClient();

        using var unknownResponse = await LoginAsync(
            unknownClient,
            "unknown@example.com",
            TestAuthenticationData.Password);
        using var wrongPasswordResponse = await LoginAsync(
            wrongPasswordClient,
            TestAuthenticationData.Email,
            "wrong-password");

        Assert.Equal(HttpStatusCode.Unauthorized, unknownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
        Assert.Equal("Authentication.InvalidCredentials", await ReadProblemCodeAsync(unknownResponse));
        Assert.Equal("Authentication.InvalidCredentials", await ReadProblemCodeAsync(wrongPasswordResponse));
    }

    [Fact]
    public async Task Login_without_antiforgery_token_is_rejected()
    {
        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { TestAuthenticationData.Email, TestAuthenticationData.Password },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Antiforgery.ValidationFailed", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task Register_without_antiforgery_token_is_rejected()
    {
        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { Email = "new@example.com", Password = "Password123!", TenantName = "New Academy" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Antiforgery.ValidationFailed", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task Register_with_valid_request_returns_created_and_safe_payload()
    {
        using var client = CreateClient();
        var antiforgery = await GetAntiforgeryTokenAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new
            {
                Email = "fresh-teacher@example.com",
                Password = "ValidPassword123!",
                TenantName = "Fresh Academy",
            }),
        };
        request.Headers.Add("X-CSRF-TOKEN", antiforgery.Token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        Assert.NotEqual(Guid.Empty, root.GetProperty("userId").GetGuid());
        Assert.Equal("fresh-teacher@example.com", root.GetProperty("email").GetString());
        Assert.NotEqual(Guid.Empty, root.GetProperty("tenantId").GetGuid());

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_conflict()
    {
        using var client = CreateClient();
        var antiforgery = await GetAntiforgeryTokenAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new
            {
                Email = TestAuthenticationData.Email,
                Password = "ValidPassword123!",
                TenantName = "Duplicate Academy",
            }),
        };
        request.Headers.Add("X-CSRF-TOKEN", antiforgery.Token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Authentication.DuplicateEmail", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task Register_endpoint_is_rate_limited()
    {
        await using var isolatedFactory = new TeacherOSApiFactory();
        using var client = CreateClient(isolatedFactory);
        var antiforgery = await GetAntiforgeryTokenAsync(client);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
            {
                Content = JsonContent.Create(new
                {
                    Email = $"user{attempt}@example.com",
                    Password = "ValidPassword123!",
                    TenantName = "Rate Limit Academy",
                }),
            };
            request.Headers.Add("X-CSRF-TOKEN", antiforgery.Token);

            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        using var rejectedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new
            {
                Email = "overflow@example.com",
                Password = "ValidPassword123!",
                TenantName = "Rate Limit Academy",
            }),
        };
        rejectedRequest.Headers.Add("X-CSRF-TOKEN", antiforgery.Token);

        using var rejected = await client.SendAsync(rejectedRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("Authentication.RateLimitExceeded", await ReadProblemCodeAsync(rejected));
    }

    [Fact]
    public async Task Cookie_session_and_tenant_selection_fail_closed_end_to_end()
    {
        using var client = CreateClient();

        using (var unauthenticated = await client.GetAsync(
            "/api/auth/me",
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
            Assert.Null(unauthenticated.Headers.Location);
        }

        var antiforgery = await GetAntiforgeryTokenAsync(client);
        var antiforgeryCookie = Assert.IsType<string>(antiforgery.SetCookie);
        Assert.Contains("__Host-TeacherOS.Antiforgery=", antiforgeryCookie, StringComparison.Ordinal);
        Assert.Contains("secure", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);

        using (var login = await PostLoginAsync(
            client,
            TestAuthenticationData.Email,
            TestAuthenticationData.Password,
            antiforgery.Token))
        {
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            var cookie = Assert.Single(
                login.Headers.GetValues("Set-Cookie"),
                value => value.StartsWith("__Host-TeacherOS.Auth=", StringComparison.Ordinal));
            Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);

            var body = await login.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
        }

        using (var current = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, current.StatusCode);
            using var document = await ReadJsonAsync(current);
            var root = document.RootElement;
            Assert.Equal(TestAuthenticationData.UserId, root.GetProperty("userId").GetGuid());
            Assert.Equal(TestAuthenticationData.Email, root.GetProperty("email").GetString());
            Assert.Equal(JsonValueKind.Null, root.GetProperty("selectedTenantId").ValueKind);

            var memberships = root.GetProperty("memberships").EnumerateArray().ToArray();
            Assert.Equal(3, memberships.Length);
            Assert.Contains(
                memberships,
                membership =>
                    membership.GetProperty("tenantId").GetGuid() == TestAuthenticationData.SuspendedTenantId &&
                    membership.GetProperty("membershipStatus").GetString() == "Suspended");
        }

        await AssertSelectedTenantAsync(client, TestAuthenticationData.FirstActiveTenantId);
        await AssertSelectedTenantAsync(client, TestAuthenticationData.SecondActiveTenantId);

        using (var withoutSelector = await client.GetAsync(
            "/api/auth/me",
            TestContext.Current.CancellationToken))
        {
            using var document = await ReadJsonAsync(withoutSelector);
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("selectedTenantId").ValueKind);
        }

        await AssertTenantDeniedAsync(client, TestAuthenticationData.SuspendedTenantId);
        await AssertTenantDeniedAsync(client, TestAuthenticationData.NonMemberTenantId);

        using (var malformedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me"))
        {
            malformedRequest.Headers.Add(TenantHeaderName, "not-a-guid");
            using var malformed = await client.SendAsync(
                malformedRequest,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
            Assert.Equal("Tenancy.InvalidSelector", await ReadProblemCodeAsync(malformed));
        }

        using (var multipleRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me"))
        {
            multipleRequest.Headers.TryAddWithoutValidation(
                TenantHeaderName,
                [
                    TestAuthenticationData.FirstActiveTenantId.ToString(),
                    TestAuthenticationData.SecondActiveTenantId.ToString(),
                ]);
            using var multiple = await client.SendAsync(
                multipleRequest,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, multiple.StatusCode);
            Assert.Equal("Tenancy.InvalidSelector", await ReadProblemCodeAsync(multiple));
        }

        using (var missingAntiforgery = await client.PostAsync(
            "/api/auth/logout",
            new StringContent(string.Empty, Encoding.UTF8),
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.BadRequest, missingAntiforgery.StatusCode);
            Assert.Equal("Antiforgery.ValidationFailed", await ReadProblemCodeAsync(missingAntiforgery));
        }

        var authenticatedAntiforgery = await GetAntiforgeryTokenAsync(client);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout"))
        {
            request.Headers.Add("X-CSRF-TOKEN", authenticatedAntiforgery.Token);
            using var logout = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }

        using (var afterLogout = await client.GetAsync(
            "/api/auth/me",
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
            Assert.Null(afterLogout.Headers.Location);
        }
    }

    [Fact]
    public async Task Login_endpoint_is_rate_limited()
    {
        await using var isolatedFactory = new TeacherOSApiFactory();
        using var client = CreateClient(isolatedFactory);
        var antiforgery = await GetAntiforgeryTokenAsync(client);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var response = await PostLoginAsync(
                client,
                "unknown@example.com",
                "wrong-password",
                antiforgery.Token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var rejected = await PostLoginAsync(
            client,
            "unknown@example.com",
            "wrong-password",
            antiforgery.Token);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("Authentication.RateLimitExceeded", await ReadProblemCodeAsync(rejected));
    }

    private HttpClient CreateClient() => CreateClient(_factory);

    private static HttpClient CreateClient(TeacherOSApiFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });
    }

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var antiforgery = await GetAntiforgeryTokenAsync(client);
        return await PostLoginAsync(client, email, password, antiforgery.Token);
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string email,
        string password,
        string antiforgeryToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { Email = email, Password = password }),
        };
        request.Headers.Add("X-CSRF-TOKEN", antiforgeryToken);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<(string Token, string? SetCookie)> GetAntiforgeryTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync(
            "/api/auth/antiforgery",
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = await ReadJsonAsync(response);
        var token = document.RootElement.GetProperty("token").GetString();
        var cookie = response.Headers.TryGetValues("Set-Cookie", out var setCookies)
            ? setCookies.SingleOrDefault(
                value => value.StartsWith("__Host-TeacherOS.Antiforgery=", StringComparison.Ordinal))
            : null;

        return (Assert.IsType<string>(token), cookie);
    }

    private static async Task AssertSelectedTenantAsync(HttpClient client, Guid tenantId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add(TenantHeaderName, tenantId.ToString());
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        Assert.Equal(tenantId, document.RootElement.GetProperty("selectedTenantId").GetGuid());
    }

    private static async Task AssertTenantDeniedAsync(HttpClient client, Guid tenantId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add(TenantHeaderName, tenantId.ToString());
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Tenancy.AccessDenied", await ReadProblemCodeAsync(response));
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = await ReadJsonAsync(response);
        return document.RootElement.GetProperty("code").GetString();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var content = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        return await JsonDocument.ParseAsync(
            content,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
