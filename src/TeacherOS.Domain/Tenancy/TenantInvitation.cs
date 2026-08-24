using System;
using TeacherOS.Domain.Common;

namespace TeacherOS.Domain.Tenancy;

public sealed class TenantInvitation : Entity<Guid>, ITenantOwnedEntity
{
    public const int MaxEmailLength = 256;
    public const int MaxTokenHashLength = 128;

    public TenantInvitation(
        Guid id,
        Guid tenantId,
        string email,
        string normalizedEmail,
        string tokenHash,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc,
        Guid? roleId = null,
        DateTimeOffset? acceptedAtUtc = null,
        DateTimeOffset? revokedAtUtc = null)
        : base(ValidateId(id, nameof(id)))
    {
        TenantId = ValidateId(tenantId, nameof(tenantId));
        Email = ValidateEmail(email);
        NormalizedEmail = ValidateNormalizedEmail(normalizedEmail);
        TokenHash = ValidateTokenHash(tokenHash);
        CreatedByUserId = ValidateId(createdByUserId, nameof(createdByUserId));
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = ValidateExpiration(createdAtUtc, expiresAtUtc);
        RoleId = roleId;
        AcceptedAtUtc = acceptedAtUtc;
        RevokedAtUtc = revokedAtUtc;

        if (acceptedAtUtc.HasValue && revokedAtUtc.HasValue)
        {
            throw new ArgumentException("An invitation cannot be both accepted and revoked.");
        }
    }

    public Guid TenantId { get; private set; }
    public string Email { get; private set; }
    public string NormalizedEmail { get; private set; }
    public Guid? RoleId { get; private set; }
    public string TokenHash { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public bool IsAccepted => AcceptedAtUtc.HasValue;
    public bool IsRevoked => RevokedAtUtc.HasValue;

    public bool IsExpired(DateTimeOffset utcNow) =>
        !IsAccepted && !IsRevoked && utcNow >= ExpiresAtUtc;

    public bool IsPending(DateTimeOffset utcNow) =>
        !IsAccepted && !IsRevoked && utcNow < ExpiresAtUtc;

    public void Accept(DateTimeOffset acceptedAtUtc)
    {
        if (IsRevoked)
        {
            throw new InvalidOperationException("A revoked invitation cannot be accepted.");
        }

        if (IsAccepted)
        {
            throw new InvalidOperationException("An accepted invitation cannot be accepted again.");
        }

        if (acceptedAtUtc >= ExpiresAtUtc)
        {
            throw new InvalidOperationException("An expired invitation cannot be accepted.");
        }

        AcceptedAtUtc = acceptedAtUtc;
    }

    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        if (IsAccepted)
        {
            throw new InvalidOperationException("An accepted invitation cannot be revoked.");
        }

        if (IsRevoked)
        {
            throw new InvalidOperationException("An invitation cannot be revoked more than once.");
        }

        RevokedAtUtc = revokedAtUtc;
    }

    private static Guid ValidateId(Guid id, string paramName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", paramName);
        }

        return id;
    }

    private static string ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        var trimmed = email.Trim();
        if (trimmed.Length > MaxEmailLength)
        {
            throw new ArgumentException($"Email cannot exceed {MaxEmailLength} characters.", nameof(email));
        }

        return trimmed;
    }

    private static string ValidateNormalizedEmail(string normalizedEmail)
    {
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new ArgumentException("Normalized email is required.", nameof(normalizedEmail));
        }

        var trimmed = normalizedEmail.Trim();
        if (trimmed.Length > MaxEmailLength)
        {
            throw new ArgumentException($"Normalized email cannot exceed {MaxEmailLength} characters.", nameof(normalizedEmail));
        }

        return trimmed;
    }

    private static string ValidateTokenHash(string tokenHash)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        var trimmed = tokenHash.Trim();
        if (trimmed.Length > MaxTokenHashLength)
        {
            throw new ArgumentException($"Token hash cannot exceed {MaxTokenHashLength} characters.", nameof(tokenHash));
        }

        return trimmed;
    }

    private static DateTimeOffset ValidateExpiration(DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
    {
        if (expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentException("Expiration time must be greater than creation time.", nameof(expiresAtUtc));
        }

        return expiresAtUtc;
    }
}
