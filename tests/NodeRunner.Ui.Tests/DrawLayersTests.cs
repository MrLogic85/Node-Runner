using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Guards #767: a drawn creature and Training's world order their drawing through the named
/// layers in <see cref="CreatureLayers"/> and <see cref="ArenaLayers"/>, never ZIndex numbers.
/// A part's effective z is its creature root's layer plus its own, as every body sits at 0.
/// </summary>
public sealed class DrawLayersTests
{
    private const string _partVisual = "theme/PartVisual.cs";

    private static readonly string[] _layerClasses = [nameof(CreatureLayers), nameof(ArenaLayers)];

    private static readonly int[] _creatureLayers = typeof(CreatureLayers)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.Name != nameof(CreatureLayers.Count))
        .Select(field => (int)field.GetRawConstantValue()!)
        .ToArray();

    [Fact]
    public void Creature_layers_stack_each_selected_part_over_its_kind()
    {
        int[] order =
        [
            CreatureLayers.Knockout,
            CreatureLayers.Hatch,
            CreatureLayers.Underlays,
            CreatureLayers.Beams,
            CreatureLayers.Links,
            CreatureLayers.SelectedLinks,
            CreatureLayers.Sensors,
            CreatureLayers.SelectedSensors,
            CreatureLayers.Joints,
            CreatureLayers.SelectedJoints,
            CreatureLayers.Overlays,
        ];

        order.ShouldBeInOrder(SortDirection.Ascending, "Each layer draws over the one before it.");
        order.Distinct().Count().ShouldBe(order.Length);
        _creatureLayers.Order().ToArray().ShouldBe(order, "Every layer has its place in this order.");
        _creatureLayers.ShouldAllBe(layer => layer >= 0 && layer < CreatureLayers.Count);
    }

    [Fact]
    public void Arena_marks_and_ground_are_under_every_creature() =>
        new[] { ArenaLayers.BestMarker, ArenaLayers.StartSign, ArenaLayers.Ground }.ShouldAllBe(layer => layer < ArenaLayers.Shadows + LowestCreatureLayer);

    [Fact]
    public void Followed_creature_is_wholly_over_every_shadow() =>
        (ArenaLayers.Followed + LowestCreatureLayer).ShouldBeGreaterThan(ArenaLayers.Shadows + HighestCreatureLayer);

    [Fact]
    public void Drawing_outside_the_ui_sets_z_only_from_named_layers()
    {
        var violations = CSharpSources.Project
            .Where(source => !source.Path.StartsWith("ui/", StringComparison.Ordinal))
            .SelectMany(source => source.Find(node => SetsUnnamedZIndex(node, source.Path)))
            .ToList();

        violations.ShouldBeEmpty("Set ZIndex from CreatureLayers or ArenaLayers (#767), so the draw order lives in one place.");
    }

    [Fact]
    public void Part_visuals_take_their_layers_by_name()
    {
        var violations = CSharpSources.Project
            .SelectMany(source => source.Find(LeavesItsLayersUnnamed))
            .ToList();

        violations.ShouldBeEmpty("Every constructor of a PartVisual passes its two CreatureLayers to base(...).");
    }

    [Theory]
    [InlineData("class Part : PartVisual { }")]
    [InlineData("class Part : PartVisual { Part() : base() { } }")]
    [InlineData("class Part : PartVisual { Part() : base(-1, 3) { } }")]
    [InlineData("class Part : PartVisual { Part() : base(ArenaLayers.Ground, ArenaLayers.Ground) { } }")]
    [InlineData("class Part() : PartVisual(CreatureLayers.Beams, CreatureLayers.SelectedLinks) { }")]
    public void Part_without_named_layers_is_flagged(string member) =>
        CSharpSources.Snippet(member).Find(LeavesItsLayersUnnamed).ShouldHaveSingleItem();

    [Fact]
    public void Part_with_named_layers_passes() =>
        CSharpSources.Snippet("class Part : PartVisual { Part() : base(CreatureLayers.Beams, CreatureLayers.SelectedLinks) { } }")
            .Find(LeavesItsLayersUnnamed)
            .ShouldBeEmpty();

    [Theory]
    [InlineData("void M(Node2D node) { node.ZIndex = -1; }")]
    [InlineData("void M(Node2D node, int z) { node.ZIndex = z; }")]
    [InlineData("void M(Node2D node) { node.ZIndex = CreatureLayers.Joints + 1; }")]
    [InlineData("void M(Node2D node) { node.ZIndex = ArenaLayers.BestMarker - ArenaLayers.Ground; }")]
    public void Unnamed_z_is_flagged(string member) =>
        CSharpSources.Snippet(member).Find(node => SetsUnnamedZIndex(node, "creature/Snippet.cs")).ShouldHaveSingleItem();

    [Theory]
    [InlineData("void M(Node2D node) { node.ZIndex = CreatureLayers.Overlays; }")]
    [InlineData("void M(Node2D node, bool shadow) { node.ZIndex = shadow ? ArenaLayers.Shadows : ArenaLayers.Followed; }")]
    public void Named_z_passes(string member) =>
        CSharpSources.Snippet(member).Find(node => SetsUnnamedZIndex(node, "creature/Snippet.cs")).ShouldBeEmpty();

    private static int LowestCreatureLayer => _creatureLayers.Min();

    private static int HighestCreatureLayer => _creatureLayers.Max();

    // PartVisual alone applies the layers its subclasses name in their base call.
    private static bool SetsUnnamedZIndex(SyntaxNode node, string path) =>
        path != _partVisual
        && node is AssignmentExpressionSyntax
        {
            Left: IdentifierNameSyntax { Identifier.Text: "ZIndex" }
                or MemberAccessExpressionSyntax { Name.Identifier.Text: "ZIndex" },
        } assignment
        && !IsNamedLayers(assignment.Right);

    private static bool LeavesItsLayersUnnamed(SyntaxNode node) =>
        node is ClassDeclarationSyntax { BaseList: { } baseList } declaration
        && baseList.Types.Any(type => type.Type.ToString() == nameof(PartVisual))
        && !(baseList.Types.All(type => type is SimpleBaseTypeSyntax)
            && declaration.Members.OfType<ConstructorDeclarationSyntax>().ToList() is { Count: > 0 } constructors
            && constructors.All(NamesItsLayers));

    private static bool NamesItsLayers(ConstructorDeclarationSyntax constructor) =>
        constructor.Initializer is { ThisOrBaseKeyword.Text: "base", ArgumentList.Arguments: { Count: 2 } arguments }
        && arguments.All(argument => argument.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.Text: nameof(CreatureLayers) },
        });

    // Layers are chosen with ?:, but every choice is a named layer.
    private static bool IsNamedLayers(ExpressionSyntax expression) => expression switch
    {
        ParenthesizedExpressionSyntax parenthesized => IsNamedLayers(parenthesized.Expression),
        ConditionalExpressionSyntax conditional => IsNamedLayers(conditional.WhenTrue) && IsNamedLayers(conditional.WhenFalse),
        _ => IsLayer(expression),
    };

    private static bool IsLayer(ExpressionSyntax expression) =>
        expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax owner }
        && _layerClasses.Contains(owner.Identifier.Text);
}
