using System.Text.RegularExpressions;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Guards authored scenes against nodes added inside an instanced scene without its
/// <c>[editable path=…]</c> marker. Loading such a scene still works, but export re-packs it
/// and <c>PackedScene.Pack</c> silently drops those nodes (#333). No Godot runtime is involved.
/// </summary>
public sealed partial class SceneEditableChildrenTests
{
    [Fact]
    public void Nodes_added_inside_an_instance_mark_it_editable()
    {
        var scenes = Path.Combine(SceneNodes.FindRepositoryRoot(), "project", "scenes");
        var violations = Directory.EnumerateFiles(scenes, "*.tscn", SearchOption.AllDirectories)
            .SelectMany(path => Violations(Path.GetFileName(path), File.ReadAllText(path)))
            .ToList();

        violations.ShouldBeEmpty(
            "Add [editable path=\"<instance>\"] (Editable Children in the editor); without it export drops the node.");
    }

    [Fact]
    public void Guard_flags_a_child_inside_a_non_editable_instance()
    {
        const string scene = """
            [node name="Root" type="Control"]

            [node name="Frame" parent="." instance=ExtResource("1")]

            [node name="Inner" type="VBoxContainer" parent="Frame/Margin/Card"]

            [node name="Child" type="Label" parent="Frame/Margin/Card/Inner"]

            [node name="Direct" type="Label" parent="Frame"]
            """;

        Violations("test.tscn", scene).ShouldBe(["test.tscn: Frame/Margin/Card/Inner needs [editable path=\"Frame\"]"]);
        Violations("test.tscn", scene + "\n[editable path=\"Frame\"]\n").ShouldBeEmpty();
    }

    private static IEnumerable<string> Violations(string scene, string text)
    {
        var nodes = NodeHeader().Matches(text).Select(match => new
        {
            Path = NodePath(match.Groups["parent"].Value, match.Groups["name"].Value),
            Parent = match.Groups["parent"].Value,
            Header = match.Value,
        }).ToList();
        var declared = nodes
            .Where(node => node.Header.Contains(" type=", StringComparison.Ordinal) ||
                           node.Header.Contains(" instance=", StringComparison.Ordinal))
            .Select(node => node.Path)
            .ToHashSet();
        var editable = Editable().Matches(text).Select(match => match.Groups["path"].Value).ToHashSet();

        foreach (var node in nodes.Where(node => declared.Contains(node.Path) && node.Parent.Length > 0))
        {
            var parent = node.Parent == "." ? string.Empty : node.Parent;
            if (parent.Length == 0 || declared.Contains(parent))
            {
                continue;
            }

            var instance = Ancestors(parent).FirstOrDefault(declared.Contains) ?? parent;
            if (!editable.Contains(instance))
            {
                yield return $"{scene}: {node.Path} needs [editable path=\"{instance}\"]";
            }
        }
    }

    private static string NodePath(string parent, string name) => parent switch
    {
        "" => string.Empty,
        "." => name,
        _ => parent + "/" + name,
    };

    private static IEnumerable<string> Ancestors(string path)
    {
        for (var slash = path.LastIndexOf('/'); slash > 0; slash = path.LastIndexOf('/', slash - 1))
        {
            yield return path[..slash];
        }
    }

    [GeneratedRegex("""(?m)^\[node name="(?<name>[^"]+)"(?:[^\]]*? parent="(?<parent>[^"]*)")?[^\]]*\]""")]
    private static partial Regex NodeHeader();

    [GeneratedRegex("""(?m)^\[editable path="(?<path>[^"]+)"\]""")]
    private static partial Regex Editable();
}
