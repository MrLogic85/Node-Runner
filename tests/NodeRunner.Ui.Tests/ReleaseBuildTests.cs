using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NodeRunner.Ui.Tests;

/// <summary>Debug-only menu entries stay hidden in release exports (#808).</summary>
public sealed class ReleaseBuildTests
{
    [Fact]
    public void Only_debug_builds_show_the_component_library_link()
    {
        var assignments = CSharpSources.Project
            .SelectMany(source => source.Tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>()
                .Where(assignment => assignment.Left is MemberAccessExpressionSyntax { Name.Identifier.Text: "ShowComponentLibraryLink" })
                .Select(assignment => (source, assignment)))
            .ToList();

        assignments.ShouldNotBeEmpty();
        assignments
            .Where(found => !Conjuncts(found.assignment.Right).Any(IsDebugBuildCheck))
            .Select(found => found.source.Describe(found.assignment))
            .ShouldBeEmpty("Gate the Component library link on OS.IsDebugBuild() so release exports hide it.");
    }

    private static IEnumerable<ExpressionSyntax> Conjuncts(ExpressionSyntax expression) =>
        expression switch
        {
            ParenthesizedExpressionSyntax parenthesized => Conjuncts(parenthesized.Expression),
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression) =>
                Conjuncts(binary.Left).Concat(Conjuncts(binary.Right)),
            _ => [expression],
        };

    private static bool IsDebugBuildCheck(ExpressionSyntax expression) =>
        expression is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.Text: "OS" },
                Name.Identifier.Text: "IsDebugBuild",
            },
            ArgumentList.Arguments.Count: 0,
        };
}
