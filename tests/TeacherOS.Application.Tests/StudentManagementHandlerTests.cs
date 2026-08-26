using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Data;
using TeacherOS.Application.Abstractions.Students;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Common;
using TeacherOS.Application.Students;
using TeacherOS.Domain.Students;
using Xunit;

namespace TeacherOS.Application.Tests;

public sealed class StudentManagementHandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public async Task Create_student_persists_a_valid_student_after_its_references_and_unique_values_are_checked()
    {
        var store = new FakeStore { Branch = CreateBranch(), GradeLevel = CreateGradeLevel() };
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(store, unitOfWork);

        var result = await handler.CreateStudentAsync(_tenantId, " ST-001 ", " Mona Ali ", " 12345 ", store.Branch.Id, store.GradeLevel.Id, new DateOnly(2026, 8, 1), null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(store.AddedStudent);
        Assert.Equal("ST-001", store.AddedStudent!.StudentCode);
        Assert.Equal("Mona Ali", store.AddedStudent.FullName);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Create_student_does_not_persist_when_its_code_already_exists()
    {
        var store = new FakeStore { Branch = CreateBranch(), GradeLevel = CreateGradeLevel(), StudentCodeExists = true };
        var unitOfWork = new FakeUnitOfWork();

        var result = await CreateHandler(store, unitOfWork).CreateStudentAsync(_tenantId, "ST-001", "Mona Ali", "12345", store.Branch.Id, store.GradeLevel.Id, new DateOnly(2026, 8, 1), null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(StudentManagementErrors.StudentCodeExists, result.Error);
        Assert.Null(store.AddedStudent);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Cross_tenant_or_unauthenticated_requests_are_rejected_before_the_store_is_used()
    {
        var store = new FakeStore();
        var unitOfWork = new FakeUnitOfWork();
        var deniedHandler = new StudentManagementHandler(new FakeCurrentUser(true), new FakeTenantContext(Guid.NewGuid()), store, unitOfWork);
        var anonymousHandler = new StudentManagementHandler(new FakeCurrentUser(false), new FakeTenantContext(_tenantId), store, unitOfWork);

        var denied = await deniedHandler.ListBranchesAsync(_tenantId, TestContext.Current.CancellationToken);
        var anonymous = await anonymousHandler.ListBranchesAsync(_tenantId, TestContext.Current.CancellationToken);

        Assert.Equal("Tenancy.AccessDenied", denied.Error.Code);
        Assert.Equal("Authentication.Unauthorized", anonymous.Error.Code);
        Assert.Equal(0, store.ListBranchesCallCount);
    }

    [Fact]
    public async Task Student_status_transition_persists_once_and_rejects_a_duplicate_transition()
    {
        var student = CreateStudent();
        var store = new FakeStore { Student = student };
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(store, unitOfWork);

        var suspended = await handler.SuspendForNonPaymentAsync(_tenantId, student.Id, TestContext.Current.CancellationToken);
        var duplicate = await handler.SuspendForNonPaymentAsync(_tenantId, student.Id, TestContext.Current.CancellationToken);

        Assert.True(suspended.IsSuccess);
        Assert.Equal(StudentStatus.SuspendedNonPayment, student.Status);
        Assert.Equal(StudentManagementErrors.StudentAlreadyInStatus, duplicate.Error);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Guardian_linking_validates_references_and_prevents_duplicates()
    {
        var student = CreateStudent();
        var guardian = new Guardian(Guid.NewGuid(), _tenantId, "Parent", "0100");
        var store = new FakeStore { Student = student, Guardian = guardian };
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(store, unitOfWork);

        var linked = await handler.LinkGuardianAsync(_tenantId, student.Id, guardian.Id, GuardianRelationshipType.Father, true, TestContext.Current.CancellationToken);
        store.StudentGuardian = linked.Value;
        var duplicate = await handler.LinkGuardianAsync(_tenantId, student.Id, guardian.Id, GuardianRelationshipType.Father, true, TestContext.Current.CancellationToken);

        Assert.True(linked.IsSuccess);
        Assert.Equal(StudentManagementErrors.StudentGuardianAlreadyLinked, duplicate.Error);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    private StudentManagementHandler CreateHandler(FakeStore store, FakeUnitOfWork unitOfWork) =>
        new(new FakeCurrentUser(true), new FakeTenantContext(_tenantId), store, unitOfWork);

    private Branch CreateBranch() => new(Guid.NewGuid(), _tenantId, "Main");
    private GradeLevel CreateGradeLevel() => new(Guid.NewGuid(), _tenantId, "Grade 1", 1);
    private Student CreateStudent() => new(Guid.NewGuid(), _tenantId, Guid.NewGuid(), Guid.NewGuid(), "ST-001", "Mona Ali", "12345", new DateOnly(2026, 8, 1));

    private sealed record FakeCurrentUser(bool IsAuthenticated) : ICurrentUser { public Guid? UserId => null; }
    private sealed class FakeTenantContext(Guid tenantId) : ITenantContext { public bool IsAvailable => true; public Guid TenantId => tenantId; public void Establish(Guid establishedTenantId) { } }
    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCount++; return Task.FromResult(1); }
        public Task<Result<T>> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
    }
    private sealed class FakeStore : IStudentManagementStore
    {
        public Branch? Branch { get; init; }
        public GradeLevel? GradeLevel { get; init; }
        public Student? Student { get; init; }
        public Guardian? Guardian { get; init; }
        public StudentGuardian? StudentGuardian { get; set; }
        public bool StudentCodeExists { get; init; }
        public Student? AddedStudent { get; private set; }
        public int ListBranchesCallCount { get; private set; }
        public Task<IReadOnlyList<Branch>> ListBranchesAsync(Guid tenantId, CancellationToken cancellationToken = default) { ListBranchesCallCount++; return Task.FromResult<IReadOnlyList<Branch>>([]); }
        public Task<Branch?> GetBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult(Branch?.Id == branchId ? Branch : null);
        public Task<bool> BranchNameExistsAsync(Guid tenantId, string name, Guid? excludingBranchId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public void AddBranch(Branch branch) { }
        public Task<IReadOnlyList<GradeLevel>> ListGradeLevelsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GradeLevel>>([]);
        public Task<GradeLevel?> GetGradeLevelAsync(Guid tenantId, Guid gradeLevelId, CancellationToken cancellationToken = default) => Task.FromResult(GradeLevel?.Id == gradeLevelId ? GradeLevel : null);
        public Task<bool> GradeLevelNameExistsAsync(Guid tenantId, string name, Guid? excludingGradeLevelId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public void AddGradeLevel(GradeLevel gradeLevel) { }
        public Task<Student?> GetStudentAsync(Guid tenantId, Guid studentId, CancellationToken cancellationToken = default) => Task.FromResult(Student?.Id == studentId ? Student : null);
        public Task<bool> StudentCodeExistsAsync(Guid tenantId, string studentCode, Guid? excludingStudentId, CancellationToken cancellationToken = default) => Task.FromResult(StudentCodeExists);
        public Task<bool> NationalIdExistsAsync(Guid tenantId, string nationalId, Guid? excludingStudentId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public void AddStudent(Student student) => AddedStudent = student;
        public Task<IReadOnlyList<Guardian>> ListGuardiansAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Guardian>>([]);
        public Task<Guardian?> GetGuardianAsync(Guid tenantId, Guid guardianId, CancellationToken cancellationToken = default) => Task.FromResult(Guardian?.Id == guardianId ? Guardian : null);
        public void AddGuardian(Guardian guardian) { }
        public Task<IReadOnlyList<StudentGuardian>> ListStudentGuardiansAsync(Guid tenantId, Guid studentId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StudentGuardian>>([]);
        public Task<StudentGuardian?> GetStudentGuardianAsync(Guid tenantId, Guid studentId, Guid guardianId, CancellationToken cancellationToken = default) => Task.FromResult(StudentGuardian);
        public void AddStudentGuardian(StudentGuardian studentGuardian) { }
        public void RemoveStudentGuardian(StudentGuardian studentGuardian) { }
    }
}
