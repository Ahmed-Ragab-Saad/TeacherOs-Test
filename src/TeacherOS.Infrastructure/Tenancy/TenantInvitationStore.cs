using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TeacherOS.Application.Abstractions.Invitations;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure.Email;
using TeacherOS.Infrastructure.Persistence;

namespace TeacherOS.Infrastructure.Tenancy;

internal sealed class TenantInvitationStore(
    ApplicationDbContext dbContext,
    TimeProvider timeProvider) : ITenantInvitationStore
{
    public Task<TenantInvitation?> GetByIdAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.TenantInvitations
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId, cancellationToken);
    }

    public Task<TenantInvitation?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return dbContext.TenantInvitations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);
    }

    public Task<bool> HasPendingInvitationAsync(
        Guid tenantId,
        string normalizedEmail,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        return dbContext.TenantInvitations
            .AnyAsync(
                i => i.TenantId == tenantId &&
                     i.NormalizedEmail == normalizedEmail &&
                     i.AcceptedAtUtc == null &&
                     i.RevokedAtUtc == null &&
                     i.ExpiresAtUtc > utcNow,
                cancellationToken);
    }

    public async Task<IReadOnlyList<TenantInvitationListItem>> ListByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var utcNow = timeProvider.GetUtcNow();

        var invitations = await dbContext.TenantInvitations
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var roleIds = invitations
            .Where(i => i.RoleId.HasValue)
            .Select(i => i.RoleId!.Value)
            .Distinct()
            .ToList();

        var roleNameMap = roleIds.Count > 0
            ? await dbContext.Roles
                .AsNoTracking()
                .Where(r => roleIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken)
            : new Dictionary<Guid, string>();

        var result = new List<TenantInvitationListItem>(invitations.Count);
        foreach (var inv in invitations)
        {
            var status = inv.IsRevoked
                ? "Revoked"
                : inv.IsAccepted
                    ? "Accepted"
                    : inv.IsExpired(utcNow)
                        ? "Expired"
                        : "Pending";

            string? roleName = null;
            if (inv.RoleId.HasValue)
            {
                roleNameMap.TryGetValue(inv.RoleId.Value, out roleName);
            }

            result.Add(new TenantInvitationListItem(
                inv.Id,
                inv.Email,
                inv.RoleId,
                roleName,
                inv.CreatedAtUtc,
                inv.ExpiresAtUtc,
                inv.AcceptedAtUtc,
                inv.RevokedAtUtc,
                status));
        }

        return result;
    }

    public void Add(
        TenantInvitation invitation,
        Guid outboxMessageId,
        string recipientEmail,
        string protectedToken,
        DateTimeOffset createdAtUtc)
    {
        var outboxMessage = new EmailOutboxMessage(
            outboxMessageId,
            invitation.Id,
            recipientEmail,
            protectedToken,
            createdAtUtc);

        dbContext.TenantInvitations.Add(invitation);
        dbContext.EmailOutboxMessages.Add(outboxMessage);
    }
}
