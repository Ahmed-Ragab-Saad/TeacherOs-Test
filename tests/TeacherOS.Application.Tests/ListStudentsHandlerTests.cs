using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Students;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Students;
using TeacherOS.Domain.Students;
using Xunit;

namespace TeacherOS.Application.Tests;

public sealed class ListStudentsHandlerTests
{
    [Fact]
    public async Task Authenticated_member_of_the_selected_tenant_receives_students()
    {
        var tenantId = Guid.NewGuid();
        var expected = new[]
        {
            new StudentListItem(Guid.NewGuid(), "ST-2026-001", "Mona Ali", "123", Guid.NewGuid(), "Main", Guid.NewGuid(), "Grade 1", StudentStatus.Active, new DateOnly(2026, 8, 24), null, null),
        };
        var reader = new StubStudentReader(expected);
        var handler = new ListStudentsHandler(new StubCurrentUser(true), new StubTenantContext(tenantId), reader);

        var result = await handler.HandleAsync(new ListStudentsQuery(tenantId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task Unauthenticated_request_is_rejected_without_reading_students()
    {
        var tenantId = Guid.NewGuid();
        var reader = new StubStudentReader([]);
        var handler = new ListStudentsHandler(new StubCurrentUser(false), new StubTenantContext(tenantId), reader);

        var result = await handler.HandleAsync(new ListStudentsQuery(tenantId), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("Authentication.Unauthorized", result.Error.Code);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task Different_or_missing_tenant_context_is_rejected_without_reading_students()
    {
        var reader = new StubStudentReader([]);
        var handler = new ListStudentsHandler(new StubCurrentUser(true), new StubTenantContext(null), reader);

        var result = await handler.HandleAsync(new ListStudentsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenancy.AccessDenied", result.Error.Code);
        Assert.Equal(0, reader.CallCount);
    }

    private sealed record StubCurrentUser(bool IsAuthenticated) : ICurrentUser
    {
        public Guid? UserId => null;
    }

    private sealed class StubTenantContext(Guid? tenantId) : ITenantContext
    {
        public bool IsAvailable => tenantId.HasValue;

        public Guid TenantId => tenantId ?? throw new InvalidOperationException();

        public void Establish(Guid tenantId) => throw new NotSupportedException();
    }

    private sealed class StubStudentReader(IReadOnlyList<StudentListItem> students) : IStudentReader
    {
        internal int CallCount { get; private set; }

        public Task<IReadOnlyList<StudentListItem>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(students);
        }
    }
}
