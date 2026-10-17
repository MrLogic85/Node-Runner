using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Authored scenes stay loadable and exportable: Godot only warns, or exports silently drop
/// nodes, when these break. No Godot runtime is involved.
/// </summary>
public sealed partial class SceneContractTests
{
    private static readonly Lazy<HashSet<string>> _buttonExports = new(() =>
    {
        var button = CSharpSources.ProjectCompilation.GetTypeByMetadataName("NodeRunner.Ui.Lib.UiButton")!;
        return button.GetMembers()
            .Where(member => member.GetAttributes().Any(attribute => attribute.AttributeClass?.Name == "ExportAttribute"))
            .Select(member => member.Name)
            .ToHashSet();
    });

    // Export re-packs a scene and PackedScene.Pack drops nodes added inside an instance without its marker (#333).
    [Fact]
    public void Nodes_added_inside_an_instance_mark_it_editable()
    {
        var violations = SceneNodes.Files().SelectMany(file => EditableViolations(file.Scene, file.Text)).ToList();

        violations.ShouldBeEmpty(
            "Add [editable path=\"<instance>\"] (Editable Children in the editor); without it export drops the node.");
    }

    [Fact]
    public void Editable_guard_flags_a_child_inside_a_non_editable_instance()
    {
        const string scene = """
            [node name="Root" type="Control"]

            [node name="Frame" parent="." instance=ExtResource("1")]

            [node name="Inner" type="VBoxContainer" parent="Frame/Margin/Card"]

            [node name="Child" type="Label" parent="Frame/Margin/Card/Inner"]
            """;

        EditableViolations("test.tscn", scene).ShouldBe(["test.tscn: Frame/Margin/Card/Inner needs [editable path=\"Frame\"]"]);
        EditableViolations("test.tscn", scene + "\n[editable path=\"Frame\"]\n").ShouldBeEmpty();
    }

    // A restructured component scene leaves a screen's added node pointing at a vanished parent (#407).
    // Godot renames a duplicate sibling on load, so the second node's children lose their parent.
    [Fact]
    public void Every_parent_path_resolves_to_a_node_and_siblings_have_distinct_names()
    {
        var project = Path.Combine(SceneNodes.FindRepositoryRoot(), "project");
        string? Load(string resourcePath)
        {
            var path = Path.Combine(project, resourcePath["res://".Length..]);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        var violations = SceneNodes.Files().SelectMany(file => ParentViolations(file.Scene, file.Text, Load)).ToList();
        var duplicates = SceneNodes.Files()
            .SelectMany(file => SceneNodes.Parse(file.Scene, file.Text)
                .Where(node => !node.IsRoot)
                .GroupBy(node => (node.Parent, node.Name))
                .Where(group => group.Count() > 1)
                .Select(group => $"{file.Scene}: duplicate sibling {group.Key.Parent}/{group.Key.Name}"))
            .ToList();

        violations.Concat(duplicates).ShouldBeEmpty(
            "The parent moved or was renamed in the instanced scene; re-parent the node in the editor.");
    }

    [Fact]
    public void Parent_guard_flags_a_parent_that_vanished_from_a_nested_instance()
    {
        var scenes = new Dictionary<string, string>
        {
            ["res://Toolbar.tscn"] = """
                [node name="Toolbar" type="PanelContainer"]

                [node name="Margin" type="MarginContainer" parent="."]

                [node name="Content" type="HBoxContainer" parent="Margin"]
                """,
            ["res://Frame.tscn"] = """
                [ext_resource type="PackedScene" path="res://Toolbar.tscn" id="1"]

                [node name="Frame" type="PanelContainer"]

                [node name="Card" type="VBoxContainer" parent="."]

                [node name="Toolbar" parent="Card" instance=ExtResource("1")]
                """,
        };
        const string screen = """
            [ext_resource type="PackedScene" path="res://Frame.tscn" id="1"]

            [node name="Screen" type="Control"]

            [node name="Frame" parent="." instance=ExtResource("1")]

            [node name="Title" type="Label" parent="Frame/Card/Toolbar/Margin/Content"]

            [node name="Stale" type="Label" parent="Frame/Card/Toolbar/Content"]

            [node name="Body" parent="Frame/Card"]
            """;

        ParentViolations("test.tscn", screen, scenes.GetValueOrDefault).ShouldBe([
            "test.tscn: Frame/Card/Toolbar/Content/Stale has no parent Frame/Card/Toolbar/Content",
            "test.tscn: Frame/Card/Body overrides a node that does not exist",
        ]);
    }

    // Godot drops a script from a node that is not of its native base type, and the node shows
    // nothing (#805: UiPartRow became a BaseButton and the gallery's Control-typed rows vanished).
    [Fact]
    public void Every_scripted_node_has_its_scripts_native_base_type()
    {
        var compilation = CSharpSources.ProjectCompilation;
        var nativeBases = CSharpSources.Project
            .SelectMany(source =>
            {
                var model = compilation.GetSemanticModel(source.Tree);
                return source.Tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
                    .Select(declaration => (source.Path, Type: model.GetDeclaredSymbol(declaration) as INamedTypeSymbol));
            })
            // Godot runs the class named after its file.
            .Where(entry => entry.Type?.Name == Path.GetFileNameWithoutExtension(entry.Path) && NativeBase(entry.Type) is not null)
            .ToDictionary(entry => entry.Path, entry => NativeBase(entry.Type!)!);

        var scripted = SceneNodes.Files()
            .SelectMany(file => SceneNodes.Parse(file.Scene, file.Text))
            .Where(node => node.Type is not null && node.Script?.StartsWith("res://src/", StringComparison.Ordinal) == true)
            .Select(node => (Node: node, Base: nativeBases.GetValueOrDefault(node.Script!["res://src/".Length..])))
            .Where(entry => entry.Base is not null)
            .ToList();

        scripted.ShouldNotBeEmpty();
        scripted
            .Where(entry => compilation.GetTypeByMetadataName("Godot." + entry.Node.Type) is not { } type
                || !CSharpSources.DerivesFrom(type, entry.Base))
            .Select(entry => $"{entry.Node.Node}: script base {entry.Base!.Name}")
            .ShouldBeEmpty();
    }

    // Godot silently drops a saved property the node does not have: a UiButton caption saved as
    // the code-only LabelText showed nothing; the caption lives in native text.
    [Fact]
    public void Ui_buttons_save_only_godot_properties_and_exports()
    {
        var violations = SceneNodes.Files()
            .SelectMany(file => SceneNodes.Parse(file.Scene, file.Text))
            .Where(node => node.Script?.EndsWith("/UiButton.cs", StringComparison.Ordinal) == true)
            .SelectMany(node => UnknownButtonProperties(node.Node.Body).Select(key => $"{node.Node}: {key}"))
            .ToList();

        violations.ShouldBeEmpty("Save only Godot properties and UiButton [Export]s; set a caption in Text.");
    }

    [Theory]
    [InlineData("text = \"Go\"", false)]
    [InlineData("IconId = 3", false)]
    [InlineData("LabelText = \"Go\"", true)]
    public void Button_property_guard_flags_a_property_godot_drops(string property, bool flagged)
    {
        var body = $"[node name=\"Go\" type=\"Button\" parent=\".\"]\n{property}\n";

        UnknownButtonProperties(body).Any().ShouldBe(flagged);
    }

    // Code that finds scene nodes by a name or an index: ImportScreen finds its part rows by kind (#899),
    // UiIconTabs' index is the group's index in PartTray.Groups(), and UiPickList.ShowInfoUnder moves
    // its Info line between its own rows.
    [Fact]
    public void Scene_nodes_that_code_looks_up_exist()
    {
        var import = SceneNodes.InScene("screens/ImportScreen.tscn");
        var tabs = SceneNodes.InScene("screens/BuildScreen.tscn").Single(node => node.Name == "PartTabs").Node.Body;
        var pickLists = SceneNodes.Files()
            .Select(file => SceneNodes.Parse(file.Scene, file.Text).ToList())
            .SelectMany(nodes => nodes
                .Where(node => node.Script?.EndsWith("/UiPickList.cs", StringComparison.Ordinal) == true)
                .Select(list => (List: list, Nodes: nodes)))
            .ToList();

        import.Where(node => node.Parent?.EndsWith("/PartRows", StringComparison.Ordinal) == true)
            .Select(node => node.Name)
            .ShouldBe(Enum.GetNames<NodeRunner.Domain.PartKind>(), ignoreOrder: true);
        TabIcons().Match(tabs).Groups["icons"].Value.Split(", ").Length
            .ShouldBe(NodeRunner.App.ViewModels.PartTray.Groups().Count);
        pickLists.ShouldNotBeEmpty();
        pickLists
            .Where(entry => !entry.Nodes.Any(node => node.Name == PickListInfo().Match(entry.List.Node.Body).Groups["name"].Value
                && node.Parent == (entry.List.IsRoot ? "." : NodePath(entry.List.Parent!, entry.List.Name))))
            .Select(entry => $"{entry.List.Node}: Info is not a node directly under the list")
            .ShouldBeEmpty();
    }

    // A level saved in a scene would bypass UiLayers (#768). A scene's CanvasLayer is a UiLevelLayer,
    // which keeps Overlay, or a world backdrop under the screen, like Training's arena background.
    [Fact]
    public void Scenes_save_no_canvas_layer_level_over_the_screen()
    {
        var violations = SceneNodes.Files()
            .SelectMany(file => SceneNodes.Parse(file.Scene, file.Text))
            .Where(SavesUnnamedLevel)
            .Select(node => node.Node)
            .ToList();

        violations.ShouldBeEmpty("Use a UiLevelLayer, and set any other UI level in code from UiLayers (#768).");
    }

    [Theory]
    [InlineData("type=\"CanvasLayer\"", "", true)]
    [InlineData("type=\"CanvasLayer\"", "layer = -1", false)]
    [InlineData("type=\"CanvasLayer\"", "layer = 2\nscript = ExtResource(\"1\")", true)]
    [InlineData("type=\"CanvasLayer\"", "script = ExtResource(\"1\")", false)]
    [InlineData("instance=ExtResource(\"2\")", "layer = 2", true)]
    public void Level_guard_flags_a_saved_level_over_the_screen(string kind, string properties, bool flagged)
    {
        var scene = $"""
            [ext_resource type="Script" path="res://src/ui/lib/UiLevelLayer.cs" id="1"]

            [node name="Layer" {kind} parent="."]
            {properties}

            """;

        SceneNodes.Parse("Snippet.tscn", scene).Count(SavesUnnamedLevel).ShouldBe(flagged ? 1 : 0);
    }

    private static bool SavesUnnamedLevel(SceneNodes.SceneNode node)
    {
        var match = SavedLayer().Match(node.Node.Body);
        int? saved = match.Success ? int.Parse(match.Groups["layer"].Value, CultureInfo.InvariantCulture) : null;
        if (node.Script?.EndsWith("/UiLevelLayer.cs", StringComparison.Ordinal) == true)
        {
            return saved is not null;
        }

        // Godot's default layer, saved as nothing, is already over the screen.
        return node.Type == "CanvasLayer" ? saved is not < UiLayers.Screen : saved is >= UiLayers.Screen;
    }

    // Godot saves its own properties in snake_case, and a script's [Export]s by their C# name.
    private static IEnumerable<string> UnknownButtonProperties(string body) =>
        PascalCaseProperty().Matches(body).Select(match => match.Groups["key"].Value).Where(key => !_buttonExports.Value.Contains(key));

    // The first Godot class the script derives from: what Godot instantiates before attaching it.
    private static INamedTypeSymbol? NativeBase(INamedTypeSymbol? type)
    {
        for (var current = type?.BaseType; current is not null; current = current.BaseType)
        {
            if (current.ContainingNamespace.ToDisplayString() == "Godot")
            {
                return current;
            }
        }

        return null;
    }

    private static IEnumerable<string> EditableViolations(string scene, string text)
    {
        var nodes = SceneNodes.Parse(scene, text).Where(node => !node.IsRoot).ToList();
        var declared = nodes.Where(HeaderDeclares).Select(node => NodePath(node.Parent!, node.Name)).ToHashSet();
        var editable = Editable().Matches(text).Select(match => match.Groups["path"].Value).ToHashSet();

        foreach (var node in nodes.Where(node => HeaderDeclares(node) && node.Parent != "." && !declared.Contains(node.Parent!)))
        {
            var instance = Ancestors(node.Parent!).FirstOrDefault(declared.Contains) ?? node.Parent!;
            if (!editable.Contains(instance))
            {
                yield return $"{scene}: {NodePath(node.Parent!, node.Name)} needs [editable path=\"{instance}\"]";
            }
        }
    }

    private static IEnumerable<string> ParentViolations(string scene, string text, Func<string, string?> load)
    {
        var known = KnownPaths(scene, text, load, []);
        foreach (var node in SceneNodes.Parse(scene, text))
        {
            if (node.Parent is not { } parent)
            {
                continue;
            }

            var path = NodePath(parent, node.Name);
            if (parent != "." && !known.Contains(parent))
            {
                yield return $"{scene}: {path} has no parent {parent}";
            }
            else if (!IsDeclared(node) && !known.Contains(path))
            {
                yield return $"{scene}: {path} overrides a node that does not exist";
            }
        }
    }

    // The scene's own nodes plus every node of each instanced scene, recursively.
    private static HashSet<string> KnownPaths(string scene, string text, Func<string, string?> load, HashSet<string> visiting)
    {
        var known = new HashSet<string> { string.Empty };
        foreach (var node in SceneNodes.Parse(scene, text))
        {
            var path = node.Parent is { } parent ? NodePath(parent, node.Name) : string.Empty;
            if (IsDeclared(node))
            {
                known.Add(path);
            }

            if (node.Instance is not { } instance || !visiting.Add(instance))
            {
                continue;
            }

            if (instance.EndsWith(".tscn", StringComparison.Ordinal) && load(instance) is { } instanced)
            {
                foreach (var inner in KnownPaths(instance, instanced, load, visiting))
                {
                    known.Add(inner.Length == 0 ? path : NodePath(path.Length == 0 ? "." : path, inner));
                }
            }

            visiting.Remove(instance);
        }

        return known;
    }

    /// <summary>A block that creates a node; any other block only overrides a node of an instance.</summary>
    private static bool IsDeclared(SceneNodes.SceneNode node) => node.Type is not null || node.Instance is not null;

    private static bool HeaderDeclares(SceneNodes.SceneNode node) =>
        node.Node.Header.Contains(" type=", StringComparison.Ordinal) || node.Node.Header.Contains(" instance=", StringComparison.Ordinal);

    private static string NodePath(string parent, string name) => parent == "." ? name : parent + "/" + name;

    private static IEnumerable<string> Ancestors(string path)
    {
        for (var slash = path.LastIndexOf('/'); slash > 0; slash = path.LastIndexOf('/', slash - 1))
        {
            yield return path[..slash];
        }
    }

    [GeneratedRegex("""(?m)^\[editable path="(?<path>[^"]+)"\]""")]
    private static partial Regex Editable();

    [GeneratedRegex(@"(?m)^layer = (?<layer>-?\d+)$")]
    private static partial Regex SavedLayer();

    [GeneratedRegex(@"(?m)^(?<key>[A-Z]\w*) = ")]
    private static partial Regex PascalCaseProperty();

    [GeneratedRegex(@"Icons = Array\[int\]\(\[(?<icons>[^\]]+)\]\)")]
    private static partial Regex TabIcons();

    [GeneratedRegex("\nInfo = NodePath\\(\"(?<name>[^\"/]+)\"\\)")]
    private static partial Regex PickListInfo();
}
