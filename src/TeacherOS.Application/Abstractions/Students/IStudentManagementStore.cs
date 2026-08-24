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
}
