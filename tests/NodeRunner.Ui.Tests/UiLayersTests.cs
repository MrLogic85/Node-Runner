using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Guards #768: the UI stacks its levels with the named CanvasLayers in <see cref="UiLayers"/>,
/// never ZIndex (see <c>UiSourceGuardTests</c>) or a CanvasLayer number of its own in code or a scene
/// (scenes: <c>SceneContractTests</c>).
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

    [Theory]
    [InlineData("void M(CanvasLayer layer) { layer.Layer = UiLayers.Overlay + 1; }", true)]
    [InlineData("CanvasLayer M() => new CanvasLayer { Layer = 1 };", true)]
    [InlineData("void M(CanvasLayer layer) { layer.Layer = UiLayers.Notification; }", false)]
    public void Unnamed_level_is_flagged(string member, bool flagged) =>
        CSharpSources.Snippet(member).Find(SetsUnnamedLayer).Count().ShouldBe(flagged ? 1 : 0);

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
