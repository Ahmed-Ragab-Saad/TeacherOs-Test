using System;

namespace TeacherOS.Infrastructure.Email;

public sealed class EmailOutboxMessage
{
    public const int DefaultMaxAttempts = 5;
    public const int MaxRecipientEmailLength = 256;
    public const int MaxProviderMessageIdLength = 200;
    public const int MaxLastErrorCodeLength = 100;
    public const int MaxLastErrorDescriptionLength = 500;

    public EmailOutboxMessage()
    {
    }

    public EmailOutboxMessage(
        Guid id,
        Guid tenantInvitationId,
        string recipientEmail,
        string protectedInvitationToken,
        DateTimeOffset createdAtUtc,
        int maxAttempts = DefaultMaxAttempts)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Outbox message identifier is required.", nameof(id));
        }

        if (tenantInvitationId == Guid.Empty)
        {
            throw new ArgumentException("Tenant invitation identifier is required.", nameof(tenantInvitationId));
        }

        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            throw new ArgumentException("Recipient email is required.", nameof(recipientEmail));
        }

        if (string.IsNullOrWhiteSpace(protectedInvitationToken))
        {
            throw new ArgumentException("Protected invitation token is required.", nameof(protectedInvitationToken));
        }

        Id = id;
        TenantInvitationId = tenantInvitationId;
        RecipientEmail = recipientEmail.Trim();
        ProtectedInvitationToken = protectedInvitationToken;
        Status = EmailOutboxStatus.Pending;
        AttemptCount = 0;
        MaxAttempts = maxAttempts > 0 ? maxAttempts : DefaultMaxAttempts;
        CreatedAtUtc = createdAtUtc;
        NextAttemptAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantInvitationId { get; private set; }
    public string RecipientEmail { get; private set; } = string.Empty;
    public string? ProtectedInvitationToken { get; private set; }
    public EmailOutboxStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public DateTimeOffset? LastAttemptAtUtc { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? LastErrorDescription { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public void MarkProcessing(DateTimeOffset utcNow)
    {
        Status = EmailOutboxStatus.Processing;
        LastAttemptAtUtc = utcNow;
        AttemptCount++;
    }

    public void MarkSent(DateTimeOffset utcNow, string? providerMessageId)
    {
        Status = EmailOutboxStatus.Sent;
        SentAtUtc = utcNow;
        ProviderMessageId = Truncate(providerMessageId, MaxProviderMessageIdLength);
        ProtectedInvitationToken = null; // Clear protected token payload on success
        LastErrorCode = null;
        LastErrorDescription = null;
    }

    public void MarkFailed(DateTimeOffset utcNow, string? errorCode, string? errorDescription)
    {
        Status = EmailOutboxStatus.Failed;
        LastAttemptAtUtc = utcNow;
        LastErrorCode = Truncate(errorCode, MaxLastErrorCodeLength);
        LastErrorDescription = Truncate(errorDescription, MaxLastErrorDescriptionLength);
    }

    public void ScheduleRetry(DateTimeOffset nextAttemptAtUtc, string? errorCode, string? errorDescription)
    {
        Status = EmailOutboxStatus.Pending;
        NextAttemptAtUtc = nextAttemptAtUtc;
        LastErrorCode = Truncate(errorCode, MaxLastErrorCodeLength);
        LastErrorDescription = Truncate(errorDescription, MaxLastErrorDescriptionLength);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
