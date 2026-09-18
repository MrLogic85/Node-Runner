using System.Reflection;

namespace NodeRunner.Arch.Tests;

/// <summary>
/// Architecture rules enforced at test time. Analogous to kappuccino's :konsist-test module.
/// Each rule pins down a boundary declared in docs/ARCHITECTURE.md and CODE_DESIGN_PRINCIPLES.md.
/// When adding new rules, prefer a testable convention over a documented one.
/// </summary>
public sealed class ArchitectureSpec
{
    private static readonly Assembly Domain = typeof(NodeRunner.Domain.AssemblyMarker).Assembly;
    private static readonly Assembly Ml = typeof(NodeRunner.ML.AssemblyMarker).Assembly;
    private static readonly Assembly App = typeof(NodeRunner.App.AssemblyMarker).Assembly;

    [Fact]
    public void Domain_HasNoDependenciesOnOtherProjectAssemblies()
    {
        var referenced = Domain.GetReferencedAssemblies().Select(a => a.Name).ToArray();

        referenced.ShouldNotContain("NodeRunner.ML");
        referenced.ShouldNotContain("NodeRunner.App");
    }

    [Fact]
    public void Domain_DoesNotReferenceGodot()
    {
        AssertNoGodotReference(Domain);
    }

    [Fact]
    public void Ml_DoesNotReferenceGodot()
    {
        AssertNoGodotReference(Ml);
    }

    [Fact]
    public void App_DoesNotReferenceGodot()
    {
        AssertNoGodotReference(App);
    }

    [Fact]
    public void Ml_DoesNotReferenceApp()
    {
        Ml.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ShouldNotContain("NodeRunner.App");
    }

    private static void AssertNoGodotReference(Assembly assembly)
    {
        var offenders = assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n is not null && n.StartsWith("Godot", StringComparison.Ordinal))
            .ToArray();

        offenders.ShouldBeEmpty(
            $"{assembly.GetName().Name} must not depend on any Godot assembly. " +
            "See docs/CODE_DESIGN_PRINCIPLES.md §2.");
    }
}
