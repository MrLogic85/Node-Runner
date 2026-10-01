using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Screens and widgets rewritten to the ownership split in <c>docs/UI_DIRECTION.md</c> ("Who owns
/// what"). Each rewrite adds its files; #310 closes when every product screen and widget is listed.
/// </summary>
internal static class RewrittenUi
{
    /// <summary>Screen scripts, by path under <c>project/src</c>: thin, with no numbers of their own.</summary>
    public static readonly string[] Screens =
    [
        "ui/screens/BuildScreen.cs",
        "ui/screens/ColorsAndStylesScreen.cs",
        "ui/screens/ComponentGalleryScreen.cs",
        "ui/screens/CreationsScreen.cs",
        "ui/screens/ExamplesScreen.cs",
        "ui/screens/GalleryScreen.cs",
        "ui/screens/PopupGalleryScreen.cs",
        "ui/screens/ToolbarsScreen.cs",
        "ui/screens/TrainingScreen.cs",
    ];

    /// <summary>Widget scripts, by path under <c>project/src</c>: held to the library's rules.</summary>
    public static readonly string[] Widgets =
    [
        "ui/widgets/BrainFocusNetworkView.cs",
        "ui/widgets/BrainFocusSheet.cs",
        "ui/widgets/BrainSetupNetwork.cs",
        "ui/widgets/BrainSetupSheet.cs",
        "ui/widgets/BuildCanvas.cs",
        "ui/widgets/CreationCard.cs",
        "ui/widgets/CreatureThumbnail.cs",
    ];

    /// <summary>
    /// Widgets in <see cref="Widgets"/> that draw in <c>_Draw</c>. The number rule skips every literal
    /// inside <c>_Draw</c> and its <c>Draw*</c> helpers (proportions, strokes, dash lengths, segment
    /// counts, alphas); numbers elsewhere in the file are still named and every other rule holds.
    /// </summary>
    public static readonly string[] DrawnWidgets =
    [
        "ui/widgets/BrainFocusNetworkView.cs",
        "ui/widgets/BrainSetupNetwork.cs",
        "ui/widgets/BuildCanvas.cs",
    ];

    /// <summary>
    /// Scene-authored screens and widgets, by path under <c>project/scenes</c>. A listed scene's
    /// script bindings are checked even before the script joins <see cref="Screens"/>.
    /// </summary>
    public static readonly string[] Scenes = [
        "screens/BuildScreen.tscn",
        "screens/ColorsAndStylesScreen.tscn",
        "screens/ComponentGalleryScreen.tscn",
        "screens/CreationsScreen.tscn",
        "screens/ExamplesScreen.tscn",
        "screens/PopupGalleryScreen.tscn",
        "screens/ToolbarsScreen.tscn",
        "screens/TrainingScreen.tscn",
        "widgets/BrainFocusSheet.tscn",
        "widgets/BrainSetupSheet.tscn",
        "widgets/CreationCard.tscn",
    ];
}

/// <summary>
/// A rewritten screen or widget scene is built from the component library, and its script binds
/// only nodes the scene declares (#310). Reads scene text and binds C# with Roslyn; no Godot runtime.
/// </summary>
public sealed class RewrittenSceneTests
{
    private static readonly string[] _libraryScripts = ["res://src/ui/lib/", "res://src/ui/widgets/"];
    private static readonly string[] _libraryScenes = ["res://scenes/ui/", "res://scenes/widgets/"];
    private const string _screenScripts = "res://src/ui/screens/";
    private const string _scenesRoot = "res://scenes/";
    private const string _sourcesRoot = "res://src/";

    // Layout-only native types; any other Control node needs a library script or scene.
    private static readonly string[] _layoutTypes =
    [
        "Control", "Container", "BoxContainer", "HBoxContainer", "VBoxContainer", "MarginContainer",
        "ScrollContainer", "GridContainer", "CenterContainer", "FlowContainer", "HFlowContainer",
        "VFlowContainer", "AspectRatioContainer",
    ];

    [Fact]
    public void Rewritten_scenes_build_on_library_components()
    {
        RewrittenUi.Scenes
            .SelectMany(SceneNodes.InScene)
            .Where(node => !IsLibraryNode(node))
            .Select(node => node.Node.ToString())
            .ShouldBeEmpty(
                "Use a Ui* component or widget (a script in ui/lib or ui/widgets, or a scene in scenes/ui " +
                "or scenes/widgets) or a plain container; a raw Label or Button restyled locally is not reuse.");
    }

    [Theory]
    [InlineData("""[node name="Row" type="HBoxContainer" parent="."]""", null, null)]
    [InlineData("""[node name="Title" type="Label" parent="."]""", "res://src/ui/lib/UiLabel.cs", null)]
    [InlineData("""[node name="Toolbar" parent="." instance=ExtResource("1")]""", null, "res://scenes/ui/UiToolbar.tscn")]
    [InlineData("""[node name="Back" parent="Toolbar/Row"]""", null, null)]
    [InlineData("""[node name="Screen" type="Control"]""", "res://src/ui/screens/CreationsScreen.cs", null)]
    public void Library_node_passes(string header, string? script, string? instance) =>
        IsLibraryNode(Node(header, script, instance)).ShouldBeTrue();

    [Theory]
    [InlineData("""[node name="Title" type="Label" parent="."]""", null, null)]
    [InlineData("""[node name="Panel" type="PanelContainer" parent="."]""", null, null)]
    [InlineData("""[node name="Card" type="Control" parent="."]""", "res://src/ui/screens/CreationsScreen.cs", null)]
    [InlineData("""[node name="Other" parent="." instance=ExtResource("1")]""", null, "res://scenes/Creature.tscn")]
    public void Raw_node_is_flagged(string header, string? script, string? instance) =>
        IsLibraryNode(Node(header, script, instance)).ShouldBeFalse();

    [Fact]
    public void Rewritten_scripts_bind_nodes_their_scene_declares()
    {
        var compilation = CSharpSources.ProjectCompilation;
        var violations = new List<string>();
        foreach (var scenePath in RewrittenUi.Scenes)
        {
            var nodes = SceneNodes.InScene(scenePath);
            var root = nodes.Single(node => node.IsRoot);
            root.Script.ShouldNotBeNull();
            foreach (var (source, invocation, model) in ScriptAndBaseInvocations(ClassOf(root, compilation), compilation))
            {
                if (CSharpSources.Symbol(model, invocation) is not IMethodSymbol { Name: "GetNode" or "GetNodeOrNull", TypeArguments: [var bound] }
                    || invocation.ArgumentList.Arguments is not [{ Expression: LiteralExpressionSyntax { Token.ValueText: ['%', .. var name] } }])
                {
                    continue;
                }

                var node = nodes.FirstOrDefault(node => node.IsUnique && node.Name == name);
                if (node is null)
                {
                    violations.Add($"{source.Describe(invocation)}: {scenePath} declares no unique %{name}");
                }
                else if (ClassOf(node, compilation) is var type && !CSharpSources.DerivesFrom(type, bound))
                {
                    violations.Add($"{source.Describe(invocation)}: %{name} is {type?.Name ?? "unknown"}, not {bound.Name}");
                }
            }
        }

        violations.ShouldBeEmpty("Every %UniqueName a script binds is a unique node of its scene that fits the bound type.");
    }

    // Invocations in the root script and in its base classes that live in project/src (the gallery
    // pages share their toolbar bindings through GalleryScreen).
    private static IEnumerable<(CSharpSources.Source Source, InvocationExpressionSyntax Invocation, SemanticModel Model)>
        ScriptAndBaseInvocations(ITypeSymbol? type, Compilation compilation)
    {
        for (; type is not null; type = type.BaseType)
        {
            foreach (var reference in type.DeclaringSyntaxReferences)
            {
                if (CSharpSources.Project.SingleOrDefault(source => source.Tree == reference.SyntaxTree) is not { } source)
                {
                    continue;
                }

                var model = compilation.GetSemanticModel(source.Tree);
                foreach (var invocation in reference.GetSyntax().DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    yield return (source, invocation, model);
                }
            }
        }
    }

    private static bool IsLibraryNode(SceneNodes.SceneNode node) => node switch
    {
        { Instance: { } scene } => _libraryScenes.Any(root => scene.StartsWith(root, StringComparison.Ordinal)),
        { Script: { } script } => _libraryScripts.Any(root => script.StartsWith(root, StringComparison.Ordinal))
            || (node.IsRoot && script.StartsWith(_screenScripts, StringComparison.Ordinal)),
        { Type: null } => true,
        { Type: var type } => _layoutTypes.Contains(type),
    };

    // The class a node is at runtime: its script, the root of the scene it instances, or its native type.
    private static ITypeSymbol? ClassOf(SceneNodes.SceneNode node, Compilation compilation)
    {
        if (node.Script is { } script)
        {
            var source = SourceOf(script);
            var declaration = source.Tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            return compilation.GetSemanticModel(source.Tree).GetDeclaredSymbol(declaration) as ITypeSymbol;
        }

        if (node.Instance is { } scene)
        {
            return ClassOf(SceneNodes.InScene(scene[_scenesRoot.Length..]).Single(root => root.IsRoot), compilation);
        }

        return node.Type is { } type ? compilation.GetTypeByMetadataName("Godot." + type) : null;
    }

    private static CSharpSources.Source SourceOf(string script) =>
        CSharpSources.Project.Single(source => _sourcesRoot + source.Path == script);

    private static SceneNodes.SceneNode Node(string header, string? script, string? instance) =>
        new(new SceneNodes.Node("snippet.tscn", header, header + "\n"), script, instance);
}
