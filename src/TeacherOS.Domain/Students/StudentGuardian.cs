using System;
using System.Collections.Generic;
using System.Text;
using TeacherOS.Domain.Common;

namespace TeacherOS.Domain.Students;

public sealed class StudentGuardian : Entity<Guid>, ITenantOwnedEntity
{
    public StudentGuardian(
        Guid id,
        Guid tenantId,
        Guid studentId,
        Guid guardianId,
        GuardianRelationshipType relationshipType,
        bool isPrimaryContact)
        : base(ValidateId(id))
    {
        TenantId = ValidateTenantId(tenantId);
        StudentId = ValidateRequiredId(studentId, nameof(studentId), "Student");
        GuardianId = ValidateRequiredId(guardianId, nameof(guardianId), "Guardian");
        RelationshipType = ValidateRelationshipType(relationshipType);
        IsPrimaryContact = isPrimaryContact;
    }

    public Guid TenantId { get; private set; }

    public Guid StudentId { get; private set; }

    public Guid GuardianId { get; private set; }

    public GuardianRelationshipType RelationshipType { get; private set; }

    public bool IsPrimaryContact { get; private set; }

    public void Update(GuardianRelationshipType relationshipType, bool isPrimaryContact)
    {
        RelationshipType = ValidateRelationshipType(relationshipType);
        IsPrimaryContact = isPrimaryContact;
    }

    private static Guid ValidateId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Student-guardian link identifier is required.", nameof(id));
        }

        return id;
    }

    private static Guid ValidateTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Student-guardian link must belong to a tenant.", nameof(tenantId));
        }

        return tenantId;
    }

    private static Guid ValidateRequiredId(Guid id, string parameterName, string subject)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException($"{subject} is required.", parameterName);
        }

        return id;
    }

    private static GuardianRelationshipType ValidateRelationshipType(GuardianRelationshipType relationshipType)
    {
        if (!Enum.IsDefined(relationshipType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(relationshipType),
                relationshipType,
                "Relationship type is not defined.");
        }

        return relationshipType;
    }
}
