using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Application.Abstractions.Invitations;

public interface ITenantInvitationStore
{
    Task<TenantInvitation?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<bool> HasPendingInvitationAsync(Guid tenantId, string normalizedEmail, DateTimeOffset utcNow, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantInvitationListItem>> ListByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    void Add(TenantInvitation invitation, Guid outboxMessageId, string recipientEmail, string protectedToken, DateTimeOffset createdAtUtc);
}
