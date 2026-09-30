namespace NodeRunner.Ui.Tests;

/// <summary>
/// Guards authored scenes against nodes whose saved parent path no longer exists. When a
/// component scene is restructured, a screen that adds nodes inside its instance can point at
/// a vanished parent; Godot only warns at instantiation and drops the node (#407). The known
/// paths are the scene's own nodes plus every node of each instanced scene, recursively.
/// </summary>
public sealed class SceneParentPathTests
{
    [Fact]
    public void Every_parent_path_resolves_to_a_node()
    {
        var project = Path.Combine(SceneNodes.FindRepositoryRoot(), "project");
        string? Load(string resourcePath)
        {
            var path = Path.Combine(project, resourcePath["res://".Length..]);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        var violations = SceneNodes.Files()
            .SelectMany(file => Violations(file.Scene, file.Text, Load))
            .ToList();

        violations.ShouldBeEmpty(
            "The parent moved or was renamed in the instanced scene; re-parent the node in the editor.");
    }

    [Fact]
    public void Sibling_nodes_have_distinct_names()
    {
        // Godot renames a duplicate sibling on load, so the second node's children lose their parent.
        var duplicates = SceneNodes.Files()
            .SelectMany(file => SceneNodes.Parse(file.Scene, file.Text)
                .Where(node => !node.IsRoot)
                .GroupBy(node => (node.Parent, node.Name))
                .Where(group => group.Count() > 1)
                .Select(group => $"{file.Scene}: {group.Key.Parent}/{group.Key.Name}"))
            .ToList();

        duplicates.ShouldBeEmpty();
    }

    [Fact]
    public void Guard_flags_a_parent_that_vanished_from_a_nested_instance()
    {
        const string toolbar = """
            [ext_resource type="Script" path="res://Toolbar.cs" id="1"]

            [node name="Toolbar" type="PanelContainer"]
            script = ExtResource("1")

            [node name="Margin" type="MarginContainer" parent="."]

            [node name="Content" type="HBoxContainer" parent="Margin"]
            """;
        const string frame = """
            [ext_resource type="PackedScene" path="res://Toolbar.tscn" id="1"]

            [node name="Frame" type="PanelContainer"]

            [node name="Card" type="VBoxContainer" parent="."]

            [node name="Toolbar" parent="Card" instance=ExtResource("1")]
            """;
        const string screen = """
            [ext_resource type="PackedScene" path="res://Frame.tscn" id="1"]

            [node name="Screen" type="Control"]

            [node name="Frame" parent="." instance=ExtResource("1")]

            [node name="Title" type="Label" parent="Frame/Card/Toolbar/Margin/Content"]

            [node name="Subtitle" type="Label" parent="Frame/Card/Toolbar/Margin/Content/Title"]

            [node name="Stale" type="Label" parent="Frame/Card/Toolbar/Content"]

            [node name="Margin" parent="Frame/Card/Toolbar"]

            [node name="Body" parent="Frame/Card"]

            [node name="Hint" type="Label" parent="Frame/Card/Body"]

            [editable path="Frame"]
            """;
        var scenes = new Dictionary<string, string>
        {
            ["res://Toolbar.tscn"] = toolbar,
            ["res://Frame.tscn"] = frame,
        };

        Violations("test.tscn", screen, scenes.GetValueOrDefault)
            .ShouldBe([
                "test.tscn: Frame/Card/Toolbar/Content/Stale has no parent Frame/Card/Toolbar/Content",
                "test.tscn: Frame/Card/Body overrides a node that does not exist",
                "test.tscn: Frame/Card/Body/Hint has no parent Frame/Card/Body",
            ]);
    }

    private static IEnumerable<string> Violations(string scene, string text, Func<string, string?> load)
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

    /// <summary>A block that creates a node; any other block only overrides a node of an instance.</summary>
    private static bool IsDeclared(SceneNodes.SceneNode node) => node.Type is not null || node.Instance is not null;

    private static HashSet<string> KnownPaths(
        string scene,
        string text,
        Func<string, string?> load,
        HashSet<string> visiting)
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

    private static string NodePath(string parent, string name) => parent == "." ? name : parent + "/" + name;
}
