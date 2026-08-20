using System;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Authentication;
using Xunit;

namespace TeacherOS.Application.Tests;

public sealed class LoginHandlerTests
{
    [Fact]
    public async Task Valid_credentials_return_the_authenticated_user_and_preserve_the_password()
    {
        var userId = Guid.NewGuid();
        var authenticator = new StubIdentityAuthenticator(
            new IdentityAuthenticationResult(userId, "teacher@example.com"));
        var handler = new LoginHandler(authenticator);

        var result = await handler.HandleAsync(
            new LoginCommand("  TEACHER@example.com  ", " password with spaces "),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value.UserId);
        Assert.Equal("TEACHER@example.com", authenticator.Email);
        Assert.Equal(" password with spaces ", authenticator.Password);
    }

    [Fact]
    public async Task Missing_credentials_fail_before_calling_identity()
    {
        var authenticator = new StubIdentityAuthenticator(null);
        var handler = new LoginHandler(authenticator);

        var result = await handler.HandleAsync(
            new LoginCommand(" ", null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.CredentialsRequired, result.Error);
        Assert.Equal(0, authenticator.CallCount);
    }

    [Fact]
    public async Task Rejected_credentials_return_the_single_generic_failure()
    {
        var handler = new LoginHandler(new StubIdentityAuthenticator(null));

        var result = await handler.HandleAsync(
            new LoginCommand("unknown@example.com", "wrong-password"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.InvalidCredentials, result.Error);
    }

    private sealed class StubIdentityAuthenticator(IdentityAuthenticationResult? result)
        : IIdentityAuthenticator
    {
        internal int CallCount { get; private set; }

        internal string? Email { get; private set; }

        internal string? Password { get; private set; }

        public Task<IdentityAuthenticationResult?> AuthenticateAsync(
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            Email = email;
            Password = password;
            return Task.FromResult(result);
        }
    }
}
