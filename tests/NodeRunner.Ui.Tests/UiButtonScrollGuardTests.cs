using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// A Godot button made in code keeps a drag from its ScrollContainer unless it passes input on
/// (#1001; <c>UiNativeScroll</c>): every one sets <c>MouseFilter = Pass</c>, in its initializer or
/// on the variable it is stored in.
/// </summary>
public sealed class UiButtonScrollGuardTests
{
    [Fact]
    public void EveryGodotButtonMadeInCode_PassesDragsToItsScrollContainer()
    {
        var compilation = CSharpSources.ProjectCompilation;
        var baseButton = compilation.GetTypeByMetadataName("Godot.BaseButton");
        baseButton.ShouldNotBeNull();

        var made = CSharpSources.Project
            .SelectMany(source =>
            {
                var model = compilation.GetSemanticModel(source.Tree);
                return source.Tree.GetRoot().DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>()
                    .Where(creation => model.GetTypeInfo(creation).Type is { } type
                        && type.ContainingNamespace.ToDisplayString() == "Godot"
                        && CSharpSources.DerivesFrom(type, baseButton))
                    .Select(creation => (source.Path, Creation: creation));
            })
            .ToList();

        made.ShouldNotBeEmpty();
        made.Where(entry => !PassesInput(entry.Creation))
            .Select(entry => $"{entry.Path}:{entry.Creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1}")
            .ShouldBeEmpty("Buttons made in code set MouseFilter = Pass so the panel scrolls under them (#1001).");
    }

    private static bool PassesInput(BaseObjectCreationExpressionSyntax creation)
    {
        if (creation.Initializer?.Expressions.OfType<AssignmentExpressionSyntax>().Any(IsPass) == true)
        {
            return true;
        }

        var stored = creation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault()?.Identifier.Text
            ?? (creation.Ancestors().OfType<AssignmentExpressionSyntax>().FirstOrDefault()?.Left as IdentifierNameSyntax)?.Identifier.Text;
        var member = creation.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault();
        return stored is not null && member is not null && member.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Any(assignment => IsPass(assignment)
                && assignment.Left is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax target }
                && target.Identifier.Text == stored);
    }

    private static bool IsPass(AssignmentExpressionSyntax assignment) =>
        assignment.Left.ToString().EndsWith("MouseFilter", StringComparison.Ordinal)
        && assignment.Right.ToString().EndsWith(".Pass", StringComparison.Ordinal);
}
