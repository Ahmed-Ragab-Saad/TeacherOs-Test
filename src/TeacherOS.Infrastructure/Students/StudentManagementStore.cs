using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TeacherOS.Application.Abstractions.Students;
using TeacherOS.Domain.Students;
using TeacherOS.Infrastructure.Persistence;

namespace TeacherOS.Infrastructure.Students;

internal sealed class StudentManagementStore(ApplicationDbContext dbContext) : IStudentManagementStore
{
    public async Task<IReadOnlyList<Branch>> ListBranchesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Branches.AsNoTracking().Where(branch => branch.TenantId == tenantId).OrderBy(branch => branch.Name).ToListAsync(cancellationToken);
    }

    public Task<Branch?> GetBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default)
    {
        return dbContext.Branches.FirstOrDefaultAsync(branch => branch.TenantId == tenantId && branch.Id == branchId, cancellationToken);
    }

    public Task<bool> BranchNameExistsAsync(Guid tenantId, string name, Guid? excludingBranchId, CancellationToken cancellationToken = default)
    {
        return dbContext.Branches.AnyAsync(branch => branch.TenantId == tenantId && branch.Name == name && (!excludingBranchId.HasValue || branch.Id != excludingBranchId.Value), cancellationToken);
    }

    public void AddBranch(Branch branch) => dbContext.Branches.Add(branch);

    public async Task<IReadOnlyList<GradeLevel>> ListGradeLevelsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.GradeLevels.AsNoTracking().Where(gradeLevel => gradeLevel.TenantId == tenantId).OrderBy(gradeLevel => gradeLevel.SortOrder).ThenBy(gradeLevel => gradeLevel.Name).ToListAsync(cancellationToken);
    }

    public Task<GradeLevel?> GetGradeLevelAsync(Guid tenantId, Guid gradeLevelId, CancellationToken cancellationToken = default)
    {
        return dbContext.GradeLevels.FirstOrDefaultAsync(gradeLevel => gradeLevel.TenantId == tenantId && gradeLevel.Id == gradeLevelId, cancellationToken);
    }

    public Task<bool> GradeLevelNameExistsAsync(Guid tenantId, string name, Guid? excludingGradeLevelId, CancellationToken cancellationToken = default)
    {
        return dbContext.GradeLevels.AnyAsync(gradeLevel => gradeLevel.TenantId == tenantId && gradeLevel.Name == name && (!excludingGradeLevelId.HasValue || gradeLevel.Id != excludingGradeLevelId.Value), cancellationToken);
    }

    public void AddGradeLevel(GradeLevel gradeLevel) => dbContext.GradeLevels.Add(gradeLevel);

    public Task<Student?> GetStudentAsync(Guid tenantId, Guid studentId, CancellationToken cancellationToken = default)
    {
        return dbContext.Students.FirstOrDefaultAsync(student => student.TenantId == tenantId && student.Id == studentId, cancellationToken);
    }

    public Task<bool> StudentCodeExistsAsync(Guid tenantId, string studentCode, Guid? excludingStudentId, CancellationToken cancellationToken = default)
    {
        return dbContext.Students.AnyAsync(student => student.TenantId == tenantId && student.StudentCode == studentCode && (!excludingStudentId.HasValue || student.Id != excludingStudentId.Value), cancellationToken);
    }

    public Task<bool> NationalIdExistsAsync(Guid tenantId, string nationalId, Guid? excludingStudentId, CancellationToken cancellationToken = default)
    {
        return dbContext.Students.AnyAsync(student => student.TenantId == tenantId && student.NationalId == nationalId && (!excludingStudentId.HasValue || student.Id != excludingStudentId.Value), cancellationToken);
    }

    public void AddStudent(Student student) => dbContext.Students.Add(student);

    public async Task<IReadOnlyList<Guardian>> ListGuardiansAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Guardians.AsNoTracking()
            .Where(guardian => guardian.TenantId == tenantId)
            .OrderBy(guardian => guardian.FullName)
            .ThenBy(guardian => guardian.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Guardian?> GetGuardianAsync(Guid tenantId, Guid guardianId, CancellationToken cancellationToken = default)
    {
        return dbContext.Guardians.FirstOrDefaultAsync(guardian => guardian.TenantId == tenantId && guardian.Id == guardianId, cancellationToken);
    }

    public void AddGuardian(Guardian guardian) => dbContext.Guardians.Add(guardian);

    public async Task<IReadOnlyList<StudentGuardian>> ListStudentGuardiansAsync(Guid tenantId, Guid studentId, CancellationToken cancellationToken = default)
    {
        return await dbContext.StudentGuardians.AsNoTracking()
            .Where(link => link.TenantId == tenantId && link.StudentId == studentId)
            .OrderByDescending(link => link.IsPrimaryContact)
            .ThenBy(link => link.GuardianId)
            .ToListAsync(cancellationToken);
    }

    public Task<StudentGuardian?> GetStudentGuardianAsync(Guid tenantId, Guid studentId, Guid guardianId, CancellationToken cancellationToken = default)
    {
        return dbContext.StudentGuardians.FirstOrDefaultAsync(
            link => link.TenantId == tenantId && link.StudentId == studentId && link.GuardianId == guardianId,
            cancellationToken);
    }

    public void AddStudentGuardian(StudentGuardian studentGuardian) => dbContext.StudentGuardians.Add(studentGuardian);

    public void RemoveStudentGuardian(StudentGuardian studentGuardian) => dbContext.StudentGuardians.Remove(studentGuardian);
}
