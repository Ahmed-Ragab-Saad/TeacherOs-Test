using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using TeacherOS.Api.Authentication;
using TeacherOS.Api.Invitations;
using TeacherOS.Api.Memberships;
using Xunit;

namespace TeacherOS.IntegrationTests;

public sealed class MembershipAndInvitationEndpointTests : IClassFixture<TeacherOSApiFactory>
{
    private readonly TeacherOSApiFactory _factory;
    private readonly HttpClient _client;

    public MembershipAndInvitationEndpointTests(TeacherOSApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });
    }

    [Fact]
    public async Task Members_endpoint_fails_closed_when_unauthenticated()
    {
        var response = await _client.GetAsync($"/api/tenants/{Guid.NewGuid()}/members", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Invitations_endpoint_fails_closed_when_unauthenticated()
    {
        var response = await _client.GetAsync($"/api/tenants/{Guid.NewGuid()}/invitations", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_invitation_requires_antiforgery_token_and_fails_if_missing()
    {
        await LoginAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/tenants/{TestAuthenticationData.FirstActiveTenantId}/invitations")
        {
            Content = JsonContent.Create(new CreateTenantInvitationRequest("invitee@example.com", null)),
        };
        request.Headers.Add("X-Tenant-Id", TestAuthenticationData.FirstActiveTenantId.ToString());

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Public_invitation_inspection_returns_404_for_unknown_token()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/tenant-invitations/inspect",
            new InspectTenantInvitationRequest("non-existent-token-12345"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Accept_invitation_requires_antiforgery_token_and_fails_if_missing()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tenant-invitations/accept")
        {
            Content = JsonContent.Create(new AcceptTenantInvitationRequest("some-token", "Password123!")),
        };
        // No X-CSRF-TOKEN header sent

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Members_endpoint_fails_closed_when_tenant_header_does_not_match_route()
    {
        await LoginAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/tenants/{TestAuthenticationData.FirstActiveTenantId}/members");
        // Sending different tenant in header vs route
        request.Headers.Add("X-Tenant-Id", TestAuthenticationData.SecondActiveTenantId.ToString());

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_spec_contains_all_membership_and_invitation_endpoints()
    {
        var response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("/api/tenants/{tenantId}/members", json);
        Assert.Contains("/api/tenants/{tenantId}/invitations", json);
        Assert.Contains("/api/tenant-invitations/inspect", json);
        Assert.Contains("/api/tenant-invitations/accept", json);
        Assert.DoesNotContain("/api/tenant-invitations/{token}", json);
    }

    private async Task LoginAsync()
    {
        var antiforgery = await GetAntiforgeryTokenAsync();
        var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(TestAuthenticationData.Email, TestAuthenticationData.Password)),
        };
        loginRequest.Headers.Add("X-CSRF-TOKEN", antiforgery);

        var loginResponse = await _client.SendAsync(loginRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    private async Task<string> GetAntiforgeryTokenAsync()
    {
        var response = await _client.GetAsync("/api/auth/antiforgery", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<AntiforgeryTokenResponse>(TestContext.Current.CancellationToken);
        return content!.Token;
    }
}
