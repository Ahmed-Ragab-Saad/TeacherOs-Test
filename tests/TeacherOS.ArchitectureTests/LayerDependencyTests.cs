namespace TeacherOS.ArchitectureTests;

public sealed class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(Entity<>).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IUnitOfWork).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(InfrastructureServiceCollectionExtensions).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_application()
    {
        AssertDoesNotReference(DomainAssembly, ApplicationAssembly);
    }

    [Fact]
    public void Domain_does_not_depend_on_infrastructure()
    {
        AssertDoesNotReference(DomainAssembly, InfrastructureAssembly);
    }

    [Fact]
    public void Domain_does_not_depend_on_api()
    {
        AssertDoesNotReference(DomainAssembly, ApiAssembly);
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure()
    {
        AssertDoesNotReference(ApplicationAssembly, InfrastructureAssembly);
    }

    [Fact]
    public void Application_does_not_depend_on_api()
    {
        AssertDoesNotReference(ApplicationAssembly, ApiAssembly);
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        AssertDoesNotReference(InfrastructureAssembly, ApiAssembly);
    }

    [Fact]
    public void Application_does_not_depend_on_entity_framework_core()
    {
        AssertDoesNotReferenceAssemblyPrefix(ApplicationAssembly, "Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void Domain_does_not_depend_on_aspnet_core_identity()
    {
        AssertDoesNotReferenceAssemblyPrefix(DomainAssembly, "Microsoft.AspNetCore.Identity");
    }

    [Fact]
    public void Application_does_not_depend_on_aspnet_core_identity()
    {
        AssertDoesNotReferenceAssemblyPrefix(ApplicationAssembly, "Microsoft.AspNetCore.Identity");
    }

    [Fact]
    public void Layer_namespaces_match_their_assemblies()
    {
        AssertNamespacePrefix(DomainAssembly, "TeacherOS.Domain");
        AssertNamespacePrefix(ApplicationAssembly, "TeacherOS.Application");
        AssertNamespacePrefix(InfrastructureAssembly, "TeacherOS.Infrastructure");
    }

    private static void AssertDoesNotReference(Assembly source, Assembly forbiddenDependency)
    {
        Assert.DoesNotContain(
            source.GetReferencedAssemblies(),
            reference => string.Equals(
                reference.Name,
                forbiddenDependency.GetName().Name,
                StringComparison.Ordinal));
    }

    private static void AssertDoesNotReferenceAssemblyPrefix(Assembly source, string forbiddenPrefix)
    {
        Assert.DoesNotContain(
            source.GetReferencedAssemblies(),
            reference => reference.Name?.StartsWith(forbiddenPrefix, StringComparison.Ordinal) == true);
    }

    private static void AssertNamespacePrefix(Assembly assembly, string expectedPrefix)
    {
        var types = assembly.GetTypes().Where(type => type.Namespace is not null);

        Assert.All(
            types,
            type => Assert.StartsWith(expectedPrefix, type.Namespace!, StringComparison.Ordinal));
    }
}
