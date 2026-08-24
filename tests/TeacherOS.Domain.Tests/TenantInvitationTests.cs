using System;
using TeacherOS.Domain.Tenancy;
using Xunit;

namespace TeacherOS.Domain.Tests;

public sealed class TenantInvitationTests
{
    [Fact]
    public void Valid_invitation_is_created_with_pending_status()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var email = "invited@school.local";
        var normalizedEmail = "INVITED@SCHOOL.LOCAL";
        var tokenHash = "dummyhash123456789";
        var createdByUserId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var expiresAt = createdAt.AddDays(7);
        var roleId = Guid.NewGuid();

        var invitation = new TenantInvitation(
            id,
            tenantId,
            email,
            normalizedEmail,
            tokenHash,
            createdByUserId,
            createdAt,
            expiresAt,
            roleId);

        Assert.Equal(id, invitation.Id);
        Assert.Equal(tenantId, invitation.TenantId);
        Assert.Equal(email, invitation.Email);
        Assert.Equal(normalizedEmail, invitation.NormalizedEmail);
        Assert.Equal(tokenHash, invitation.TokenHash);
        Assert.Equal(createdByUserId, invitation.CreatedByUserId);
        Assert.Equal(createdAt, invitation.CreatedAtUtc);
        Assert.Equal(expiresAt, invitation.ExpiresAtUtc);
        Assert.Equal(roleId, invitation.RoleId);
        Assert.Null(invitation.AcceptedAtUtc);
        Assert.Null(invitation.RevokedAtUtc);
        Assert.False(invitation.IsAccepted);
        Assert.False(invitation.IsRevoked);
        Assert.True(invitation.IsPending(createdAt.AddDays(1)));
        Assert.False(invitation.IsExpired(createdAt.AddDays(1)));
    }

    [Fact]
    public void Invitation_accept_sets_accepted_at_utc()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var expiresAt = createdAt.AddDays(7);
        var invitation = CreateSampleInvitation(createdAt, expiresAt);

        var acceptedAt = createdAt.AddHours(2);
        invitation.Accept(acceptedAt);

        Assert.True(invitation.IsAccepted);
        Assert.Equal(acceptedAt, invitation.AcceptedAtUtc);
        Assert.False(invitation.IsPending(acceptedAt));
        Assert.False(invitation.IsExpired(acceptedAt));
    }

    [Fact]
    public void Invitation_revoke_sets_revoked_at_utc()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var expiresAt = createdAt.AddDays(7);
        var invitation = CreateSampleInvitation(createdAt, expiresAt);

        var revokedAt = createdAt.AddHours(1);
        invitation.Revoke(revokedAt);

        Assert.True(invitation.IsRevoked);
        Assert.Equal(revokedAt, invitation.RevokedAtUtc);
        Assert.False(invitation.IsPending(revokedAt));
        Assert.False(invitation.IsExpired(revokedAt));
    }

    [Fact]
    public void Cannot_accept_revoked_invitation()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var invitation = CreateSampleInvitation(createdAt, createdAt.AddDays(7));
        invitation.Revoke(createdAt.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => invitation.Accept(createdAt.AddHours(2)));
    }

    [Fact]
    public void Cannot_revoke_accepted_invitation()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var invitation = CreateSampleInvitation(createdAt, createdAt.AddDays(7));
        invitation.Accept(createdAt.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => invitation.Revoke(createdAt.AddHours(2)));
    }

    [Fact]
    public void Cannot_accept_expired_invitation()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var expiresAt = createdAt.AddDays(1);
        var invitation = CreateSampleInvitation(createdAt, expiresAt);

        Assert.Throws<InvalidOperationException>(() => invitation.Accept(expiresAt.AddHours(1)));
    }

    [Fact]
    public void Cannot_accept_already_accepted_invitation()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var invitation = CreateSampleInvitation(createdAt, createdAt.AddDays(7));
        invitation.Accept(createdAt.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => invitation.Accept(createdAt.AddHours(2)));
    }

    [Fact]
    public void Cannot_revoke_already_revoked_invitation()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var invitation = CreateSampleInvitation(createdAt, createdAt.AddDays(7));
        invitation.Revoke(createdAt.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => invitation.Revoke(createdAt.AddHours(2)));
    }

    [Fact]
    public void Expiration_must_be_after_creation()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => new TenantInvitation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "email@school.local",
            "EMAIL@SCHOOL.LOCAL",
            "hash",
            Guid.NewGuid(),
            now,
            now));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("tenant")]
    [InlineData("user")]
    public void Rejects_empty_required_identifiers(string field)
    {
        var now = DateTimeOffset.UtcNow;
        var id = field == "id" ? Guid.Empty : Guid.NewGuid();
        var tenantId = field == "tenant" ? Guid.Empty : Guid.NewGuid();
        var userId = field == "user" ? Guid.Empty : Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new TenantInvitation(
            id,
            tenantId,
            "email@school.local",
            "EMAIL@SCHOOL.LOCAL",
            "hash",
            userId,
            now,
            now.AddDays(1)));
    }

    private static TenantInvitation CreateSampleInvitation(DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        return new TenantInvitation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "invited@school.local",
            "INVITED@SCHOOL.LOCAL",
            "hash123",
            Guid.NewGuid(),
            createdAt,
            expiresAt);
    }
}
