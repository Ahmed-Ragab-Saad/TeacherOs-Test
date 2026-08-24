using TeacherOS.Application.Common;

namespace TeacherOS.Application.Students;

public static class StudentManagementErrors
{
    public static Error InvalidInput { get; } = new("Students.InvalidInput", "One or more student module values are invalid.", ErrorType.Validation);
    public static Error BranchNotFound { get; } = new("Students.BranchNotFound", "The branch was not found.", ErrorType.NotFound);
    public static Error GradeLevelNotFound { get; } = new("Students.GradeLevelNotFound", "The grade level was not found.", ErrorType.NotFound);
    public static Error StudentNotFound { get; } = new("Students.StudentNotFound", "The student was not found.", ErrorType.NotFound);
    public static Error BranchNameExists { get; } = new("Students.BranchNameExists", "A branch with this name already exists.", ErrorType.Conflict);
    public static Error GradeLevelNameExists { get; } = new("Students.GradeLevelNameExists", "A grade level with this name already exists.", ErrorType.Conflict);
    public static Error StudentCodeExists { get; } = new("Students.StudentCodeExists", "A student with this code already exists.", ErrorType.Conflict);
    public static Error NationalIdExists { get; } = new("Students.NationalIdExists", "A student with this national ID already exists.", ErrorType.Conflict);
    public static Error StudentAlreadyInStatus { get; } = new("Students.StudentAlreadyInStatus", "The student is already in the requested status.", ErrorType.Conflict);
}
