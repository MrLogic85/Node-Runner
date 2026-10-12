using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NodeRunner.Ui.Tests;

/// <summary>No creature body sets continuous collision detection (#1171).</summary>
public sealed class CreatureCcdGuardTests
{
    [Fact]
    public void CreatureBodies_LeaveCcdOff_SoFastRollersCannotLaunch()
    {
        var violations = CSharpSources.Project
            .Where(source => source.Path.StartsWith("creature/", StringComparison.Ordinal))
            .SelectMany(source => source.Find(SetsCcd))
            .ToList();

        violations.ShouldBeEmpty(
            "Leave ContinuousCd unset on creature bodies (#1171; why: project/src/creature/AGENTS.md rule 3).");
    }

    [Theory]
    [InlineData("void Make() { var body = new RigidBody2D { ContinuousCd = RigidBody2D.CcdMode.CastRay }; }")]
    [InlineData("void Make(RigidBody2D body) { body.ContinuousCd = RigidBody2D.CcdMode.CastShape; }")]
    public void Guard_FlagsAnyCcdSetting(string member) =>
        CSharpSources.Snippet(member).Find(SetsCcd).ShouldNotBeEmpty();

    [Fact]
    public void Guard_AllowsABodyWithoutCcd() =>
        CSharpSources.Snippet("void Make() { var body = new RigidBody2D { CanSleep = false }; }").Find(SetsCcd).ShouldBeEmpty();

    private static bool SetsCcd(SyntaxNode node) =>
        node is AssignmentExpressionSyntax assignment
        && assignment.Left switch
        {
            IdentifierNameSyntax name => name.Identifier.Text == "ContinuousCd",
            MemberAccessExpressionSyntax access => access.Name.Identifier.Text == "ContinuousCd",
            _ => false,
        };
}
