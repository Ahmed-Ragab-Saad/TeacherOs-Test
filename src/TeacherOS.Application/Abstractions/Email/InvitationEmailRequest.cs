using System;

namespace TeacherOS.Application.Abstractions.Email;

public sealed record InvitationEmailRequest(
    string RecipientEmail,
    string TenantDisplayName,
    string? RoleDisplayName,
    string RawInvitationToken,
    DateTimeOffset ExpiresAtUtc,
    string? InvitationInspectionUrl);
