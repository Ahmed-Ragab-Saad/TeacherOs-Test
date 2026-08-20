namespace TeacherOS.ArchitectureTests;

public sealed class ProjectReferenceRulesTests
{
    [Fact]
    public void Production_projects_have_exactly_the_allowed_project_references()
    {
        var repositoryRoot = FindRepositoryRoot();
        var expectedReferences = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["TeacherOS.Domain"] = [],
            ["TeacherOS.Application"] = ["TeacherOS.Domain"],
            ["TeacherOS.Infrastructure"] = ["TeacherOS.Application", "TeacherOS.Domain"],
            ["TeacherOS.Api"] = ["TeacherOS.Application", "TeacherOS.Infrastructure"],
        };

        foreach (var (projectName, expected) in expectedReferences)
        {
            var projectFile = Path.Combine(repositoryRoot, "src", projectName, $"{projectName}.csproj");
            var project = XDocument.Load(projectFile);
            var actual = project
                .Descendants("ProjectReference")
                .Select(reference => reference.Attribute("Include")?.Value)
                .Where(path => path is not null)
                .Select(path => Path.GetFileNameWithoutExtension(path!))
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected.Order(StringComparer.Ordinal), actual);
        }
    }

    [Fact]
    public void Domain_and_application_have_no_forbidden_package_references()
    {
        var repositoryRoot = FindRepositoryRoot();

        Assert.Empty(ReadPackageReferences(repositoryRoot, "TeacherOS.Domain"));
        Assert.DoesNotContain(
            ReadPackageReferences(repositoryRoot, "TeacherOS.Application"),
            package => package.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TeacherOS.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the TeacherOS repository root.");
    }

    private static string[] ReadPackageReferences(string repositoryRoot, string projectName)
    {
        var projectFile = Path.Combine(repositoryRoot, "src", projectName, $"{projectName}.csproj");
        var project = XDocument.Load(projectFile);

        return project
            .Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(package => package is not null)
            .Select(package => package!)
            .ToArray();
    }
}
