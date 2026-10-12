using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NodeRunner.Ui.Tests;

/// <summary>A ui/lib component sets its own mouse filter through <c>UiMouseFilter.Prefer</c>, never by assigning it (#1164).</summary>
public sealed class UiMouseFilterGuardTests
{
    private const string _helper = "ui/lib/UiMouseFilter.cs";

    [Fact]
    public void Components_PreferTheirMouseFilter_SoAScenesValueHolds()
    {
        var violations = CSharpSources.Project
            .Where(source => source.Path.StartsWith("ui/lib/", StringComparison.Ordinal) && source.Path != _helper)
            .SelectMany(source => source.Find(AssignsOwnMouseFilter))
            .ToList();

        violations.ShouldBeEmpty(
            "Set a component's own mouse filter with UiMouseFilter.Prefer(this, ...), so a scene's mouse_filter still holds (#1164).");
    }

    [Theory]
    [InlineData("Snippet() => MouseFilter = MouseFilterEnum.Pass;")]
    [InlineData("public override void _Ready() { this.MouseFilter = MouseFilterEnum.Ignore; }")]
    public void Guard_FlagsAnOwnAssignment(string member) =>
        CSharpSources.Snippet(member).Find(AssignsOwnMouseFilter).ShouldNotBeEmpty();

    [Theory]
    [InlineData("public override void _Ready() { UiMouseFilter.Prefer(this, MouseFilterEnum.Pass); }")]
    [InlineData("void Make() { var label = new Label { MouseFilter = MouseFilterEnum.Ignore }; }")]
    [InlineData("void Make(Control child) { child.MouseFilter = MouseFilterEnum.Ignore; }")]
    public void Guard_AllowsOtherControlsAndPrefer(string member) =>
        CSharpSources.Snippet(member).Find(AssignsOwnMouseFilter).ShouldBeEmpty();

    private static bool AssignsOwnMouseFilter(SyntaxNode node) =>
        node is AssignmentExpressionSyntax assignment
        && assignment.Parent is not InitializerExpressionSyntax
        && assignment.Left switch
        {
            IdentifierNameSyntax name => name.Identifier.Text == "MouseFilter",
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access => access.Name.Identifier.Text == "MouseFilter",
            _ => false,
        };
}
