using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace NodeRunner.Arch.Tests;

/// <summary>
/// Architecture rules enforced at test time. Analogous to kappuccino's :konsist-test module.
/// Each rule pins down a boundary declared in docs/ARCHITECTURE.md and CODE_DESIGN_PRINCIPLES.md.
/// When adding new rules, prefer a testable convention over a documented one.
/// </summary>
public sealed partial class ArchitectureSpec
{
    private static readonly Assembly _domain = typeof(NodeRunner.Domain.AssemblyMarker).Assembly;
    private static readonly Assembly _mechanics = typeof(NodeRunner.Mechanics.AssemblyMarker).Assembly;
    private static readonly Assembly _ml = typeof(NodeRunner.ML.AssemblyMarker).Assembly;
    private static readonly Assembly _app = typeof(NodeRunner.App.AssemblyMarker).Assembly;

    [Fact]
    public void Domain_HasNoDependenciesOnOtherProjectAssemblies()
    {
        var referenced = _domain.GetReferencedAssemblies().Select(a => a.Name).ToArray();

        referenced.ShouldNotContain("NodeRunner.Mechanics");
        referenced.ShouldNotContain("NodeRunner.ML");
        referenced.ShouldNotContain("NodeRunner.App");
    }

    [Fact]
    public void Mechanics_DependsOnlyOnDomain()
    {
        var referenced = _mechanics.GetReferencedAssemblies().Select(a => a.Name).ToArray();

        referenced.ShouldNotContain("NodeRunner.ML");
        referenced.ShouldNotContain("NodeRunner.App");
    }

    [Fact]
    public void Mechanics_DoesNotReferenceGodot()
    {
        AssertNoGodotReference(_mechanics);
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

    [Fact]
    public void Ml_DoesNotReferenceMechanics()
    {
        _ml.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ShouldNotContain("NodeRunner.Mechanics");
    }

    [Fact]
    public void ProductionSourceFiles_StayWithinHardLineLimit()
    {
        const int hardLimit = 2000;
        var root = FindRepositoryRoot();
        string[] skipped = ["bin", "obj", ".godot"];
        var offenders = new List<string>();

        foreach (var dir in new[] { "libs", Path.Combine("project", "src") })
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root, dir), "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, path);
                if (relative.Split(Path.DirectorySeparatorChar).Any(skipped.Contains))
                {
                    continue;
                }

                var lines = File.ReadLines(path).Count();
                if (lines > hardLimit)
                {
                    offenders.Add($"{relative} ({lines} lines)");
                }
            }
        }

        offenders.ShouldBeEmpty(
            $"Production files must stay within {hardLimit} lines; split out functionality. " +
            "See docs/CODE_DESIGN_PRINCIPLES.md §5.");
    }

    [Fact]
    public void AndroidBuildNumber_IsDerivedFromProjectVersion()
    {
        var version = VersionPattern().Match(ProjectSetting("project.godot", "config/version"));
        version.Success.ShouldBeTrue("application/config/version must be a quoted X.Y.Z.");

        var code = int.Parse(ProjectSetting("export_presets.cfg", "version/code"), CultureInfo.InvariantCulture);

        code.ShouldBe(
            (VersionPart(version, "major") * 1_000_000) + (VersionPart(version, "minor") * 1_000) + VersionPart(version, "patch"),
            "The build number is 1000000·major + 1000·minor + patch (#809); run .github/scripts/set-version.sh.");
    }

    [Fact]
    public void AndroidVersionName_FallsBackToProjectVersion() =>
        ProjectSetting("export_presets.cfg", "version/name")
            .ShouldBe("\"\"", "An empty name makes the export read application/config/version (#809).");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NodeRunner.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find NodeRunner.slnx.");
    }

    [Fact]
    public void AndroidDebugPreset_MatchesTheReleasePresetApartFromItsIdentity()
    {
        // Godot presets cannot inherit, so the debug preset repeats the release one. Only what
        // lets a debug build install next to the release may differ (#898).
        var release = ExportPreset("Android");
        var debug = ExportPreset("Android Debug");

        debug["package/unique_name"].ShouldBe("\"dev.mrlogic85.noderunner.debug\"");
        debug["package/name"].ShouldBe("\"Node Runner Debug\"");

        string[] identity = ["name", "package/unique_name", "package/name"];
        var drift = release.Keys.Union(debug.Keys)
            .Except(identity)
            .Where(key => release.GetValueOrDefault(key) != debug.GetValueOrDefault(key))
            .ToList();

        drift.ShouldBeEmpty("Change export options in both presets of project/export_presets.cfg.");
    }

    /// <summary>One value of <paramref name="key"/>; every export preset must agree on it.</summary>
    private static string ProjectSetting(string file, string key)
    {
        var prefix = key + "=";
        var values = File.ReadLines(Path.Combine(FindRepositoryRoot(), "project", file))
            .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
            .Select(line => line[prefix.Length..])
            .Distinct()
            .ToList();
        values.ShouldNotBeEmpty($"{file} has no {key}.");
        values.Count.ShouldBe(1, $"{file} sets {key} to more than one value.");
        return values[0];
    }

    /// <summary>The settings of the export preset named <paramref name="name"/>, with its options.</summary>
    private static Dictionary<string, string> ExportPreset(string name)
    {
        var presets = new List<Dictionary<string, string>>();
        foreach (var line in File.ReadLines(Path.Combine(FindRepositoryRoot(), "project", "export_presets.cfg")))
        {
            if (line.StartsWith("[preset.", StringComparison.Ordinal) && !line.EndsWith(".options]", StringComparison.Ordinal))
            {
                presets.Add([]);
            }
            else if (presets.Count > 0 && line.IndexOf('=') is > 0 and var equals)
            {
                presets[^1].Add(line[..equals], line[(equals + 1)..]);
            }
        }

        return presets.Single(preset => preset["name"] == $"\"{name}\"");
    }

    private static int VersionPart(Match version, string group) =>
        int.Parse(version.Groups[group].Value, CultureInfo.InvariantCulture);

    [GeneratedRegex("""^"(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]{0,2})\.(?<patch>0|[1-9][0-9]{0,2})"$""")]
    private static partial Regex VersionPattern();

    private static void AssertNoGodotReference(Assembly assembly)
    {
        var offenders = assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n is not null && n.StartsWith("Godot", StringComparison.Ordinal))
            .ToArray();

        offenders.ShouldBeEmpty(
            $"{assembly.GetName().Name} must not depend on any Godot assembly. " +
            "See docs/CODE_DESIGN_PRINCIPLES.md §3.");
    }
}
