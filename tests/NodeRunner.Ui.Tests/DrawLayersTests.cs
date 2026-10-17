using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NodeRunner.Domain;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Guards #767: a drawn creature and Training's world order their drawing through the named
/// layers in <see cref="CreatureLayers"/> and <see cref="ArenaLayers"/>, never ZIndex numbers, and
/// a part in a creature's draw groups (#1107) by its <see cref="DrawSlot"/>.
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

    private static readonly int[] _arenaLayers = typeof(ArenaLayers)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral)
        .Select(field => (int)field.GetRawConstantValue()!)
        .ToArray();

    [Fact]
    public void Creature_layers_stack_the_selected_surface_over_the_unselected()
    {
        int[] order =
        [
            CreatureLayers.Knockout,
            CreatureLayers.Hatch,
            CreatureLayers.Parts,
            CreatureLayers.LooseJoints,
            CreatureLayers.SelectedUnderlays,
            CreatureLayers.SelectedParts,
            CreatureLayers.SelectedLooseJoints,
            CreatureLayers.Overlays,
        ];

        order.ShouldBeInOrder(SortDirection.Ascending, "Each layer draws over the one before it.");
        order.Distinct().Count().ShouldBe(order.Length);
        _creatureLayers.Order().ToArray().ShouldBe(order, "Every layer has its place in this order.");
        _creatureLayers.ShouldAllBe(layer => layer >= 0 && layer < CreatureLayers.Count);
    }

    // #1107: a part's layer is its draw rank on its surface, the same rank Build's touches use.
    [Fact]
    public void A_parts_layer_follows_its_draw_rank_on_its_surface()
    {
        CreatureLayers.Of(DrawSlot.Under, 0, selected: false).ShouldBe(CreatureLayers.Parts);
        CreatureLayers.Of(DrawSlot.Over, DrawGroups.MaxGroups - 1, selected: false).ShouldBe(CreatureLayers.LooseJoints - 1);
        CreatureLayers.Of(DrawSlot.Under, group: null, selected: false).ShouldBe(CreatureLayers.LooseJoints);
        CreatureLayers.Of(DrawSlot.Over, group: null, selected: false).ShouldBe(CreatureLayers.SelectedUnderlays - 1);
        CreatureLayers.Of(DrawSlot.Under, 0, selected: true).ShouldBe(CreatureLayers.SelectedParts);
        CreatureLayers.Of(DrawSlot.Under, group: null, selected: true).ShouldBe(CreatureLayers.SelectedLooseJoints);
        CreatureLayers.Of(DrawSlot.Over, group: null, selected: true).ShouldBe(CreatureLayers.Overlays - 1);
        CreatureLayers.Of(DrawSlot.Link, 3, selected: true).ShouldBe(CreatureLayers.Of(DrawSlot.Link, 3, selected: false) + CreatureLayers.SelectedParts - CreatureLayers.Parts);
    }

    // #1269: a joint's bands draw under its ring and the hinge part on it, by layer, not by the
    // order the parts are made in, on either surface and for a joint joined to no link too.
    [Theory]
    [InlineData(DrawGroups.MaxGroups - 1, true)]
    [InlineData(null, false)]
    public void A_joints_bands_draw_over_its_links_and_under_its_ring_and_hinge_part(int? group, bool selected)
    {
        CreatureLayers.Of(DrawSlot.Band, group, selected).ShouldBeGreaterThan(CreatureLayers.Of(DrawSlot.Sensor, group, selected));
        CreatureLayers.Of(DrawSlot.Band, group, selected).ShouldBeLessThan(CreatureLayers.Of(DrawSlot.Over, group, selected));
    }

    [Fact]
    public void Arena_layers_put_marks_under_every_creature_and_the_followed_one_over_every_shadow_within_godots_z_range()
    {
        ((long)_arenaLayers.Min()).ShouldBeGreaterThanOrEqualTo(Godot.RenderingServer.CanvasItemZMin);
        ((long)(ArenaLayers.Followed + HighestCreatureLayer)).ShouldBeLessThanOrEqualTo(Godot.RenderingServer.CanvasItemZMax);
        new[] { ArenaLayers.BestMarker, ArenaLayers.StartSign, ArenaLayers.Ground }.ShouldAllBe(layer => layer < ArenaLayers.Shadows + LowestCreatureLayer);
        (ArenaLayers.Followed + LowestCreatureLayer).ShouldBeGreaterThan(ArenaLayers.Shadows + HighestCreatureLayer);
    }

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

        violations.ShouldBeEmpty("Every constructor of a PartVisual passes its DrawSlot, or its CreatureLayers layer, to base(...).");
    }

    [Theory]
    [InlineData("class Part : PartVisual { }", true)]
    [InlineData("class Part : PartVisual { Part() : base(-1) { } }", true)]
    [InlineData("class Part : PartVisual { Part() : base(ArenaLayers.Ground) { } }", true)]
    [InlineData("class Part() : PartVisual(CreatureLayers.Hatch) { }", true)]
    [InlineData("class Part : PartVisual { Part() : base(DrawSlot.Link, DrawSlot.Over) { } }", true)]
    [InlineData("class Part : PartVisual { Part() : base(DrawSlot.Link) { } }", false)]
    public void Part_without_named_layers_is_flagged(string member, bool flagged) =>
        CSharpSources.Snippet(member).Find(LeavesItsLayersUnnamed).Count().ShouldBe(flagged ? 1 : 0);

    [Theory]
    [InlineData("void M(Node2D node) { node.ZIndex = CreatureLayers.Parts + 1; }", true)]
    [InlineData("void M(Node2D node, bool shadow) { node.ZIndex = shadow ? ArenaLayers.Shadows : ArenaLayers.Followed; }", false)]
    public void Unnamed_z_is_flagged(string member, bool flagged) =>
        CSharpSources.Snippet(member).Find(node => SetsUnnamedZIndex(node, "creature/Snippet.cs")).Count().ShouldBe(flagged ? 1 : 0);

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

    // A part in a creature's draw groups names its DrawSlot (#1107); any other its CreatureLayers layer.
    private static bool NamesItsLayers(ConstructorDeclarationSyntax constructor) =>
        constructor.Initializer is { ThisOrBaseKeyword.Text: "base", ArgumentList.Arguments: var arguments }
        && arguments is [{ Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: nameof(DrawSlot) or nameof(CreatureLayers) } } }];

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
