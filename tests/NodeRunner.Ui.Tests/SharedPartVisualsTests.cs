using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Guards #766: every view draws a creature's parts through the shared part visuals in
/// <c>theme/</c> (<c>JointPart</c>, <c>BeamPart</c>, <c>PistonPart</c>, <c>SensorPart</c>), so a
/// part looks and layers the same in Build and Training. Only those parts call the drawing
/// helpers that paint a joint, Piston or sensor, or a selection mark; a view feeds them state
/// instead. A beam's rod and the rigid hatch are plain pen lines with no helper to guard, so
/// this test cannot catch a view drawing those itself.
/// </summary>
public sealed class SharedPartVisualsTests
{
    private static readonly (string Type, string Method)[] _partDrawing =
    [
        ("JointDrawing", "DrawPlain"),
        ("PistonDrawing", "Draw"),
        ("SensorDrawing", "DrawAccelerometer"),
        ("SensorDrawing", "DrawCamera"),
        ("SelectionDrawing", "DrawBeam"),
        ("SelectionDrawing", "DrawJoint"),
    ];

    [Fact]
    public void Only_the_shared_parts_draw_a_part()
    {
        var violations = CSharpSources.Project
            .Where(source => !IsSharedPart(source.Path))
            .SelectMany(source => source.Find(DrawsAPart))
            .ToList();

        violations.ShouldBeEmpty("Show the part with its shared part visual in theme/ (#766) instead of drawing it.");
    }

    [Theory]
    [InlineData("void M(CanvasItem canvas) { JointDrawing.DrawPlain(canvas, theme, transform, at, 1, JointLook.Plain); }")]
    [InlineData("void M(CanvasItem canvas) { SelectionDrawing.DrawBeam(canvas, transform, glow, 1, 1, a, b); }")]
    public void Drawing_a_part_is_flagged(string member) =>
        CSharpSources.Snippet(member).Find(DrawsAPart).ShouldHaveSingleItem();

    [Fact]
    public void Feeding_a_part_passes() =>
        CSharpSources.Snippet("void M(JointPart part) { part.Radius = 1; JointDrawing.BeamSpan(1, a, 1, b, 1); }")
            .Find(DrawsAPart)
            .ShouldBeEmpty();

    private static bool IsSharedPart(string path) =>
        path.StartsWith("theme/", StringComparison.Ordinal) && path.EndsWith("Part.cs", StringComparison.Ordinal);

    private static bool DrawsAPart(SyntaxNode node) =>
        node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                Expression: IdentifierNameSyntax type,
                Name.Identifier.Text: var method,
            },
        }
        && _partDrawing.Contains((type.Identifier.Text, method));
}
