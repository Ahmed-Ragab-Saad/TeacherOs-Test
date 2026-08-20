using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Authentication;
using Xunit;

namespace TeacherOS.Application.Tests;

public sealed class GetCurrentSessionHandlerTests
{
    [Fact]
    public async Task Authenticated_user_receives_the_safe_session_projection()
    {
        var userId = Guid.NewGuid();
        var expected = new CurrentSession(
            userId,
            "teacher@example.com",
            Array.Empty<CurrentTenantMembership>());
        var handler = new GetCurrentSessionHandler(
            new StubCurrentUser(true, userId),
            new StubCurrentSessionReader(expected));

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task Missing_or_malformed_user_identifier_fails_closed()
    {
        var reader = new StubCurrentSessionReader(null);
        var handler = new GetCurrentSessionHandler(new StubCurrentUser(true, null), reader);

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.SessionUnavailable, result.Error);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task Missing_persisted_user_fails_closed()
    {
        var handler = new GetCurrentSessionHandler(
            new StubCurrentUser(true, Guid.NewGuid()),
            new StubCurrentSessionReader(null));

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.SessionUnavailable, result.Error);
    }

    private sealed record StubCurrentUser(bool IsAuthenticated, Guid? UserId) : ICurrentUser;

    private sealed class StubCurrentSessionReader(CurrentSession? session) : ICurrentSessionReader
    {
        internal int CallCount { get; private set; }

        public Task<CurrentSession?> GetAsync(Guid userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(session);
        }
    }
}
