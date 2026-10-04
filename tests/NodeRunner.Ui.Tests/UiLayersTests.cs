using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Guards #768: the UI stacks its levels with the named CanvasLayers in <see cref="UiLayers"/>,
/// never ZIndex (see <c>UiSourceGuardTests</c>) or a CanvasLayer number of its own in code or a scene.
/// </summary>
public sealed class UiLayersTests
{
    [Fact]
    public void Levels_stack_screen_then_menus_then_notifications()
    {
        int[] order = [UiLayers.Screen, UiLayers.Overlay, UiLayers.Notification];

        order.ShouldBeInOrder(SortDirection.Ascending, "Each level draws over the one before it.");
        order.Distinct().Count().ShouldBe(order.Length);
        typeof(UiLayers)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (int)field.GetRawConstantValue()!)
            .Order()
            .ToArray()
            .ShouldBe(order, "Every level has its place in this order.");
    }

    [Fact]
    public void Canvas_layers_take_their_level_by_name()
    {
        var violations = CSharpSources.Project
            .SelectMany(source => source.Find(SetsUnnamedLayer))
            .ToList();

        violations.ShouldBeEmpty("Set a CanvasLayer's Layer from UiLayers (#768), so the UI levels live in one place.");
    }

    // A level saved in a scene would bypass UiLayers. A scene's CanvasLayer is a UiLevelLayer,
    // which keeps Overlay, or a world backdrop under the screen, like Training's arena background.
    [Fact]
    public void Scenes_save_no_level_over_the_screen()
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
    [InlineData("type=\"CanvasLayer\"", "layer = 50", true)]
    [InlineData("type=\"CanvasLayer\"", "layer = -1", false)]
    [InlineData("type=\"CanvasLayer\"", "script = ExtResource(\"1\")", false)]
    [InlineData("type=\"CanvasLayer\"", "layer = 2\nscript = ExtResource(\"1\")", true)]
    [InlineData("instance=ExtResource(\"2\")", "layer = 2", true)]
    [InlineData("type=\"Control\"", "", false)]
    public void Scene_level_is_checked(string kind, string properties, bool flagged)
    {
        var scene = $"""
            [ext_resource type="Script" path="res://src/ui/lib/UiLevelLayer.cs" id="1"]
            [ext_resource type="PackedScene" path="res://scenes/ui/Some.tscn" id="2"]

            [node name="Layer" {kind} parent="."]
            {properties}

            """;

        SceneNodes.Parse("Snippet.tscn", scene).Count(SavesUnnamedLevel).ShouldBe(flagged ? 1 : 0);
    }

    [Theory]
    [InlineData("void M(CanvasLayer layer) { layer.Layer = 50; }")]
    [InlineData("CanvasLayer M() => new CanvasLayer { Layer = 1 };")]
    [InlineData("void M(CanvasLayer layer) { layer.Layer = UiLayers.Overlay + 1; }")]
    public void Unnamed_level_is_flagged(string member) =>
        CSharpSources.Snippet(member).Find(SetsUnnamedLayer).ShouldHaveSingleItem();

    [Fact]
    public void Named_level_passes() =>
        CSharpSources.Snippet("void M(CanvasLayer layer) { layer.Layer = UiLayers.Notification; }")
            .Find(SetsUnnamedLayer)
            .ShouldBeEmpty();

    private static bool SavesUnnamedLevel(SceneNodes.SceneNode node)
    {
        var match = Regex.Match(node.Node.Body, @"(?m)^layer = (?<layer>-?\d+)$");
        int? saved = match.Success ? int.Parse(match.Groups["layer"].Value, CultureInfo.InvariantCulture) : null;
        if (node.Script?.EndsWith("/UiLevelLayer.cs", StringComparison.Ordinal) == true)
        {
            return saved is not null;
        }

        // Godot's default layer, saved as nothing, is already over the screen.
        return node.Type == "CanvasLayer" ? saved is not < UiLayers.Screen : saved is >= UiLayers.Screen;
    }

    private static bool SetsUnnamedLayer(SyntaxNode node) =>
        node is AssignmentExpressionSyntax
        {
            Left: IdentifierNameSyntax { Identifier.Text: "Layer" }
                or MemberAccessExpressionSyntax { Name.Identifier.Text: "Layer" },
        } assignment
        && assignment.Right is not MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.Text: nameof(UiLayers) },
        };
}
