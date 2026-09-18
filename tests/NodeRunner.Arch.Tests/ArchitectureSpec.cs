using System.Reflection;

namespace NodeRunner.Arch.Tests;

/// <summary>
/// Architecture rules enforced at test time. Analogous to kappuccino's :konsist-test module.
/// Each rule pins down a boundary declared in docs/ARCHITECTURE.md and CODE_DESIGN_PRINCIPLES.md.
/// When adding new rules, prefer a testable convention over a documented one.
/// </summary>
public sealed class ArchitectureSpec
{
    private static readonly Assembly _domain = typeof(NodeRunner.Domain.AssemblyMarker).Assembly;
    private static readonly Assembly _ml = typeof(NodeRunner.ML.AssemblyMarker).Assembly;
    private static readonly Assembly _app = typeof(NodeRunner.App.AssemblyMarker).Assembly;

    [Fact]
    public void Domain_HasNoDependenciesOnOtherProjectAssemblies()
    {
        var referenced = _domain.GetReferencedAssemblies().Select(a => a.Name).ToArray();

        referenced.ShouldNotContain("NodeRunner.ML");
        referenced.ShouldNotContain("NodeRunner.App");
    }

    [Fact]
    public void Domain_DoesNotReferenceGodot()
    {
        AssertNoGodotReference(_domain);
    }

    [Fact]
    public void Ml_DoesNotReferenceGodot()
    {
        AssertNoGodotReference(_ml);
    }

    [Fact]
    public void App_DoesNotReferenceGodot()
    {
        AssertNoGodotReference(_app);
    }

    [Fact]
    public void Ml_DoesNotReferenceApp()
    {
        _ml.GetReferencedAssemblies()
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
