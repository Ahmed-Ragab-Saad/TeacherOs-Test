using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TeacherOS.Api.Authentication;
using Xunit;

namespace TeacherOS.IntegrationTests;

public sealed class HttpCurrentUserTests
{
    [Fact]
    public void Valid_authenticated_identifier_is_exposed()
    {
        var userId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(userId.ToString(), isAuthenticated: true);

        Assert.True(currentUser.IsAuthenticated);
        Assert.Equal(userId, currentUser.UserId);
    }

    [Fact]
    public void Missing_or_malformed_identifier_is_never_replaced_with_an_empty_guid()
    {
        Assert.Null(CreateCurrentUser(null, isAuthenticated: true).UserId);
        Assert.Null(CreateCurrentUser("malformed", isAuthenticated: true).UserId);
        Assert.Null(CreateCurrentUser(Guid.Empty.ToString(), isAuthenticated: true).UserId);
    }

    [Fact]
    public void Unauthenticated_principal_is_reported_as_unauthenticated()
    {
        var currentUser = CreateCurrentUser(Guid.NewGuid().ToString(), isAuthenticated: false);

        Assert.False(currentUser.IsAuthenticated);
    }

    private static HttpCurrentUser CreateCurrentUser(string? userId, bool isAuthenticated)
    {
        var claims = userId is null
            ? Array.Empty<Claim>()
            : [new Claim(ClaimTypes.NameIdentifier, userId)];
        var identity = new ClaimsIdentity(claims, isAuthenticated ? "Test" : null);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };

        return new HttpCurrentUser(new HttpContextAccessor { HttpContext = httpContext });
    }
}
