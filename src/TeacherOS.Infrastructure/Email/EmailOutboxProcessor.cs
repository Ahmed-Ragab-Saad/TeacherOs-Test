using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeacherOS.Application.Abstractions.Email;
using TeacherOS.Application.Abstractions.Invitations;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure.Persistence;

namespace TeacherOS.Infrastructure.Email;

internal sealed class EmailOutboxProcessor(
    ApplicationDbContext dbContext,
    ITransactionalEmailSender emailSender,
    IInvitationTokenService tokenService,
    TimeProvider timeProvider,
    ILogger<EmailOutboxProcessor> logger) : IEmailOutboxProcessor
{
    public async Task<bool> TryDispatchImmediatelyAsync(
        Guid outboxMessageId,
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var message = await dbContext.EmailOutboxMessages
            .FirstOrDefaultAsync(m => m.Id == outboxMessageId, cancellationToken);

        if (message is null || message.Status != EmailOutboxStatus.Pending)
        {
            return false;
        }

        var invitation = await dbContext.TenantInvitations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == message.TenantInvitationId, cancellationToken);

        var utcNow = timeProvider.GetUtcNow();

        if (invitation is null || invitation.IsRevoked || invitation.IsExpired(utcNow))
        {
            message.MarkFailed(utcNow, "InvitationInvalid", "Invitation was revoked, expired, or not found.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == invitation.TenantId, cancellationToken);

        var tenantName = tenant?.Name ?? "Tenant";

        string? roleName = null;
        if (invitation.RoleId.HasValue)
        {
            var role = await dbContext.Roles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == invitation.RoleId.Value, cancellationToken);
            roleName = role?.Name;
        }

        var request = new InvitationEmailRequest(
            message.RecipientEmail,
            tenantName,
            roleName,
            rawToken,
            invitation.ExpiresAtUtc,
            null);

        message.MarkProcessing(utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dispatchResult = await emailSender.SendInvitationEmailAsync(request, cancellationToken);

        if (dispatchResult.IsSuccess)
        {
            message.MarkSent(utcNow, dispatchResult.ProviderMessageId);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Invitation email dispatched immediately for OutboxMessageId={OutboxMessageId}, InvitationId={InvitationId}",
                message.Id,
                message.TenantInvitationId);
            return true;
        }

        logger.LogWarning(
            "Immediate dispatch failed for OutboxMessageId={OutboxMessageId}, ErrorCode={ErrorCode}",
            message.Id,
            dispatchResult.ErrorCode);

        if (!dispatchResult.IsTransient || message.AttemptCount >= message.MaxAttempts)
        {
            message.MarkFailed(utcNow, dispatchResult.ErrorCode, dispatchResult.ErrorDescription);
        }
        else
        {
            var retryDelay = dispatchResult.RetryAfter ??
                             TimeSpan.FromSeconds(Math.Pow(2, message.AttemptCount) * 15);
            message.ScheduleRetry(utcNow.Add(retryDelay), dispatchResult.ErrorCode, dispatchResult.ErrorDescription);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return false;
    }

    public async Task<int> ProcessPendingOutboxMessagesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var utcNow = timeProvider.GetUtcNow();

        // 1. Claim up to 10 pending messages in a concurrency-safe manner using optimistic concurrency
        var candidates = await dbContext.EmailOutboxMessages
            .Where(m => m.Status == EmailOutboxStatus.Pending && m.NextAttemptAtUtc <= utcNow)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return 0;
        }

        var claimedMessages = new List<EmailOutboxMessage>();

        foreach (var candidate in candidates)
        {
            candidate.MarkProcessing(utcNow);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                claimedMessages.Add(candidate);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another worker claimed this message, skip
                dbContext.Entry(candidate).State = EntityState.Detached;
            }
        }

        var processedCount = 0;

        foreach (var message in claimedMessages)
        {
            try
            {
                var invitation = await dbContext.TenantInvitations
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(i => i.Id == message.TenantInvitationId, cancellationToken);

                if (invitation is null || invitation.IsRevoked || invitation.IsExpired(utcNow))
                {
                    message.MarkFailed(utcNow, "InvitationInvalid", "Invitation was revoked, expired, or not found.");
                    await dbContext.SaveChangesAsync(cancellationToken);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(message.ProtectedInvitationToken))
                {
                    message.MarkFailed(utcNow, "MissingToken", "Protected invitation token is missing.");
                    await dbContext.SaveChangesAsync(cancellationToken);
                    continue;
                }

                string rawToken;
                try
                {
                    rawToken = tokenService.UnprotectToken(message.ProtectedInvitationToken);
                }
                catch (Exception ex)
                {
                    message.MarkFailed(utcNow, "UnprotectFailed", ex.Message);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    continue;
                }

                var tenant = await dbContext.Tenants
                    .FirstOrDefaultAsync(t => t.Id == invitation.TenantId, cancellationToken);
                var tenantName = tenant?.Name ?? "Tenant";

                string? roleName = null;
                if (invitation.RoleId.HasValue)
                {
                    var role = await dbContext.Roles
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(r => r.Id == invitation.RoleId.Value, cancellationToken);
                    roleName = role?.Name;
                }

                var request = new InvitationEmailRequest(
                    message.RecipientEmail,
                    tenantName,
                    roleName,
                    rawToken,
                    invitation.ExpiresAtUtc,
                    null);

                var dispatchResult = await emailSender.SendInvitationEmailAsync(request, cancellationToken);

                if (dispatchResult.IsSuccess)
                {
                    message.MarkSent(utcNow, dispatchResult.ProviderMessageId);
                    processedCount++;
                    logger.LogInformation(
                        "Outbox email sent successfully for OutboxMessageId={OutboxMessageId}, Attempt={Attempt}",
                        message.Id,
                        message.AttemptCount);
                }
                else
                {
                    logger.LogWarning(
                        "Outbox email attempt failed for OutboxMessageId={OutboxMessageId}, Attempt={Attempt}, Code={Code}",
                        message.Id,
                        message.AttemptCount,
                        dispatchResult.ErrorCode);

                    if (!dispatchResult.IsTransient || message.AttemptCount >= message.MaxAttempts)
                    {
                        message.MarkFailed(utcNow, dispatchResult.ErrorCode, dispatchResult.ErrorDescription);
                    }
                    else
                    {
                        var retryDelay = dispatchResult.RetryAfter ??
                                         TimeSpan.FromSeconds(Math.Pow(2, message.AttemptCount) * 15);
                        message.ScheduleRetry(utcNow.Add(retryDelay), dispatchResult.ErrorCode, dispatchResult.ErrorDescription);
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unexpected error processing outbox message {OutboxMessageId}", message.Id);
                message.MarkFailed(utcNow, "UnexpectedError", ex.Message);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return processedCount;
    }
}
