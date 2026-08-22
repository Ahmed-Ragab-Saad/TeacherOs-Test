using System;
using TeacherOS.Domain.Common;

namespace TeacherOS.Domain.Tenancy;

public sealed class TenantMembership : Entity<Guid>
{
    public TenantMembership(
        Guid id,
        Guid tenantId,
        Guid userId,
        TenantMembershipStatus status,
        Guid? roleId = null)
        : base(ValidateId(id))
    {
        TenantId = ValidateRequiredId(tenantId, nameof(tenantId));
        UserId = ValidateRequiredId(userId, nameof(userId));
        Status = ValidateStatus(status);
        RoleId = roleId;
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public TenantMembershipStatus Status { get; private set; }
    public Guid? RoleId { get; private set; }

    private static Guid ValidateId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Membership identifier is required.", nameof(id));
        }

        return id;
    }

    private static Guid ValidateRequiredId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A membership boundary identifier is required.", parameterName);
        }

        return id;
    }

    private static TenantMembershipStatus ValidateStatus(TenantMembershipStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Membership status is not defined.");
        }

        return status;
    }
}
