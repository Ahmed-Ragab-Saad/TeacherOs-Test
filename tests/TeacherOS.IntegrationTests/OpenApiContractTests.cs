using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TeacherOS.IntegrationTests;

public sealed class OpenApiContractTests : IClassFixture<TeacherOSApiFactory>
{
    private readonly HttpClient _client;

    public OpenApiContractTests(TeacherOSApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
    }

    [Fact]
    public async Task OpenApi_document_contains_cookie_security_scheme_and_correct_endpoints()
    {
        var doc = await GetOpenApiDocumentAsync();
        var components = doc.RootElement.GetProperty("components");
        var securitySchemes = components.GetProperty("securitySchemes");

        Assert.True(securitySchemes.TryGetProperty("CookieAuth", out var cookieAuth));
        Assert.Equal("apiKey", cookieAuth.GetProperty("type").GetString());
        Assert.Equal("cookie", cookieAuth.GetProperty("in").GetString());
        Assert.Equal("__Host-TeacherOS.Auth", cookieAuth.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Login_endpoint_requires_antiforgery_header_and_no_tenant_header()
    {
        var doc = await GetOpenApiDocumentAsync();
        var operation = GetOperation(doc, "/api/auth/login", "post");

        var headers = GetHeaderParameters(operation);
        Assert.Contains("X-CSRF-TOKEN", headers.Keys);
        Assert.DoesNotContain("X-Tenant-Id", headers.Keys);

        var csrfHeader = headers["X-CSRF-TOKEN"];
        Assert.True(csrfHeader.GetProperty("required").GetBoolean());
        Assert.Equal("string", csrfHeader.GetProperty("schema").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Register_endpoint_requires_antiforgery_header_and_no_tenant_header()
    {
        var doc = await GetOpenApiDocumentAsync();
        var operation = GetOperation(doc, "/api/auth/register", "post");

        var headers = GetHeaderParameters(operation);
        Assert.Contains("X-CSRF-TOKEN", headers.Keys);
        Assert.DoesNotContain("X-Tenant-Id", headers.Keys);
    }

    [Fact]
    public async Task Antiforgery_token_endpoint_has_documented_flow_and_no_headers()
    {
        var doc = await GetOpenApiDocumentAsync();
        var operation = GetOperation(doc, "/api/auth/antiforgery", "get");

        Assert.True(operation.TryGetProperty("description", out var description));
        Assert.Contains("X-CSRF-TOKEN", description.GetString());
        Assert.Contains("__Host-TeacherOS.Antiforgery", description.GetString());

        var headers = GetHeaderParameters(operation);
        Assert.Empty(headers);
    }

    [Fact]
    public async Task Current_user_me_endpoint_requires_auth_and_no_antiforgery_or_tenant_headers()
    {
        var doc = await GetOpenApiDocumentAsync();
        var operation = GetOperation(doc, "/api/auth/me", "get");

        var headers = GetHeaderParameters(operation);
        Assert.DoesNotContain("X-CSRF-TOKEN", headers.Keys);
        Assert.DoesNotContain("X-Tenant-Id", headers.Keys);

        Assert.True(operation.TryGetProperty("security", out var security));
        Assert.NotEmpty(security.EnumerateArray());
    }

    [Fact]
    public async Task Tenant_members_list_requires_tenant_header_and_no_antiforgery_header()
    {
        var doc = await GetOpenApiDocumentAsync();
        var operation = GetOperation(doc, "/api/tenants/{tenantId}/members", "get");

        var headers = GetHeaderParameters(operation);
        Assert.Contains("X-Tenant-Id", headers.Keys);
        Assert.DoesNotContain("X-CSRF-TOKEN", headers.Keys);

        var tenantHeader = headers["X-Tenant-Id"];
        Assert.True(tenantHeader.GetProperty("required").GetBoolean());
        Assert.Equal("string", tenantHeader.GetProperty("schema").GetProperty("type").GetString());
        Assert.Equal("uuid", tenantHeader.GetProperty("schema").GetProperty("format").GetString());
    }

    [Fact]
    public async Task Tenant_member_status_patch_requires_both_tenant_and_antiforgery_headers()
    {
        var doc = await GetOpenApiDocumentAsync();
        var operation = GetOperation(doc, "/api/tenants/{tenantId}/members/{membershipId}/status", "patch");

        var headers = GetHeaderParameters(operation);
        Assert.Contains("X-Tenant-Id", headers.Keys);
        Assert.Contains("X-CSRF-TOKEN", headers.Keys);
    }

    [Fact]
    public async Task Tenant_invitations_list_requires_tenant_header_and_no_antiforgery_header()
    {
        var doc = await GetOpenApiDocumentAsync();
        var operation = GetOperation(doc, "/api/tenants/{tenantId}/invitations", "get");

        var headers = GetHeaderParameters(operation);
        Assert.Contains("X-Tenant-Id", headers.Keys);
        Assert.DoesNotContain("X-CSRF-TOKEN", headers.Keys);
    }

    [Fact]
    public async Task Create_tenant_invitation_requires_both_tenant_and_antiforgery_headers()
    {
        var doc = await GetOpenApiDocumentAsync();
        var operation = GetOperation(doc, "/api/tenants/{tenantId}/invitations", "post");

        var headers = GetHeaderParameters(operation);
        Assert.Contains("X-Tenant-Id", headers.Keys);
        Assert.Contains("X-CSRF-TOKEN", headers.Keys);
    }

    [Fact]
    public async Task Revoke_tenant_invitation_requires_both_tenant_and_antiforgery_headers()
    {
        var doc = await GetOpenApiDocumentAsync();
        var operation = GetOperation(doc, "/api/tenants/{tenantId}/invitations/{invitationId}/revoke", "post");

        var headers = GetHeaderParameters(operation);
        Assert.Contains("X-Tenant-Id", headers.Keys);
        Assert.Contains("X-CSRF-TOKEN", headers.Keys);
    }

    [Theory]
    [InlineData("/api/tenants/{tenantId}/branches", "get", false)]
    [InlineData("/api/tenants/{tenantId}/branches", "post", true)]
    [InlineData("/api/tenants/{tenantId}/branches/{branchId}", "get", false)]
    [InlineData("/api/tenants/{tenantId}/branches/{branchId}", "patch", true)]
    [InlineData("/api/tenants/{tenantId}/grade-levels", "get", false)]
    [InlineData("/api/tenants/{tenantId}/grade-levels", "post", true)]
    [InlineData("/api/tenants/{tenantId}/grade-levels/{gradeLevelId}", "get", false)]
    [InlineData("/api/tenants/{tenantId}/grade-levels/{gradeLevelId}", "patch", true)]
    [InlineData("/api/tenants/{tenantId}/students", "get", false)]
    [InlineData("/api/tenants/{tenantId}/students", "post", true)]
    [InlineData("/api/tenants/{tenantId}/students/{studentId}", "get", false)]
    [InlineData("/api/tenants/{tenantId}/students/{studentId}", "patch", true)]
    public async Task Student_module_endpoints_require_tenant_header_and_writes_require_antiforgery(
        string path,
        string method,
        bool isWrite)
    {
        var doc = await GetOpenApiDocumentAsync();
        var operation = GetOperation(doc, path, method);
        var headers = GetHeaderParameters(operation);

        Assert.Contains("X-Tenant-Id", headers.Keys);
        Assert.Equal(isWrite, headers.ContainsKey("X-CSRF-TOKEN"));
    }

    [Fact]
    public async Task Public_invitation_inspect_requires_no_tenant_header_and_no_antiforgery_header()
    {
        var doc = await GetOpenApiDocumentAsync();
        var operation = GetOperation(doc, "/api/tenant-invitations/inspect", "post");

        var headers = GetHeaderParameters(operation);
        Assert.DoesNotContain("X-Tenant-Id", headers.Keys);
        Assert.DoesNotContain("X-CSRF-TOKEN", headers.Keys);
    }

    [Fact]
    public async Task Public_invitation_accept_requires_antiforgery_header_and_no_tenant_header()
    {
        var doc = await GetOpenApiDocumentAsync();
        var operation = GetOperation(doc, "/api/tenant-invitations/accept", "post");

        var headers = GetHeaderParameters(operation);
        Assert.Contains("X-CSRF-TOKEN", headers.Keys);
        Assert.DoesNotContain("X-Tenant-Id", headers.Keys);
    }

    [Fact]
    public async Task Header_parameters_are_never_duplicated_case_insensitively()
    {
        var doc = await GetOpenApiDocumentAsync();
        var paths = doc.RootElement.GetProperty("paths");

        foreach (var path in paths.EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                if (method.Value.TryGetProperty("parameters", out var parameters))
                {
                    var headerNames = parameters.EnumerateArray()
                        .Where(p => p.GetProperty("in").GetString() == "header")
                        .Select(p => p.GetProperty("name").GetString())
                        .ToList();

                    var distinctCount = headerNames.Distinct(StringComparer.OrdinalIgnoreCase).Count();
                    Assert.Equal(distinctCount, headerNames.Count);
                }
            }
        }
    }

    private async Task<JsonDocument> GetOpenApiDocumentAsync()
    {
        using var response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static JsonElement GetOperation(JsonDocument doc, string path, string method)
    {
        var paths = doc.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty(path, out var pathItem), $"Path '{path}' not found in OpenAPI document.");
        Assert.True(pathItem.TryGetProperty(method, out var operation), $"Method '{method}' on path '{path}' not found.");
        return operation;
    }

    private static System.Collections.Generic.Dictionary<string, JsonElement> GetHeaderParameters(JsonElement operation)
    {
        var result = new System.Collections.Generic.Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (!operation.TryGetProperty("parameters", out var parameters))
        {
            return result;
        }

        foreach (var parameter in parameters.EnumerateArray())
        {
            if (parameter.TryGetProperty("in", out var inProp) && inProp.GetString() == "header")
            {
                var name = parameter.GetProperty("name").GetString()!;
                result[name] = parameter;
            }
        }

        return result;
    }
}
