using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Domain.Students;

namespace TeacherOS.Application.Abstractions.Students;

public interface IStudentManagementStore
{
    Task<IReadOnlyList<Branch>> ListBranchesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default);
    Task<bool> BranchNameExistsAsync(Guid tenantId, string name, Guid? excludingBranchId, CancellationToken cancellationToken = default);
    void AddBranch(Branch branch);

    Task<IReadOnlyList<GradeLevel>> ListGradeLevelsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<GradeLevel?> GetGradeLevelAsync(Guid tenantId, Guid gradeLevelId, CancellationToken cancellationToken = default);
    Task<bool> GradeLevelNameExistsAsync(Guid tenantId, string name, Guid? excludingGradeLevelId, CancellationToken cancellationToken = default);
    void AddGradeLevel(GradeLevel gradeLevel);

    Task<Student?> GetStudentAsync(Guid tenantId, Guid studentId, CancellationToken cancellationToken = default);
    Task<bool> StudentCodeExistsAsync(Guid tenantId, string studentCode, Guid? excludingStudentId, CancellationToken cancellationToken = default);
    Task<bool> NationalIdExistsAsync(Guid tenantId, string nationalId, Guid? excludingStudentId, CancellationToken cancellationToken = default);
    void AddStudent(Student student);

    Task<IReadOnlyList<Guardian>> ListGuardiansAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guardian?> GetGuardianAsync(Guid tenantId, Guid guardianId, CancellationToken cancellationToken = default);
    void AddGuardian(Guardian guardian);

    Task<IReadOnlyList<StudentGuardian>> ListStudentGuardiansAsync(Guid tenantId, Guid studentId, CancellationToken cancellationToken = default);
    Task<StudentGuardian?> GetStudentGuardianAsync(Guid tenantId, Guid studentId, Guid guardianId, CancellationToken cancellationToken = default);
    void AddStudentGuardian(StudentGuardian studentGuardian);
    void RemoveStudentGuardian(StudentGuardian studentGuardian);
}
