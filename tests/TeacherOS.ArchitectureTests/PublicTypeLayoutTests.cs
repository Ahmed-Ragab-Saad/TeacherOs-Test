namespace TeacherOS.ArchitectureTests;

public sealed partial class PublicTypeLayoutTests
{
    [Fact]
    public void Every_source_file_has_at_most_one_primary_public_type_matching_its_filename()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");
        var sourceFiles = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        foreach (var sourceFile in sourceFiles)
        {
            var source = File.ReadAllText(sourceFile);
            var publicTypes = PublicTypeRegex().Matches(source);

            Assert.True(
                publicTypes.Count <= 1,
                $"{sourceFile} declares more than one public type.");

            if (publicTypes.Count == 1)
            {
                Assert.Equal(
                    Path.GetFileNameWithoutExtension(sourceFile),
                    publicTypes[0].Groups["name"].Value);
            }
        }
    }

    [Fact]
    public void Every_public_interface_has_its_own_matching_source_file()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");
        var interfaceNames = new[]
        {
            typeof(IUnitOfWork).Assembly,
            typeof(Entity<>).Assembly,
            typeof(InfrastructureServiceCollectionExtensions).Assembly,
            typeof(Program).Assembly,
        }
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.IsInterface)
            .Select(type => type.Name)
            .ToArray();

        foreach (var interfaceName in interfaceNames)
        {
            var matchingFiles = Directory
                .EnumerateFiles(sourceRoot, $"{interfaceName}.cs", SearchOption.AllDirectories)
                .ToArray();

            Assert.Single(matchingFiles);
        }
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

    [GeneratedRegex(
        @"^\s*public\s+(?:(?:abstract|sealed|static|partial)\s+)*(?:class|interface|record(?:\s+(?:class|struct))?|enum)\s+(?<name>[A-Za-z_]\w*)",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex PublicTypeRegex();
}
