using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// <see cref="UiBaseline"/>'s maths, and a guard that the rows whose label and value share a line
/// never place a text by a constant vertical offset. The pixels need the
/// engine's font metrics, so they are checked by hand.
/// </summary>
public sealed class UiBaselineTests
{
    private static readonly string[] _rows = ["ui/lib/UiValueRow.cs", "ui/lib/UiPartRow.cs", "ui/lib/UiSlider.cs"];

    private static readonly string[] _placements = ["Position", "CustomMinimumSize", "OffsetTop", "OffsetBottom"];

    private static readonly string[] _verticalMargins = ["margin_top", "margin_bottom"];

    [Fact]
    public void Heights_GiveEveryLabelTheLargestAscent_AndKeepItsOwnDescent()
    {
        // A caption, a larger readout and a marker: the readout's ascent, in the middle, is the largest.
        var heights = UiBaseline.Heights([new(10, 3), new(12, 3.5f), new(9.5f, 2.75f)]);

        heights.ShouldBe([15f, 15.5f, 14.75f]);
    }

    [Fact]
    public void Heights_OfNoLabels_AreEmpty() =>
        UiBaseline.Heights([]).ShouldBeEmpty();

    [Fact]
    public void Rows_PlaceNoTextByAConstantVerticalOffset()
    {
        var violations = _rows.SelectMany(path => Row(path).Find(IsConstantVerticalPlacement)).ToList();

        violations.ShouldBeEmpty(
            "The layout decides alignment, never a nudge: docs/UI_DIRECTION.md → \"Components\".");
    }

    [Theory]
    [InlineData("void Make(Label label) { label.Position = new Vector2(4, 2); }", true)]
    [InlineData("void Make(Label label, bool below) { label.Position = new Vector2(4, below ? 8 : 0); }", true)]
    [InlineData("void Make(Label label) { label.CustomMinimumSize = label.CustomMinimumSize with { Y = 3 }; }", true)]
    [InlineData("void Make(Label label) { label.OffsetTop = 2; }", true)]
    [InlineData("void Make(MarginContainer margin) { margin.AddThemeConstantOverride(\"margin_top\", 2); }", true)]
    [InlineData("void Make(Label label) { label.Position = new Vector2(4, TrackY); }", false)]
    [InlineData("void Make(Label label) { label.CustomMinimumSize = label.CustomMinimumSize with { X = 40 }; }", false)]
    [InlineData("void Make(MarginContainer margin) { margin.AddThemeConstantOverride(\"margin_left\", 8); }", false)]
    public void Guard_flags_only_a_constant_vertical_placement(string member, bool flagged) =>
        CSharpSources.Snippet(member).Find(IsConstantVerticalPlacement).Count().ShouldBe(flagged ? 1 : 0);

    private static CSharpSources.Source Row(string path) => CSharpSources.Project.Single(source => source.Path == path);

    private static bool IsConstantVerticalPlacement(SyntaxNode node) => node switch
    {
        AssignmentExpressionSyntax assignment when _placements.Contains(Target(assignment.Left)) =>
            assignment.Right switch
            {
                BaseObjectCreationExpressionSyntax { ArgumentList.Arguments: [_, var y] } => HasLiteral(y.Expression),
                WithExpressionSyntax with => with.Initializer.Expressions
                    .OfType<AssignmentExpressionSyntax>()
                    .Any(member => member.Left.ToString() == "Y" && HasLiteral(member.Right)),
                var value => Target(assignment.Left) is "OffsetTop" or "OffsetBottom" && HasLiteral(value),
            },
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "AddThemeConstantOverride" }, ArgumentList.Arguments: [var name, var value] } =>
            name.Expression is LiteralExpressionSyntax literal
            && _verticalMargins.Contains(literal.Token.ValueText)
            && HasLiteral(value.Expression),
        _ => false,
    };

    private static string Target(ExpressionSyntax left) => left switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
        IdentifierNameSyntax name => name.Identifier.Text,
        _ => "",
    };

    private static bool HasLiteral(ExpressionSyntax expression) =>
        expression.DescendantNodesAndSelf().Any(node => node.IsKind(SyntaxKind.NumericLiteralExpression));
}
