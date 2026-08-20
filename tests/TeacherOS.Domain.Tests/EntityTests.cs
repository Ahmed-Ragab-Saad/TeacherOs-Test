using System;
using TeacherOS.Domain.Common;
using Xunit;

namespace TeacherOS.Domain.Tests;

public sealed class EntityTests
{
    [Fact]
    public void Entity_preserves_its_identifier()
    {
        var identifier = Guid.NewGuid();

        var entity = new TestEntity(identifier);

        Assert.Equal(identifier, entity.Id);
    }

    private sealed class TestEntity(Guid id) : Entity<Guid>(id);
}
