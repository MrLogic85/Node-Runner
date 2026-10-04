using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// <c>project/src</c> parsed with Roslyn and compiled against GodotSharp and the pure libs, so
/// guards can bind types. No Godot runtime is involved.
/// </summary>
internal static class CSharpSources
{
    private static readonly Lazy<IReadOnlyList<Source>> _project = new(LoadProject);
    private static readonly Lazy<CSharpCompilation> _projectCompilation = new(() => Compile(_project.Value));
    private static readonly Lazy<IReadOnlyList<MetadataReference>> _references = new(LoadReferences);

    /// <summary>The SDK's implicit usings (<c>ImplicitUsings</c> in Directory.Build.props).</summary>
    private static readonly SyntaxTree _implicitUsings = CSharpSyntaxTree.ParseText(
        """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Net.Http;
        global using System.Threading;
        global using System.Threading.Tasks;
        """);

    /// <summary>Every file under <c>project/src</c>, by path relative to it.</summary>
    public static IReadOnlyList<Source> Project => _project.Value;

    public static CSharpCompilation ProjectCompilation => _projectCompilation.Value;

    public static CSharpCompilation Compile(IReadOnlyList<Source> sources) => CSharpCompilation.Create(
        "UiSourceGuard",
        sources.Select(source => source.Tree).Append(_implicitUsings),
        _references.Value,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    /// <summary>Compiles sources that include the pure libs themselves, so only the BCL and GodotSharp are referenced.</summary>
    public static CSharpCompilation CompileWithLibs(IReadOnlyList<Source> sources) => CSharpCompilation.Create(
        "UiSourceGuard",
        sources.Select(source => source.Tree).Append(_implicitUsings),
        _references.Value.Where(reference => reference.Display is not { } path || !System.IO.Path.GetFileName(path).StartsWith("NodeRunner.", StringComparison.Ordinal)),
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    public static Source Snippet(string member) => new(
        "snippet.cs",
        CSharpSyntaxTree.ParseText(
            $"using Godot; using NodeRunner.Ui.Lib; partial class Snippet : Godot.Control {{ {member} }}"));

    public static ISymbol? Symbol(SemanticModel model, SyntaxNode node)
    {
        var info = model.GetSymbolInfo(node);
        return info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
    }

    public static bool DerivesFrom(ITypeSymbol? type, ITypeSymbol? baseType)
    {
        for (var current = type; current is not null && baseType is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<Source> LoadProject()
    {
        var root = Path.Combine(SceneNodes.FindRepositoryRoot(), "project", "src");
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => new Source(
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                CSharpSyntaxTree.ParseText(File.ReadAllText(path))))
            .ToList();
    }

    /// <summary>
    /// The BCL, GodotSharp and the pure libs; the Godot project itself is the compiled source.
    /// Generated Godot partials are absent, which only fails unrelated binds.
    /// </summary>
    private static IReadOnlyList<MetadataReference> LoadReferences()
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return platform
            .Concat(Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll")
                .Where(path => Path.GetFileName(path) is "GodotSharp.dll" or "NodeRunner.App.dll" or "NodeRunner.Domain.dll" or "NodeRunner.ML.dll"))
            .Distinct()
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
    }

    public sealed record Source(string Path, SyntaxTree Tree)
    {
        public IEnumerable<string> Find(Func<SyntaxNode, bool> isViolation) =>
            Tree.GetRoot().DescendantNodes()
                .Where(isViolation)
                .Select(Describe);

        public string Describe(SyntaxNode node) =>
            $"{Path}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {node}";
    }
}
