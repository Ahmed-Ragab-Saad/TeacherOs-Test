using System.Collections.Generic;

namespace TeacherOS.Application.Authentication;

public sealed record CurrentSession(
    Guid UserId,
    string Email,
    IReadOnlyCollection<CurrentTenantMembership> Memberships);
