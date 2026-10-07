using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Godot attaches a script only to a node of its native base type or one derived from it; on any
/// other node it drops the script and the node shows nothing (#805: UiPartRow became a BaseButton,
/// and the gallery's Control-typed rows vanished). Every authored node's type is its script's base.
/// </summary>
public sealed class SceneScriptTypeTests
{
    [Fact]
    public void EveryScriptedSceneNode_HasItsScriptsNativeBaseType()
    {
        var compilation = CSharpSources.ProjectCompilation;
        var nativeBases = CSharpSources.Project
            .SelectMany(source =>
            {
                var model = compilation.GetSemanticModel(source.Tree);
                return source.Tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .Select(declaration => (source.Path, Type: model.GetDeclaredSymbol(declaration) as INamedTypeSymbol));
            })
            // Godot runs the class named after its file.
            .Where(entry => entry.Type is not null && entry.Type.Name == Path.GetFileNameWithoutExtension(entry.Path) && NativeBase(entry.Type) is not null)
            .ToDictionary(entry => entry.Path.Replace('\\', '/'), entry => entry.Type!);

        var checkedNodes = 0;
        var mismatches = SceneNodes.Files()
            .SelectMany(file => SceneNodes.Parse(file.Scene, file.Text))
            .Where(node => node.Type is not null && node.Script?.StartsWith("res://src/", StringComparison.Ordinal) == true)
            .Select(node => (Node: node, Type: nativeBases.GetValueOrDefault(node.Script!["res://src/".Length..])))
            .Where(entry => entry.Type is not null)
            .Select(entry =>
            {
                checkedNodes++;
                return (entry.Node, Base: NativeBase(entry.Type!)!);
            })
            .Where(entry => !IsOrDerivesFrom(compilation, entry.Node.Type!, entry.Base))
            .Select(entry => $"{entry.Node.Node}: script base {entry.Base.Name}")
            .ToList();

        checkedNodes.ShouldBeGreaterThan(0);
        mismatches.ShouldBeEmpty();
    }

    // The first Godot class the script derives from: what Godot instantiates before attaching it.
    private static INamedTypeSymbol? NativeBase(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.ContainingNamespace.ToDisplayString() == "Godot")
            {
                return current;
            }
        }

        return null;
    }

    // A node typed as a subclass of the script's base (a VBoxContainer for a Container script) is fine.
    private static bool IsOrDerivesFrom(Compilation compilation, string nodeType, INamedTypeSymbol scriptBase) =>
        compilation.GetTypeByMetadataName("Godot." + nodeType) is { } type
        && (SymbolEqualityComparer.Default.Equals(type, scriptBase) || CSharpSources.DerivesFrom(type, scriptBase));
}
