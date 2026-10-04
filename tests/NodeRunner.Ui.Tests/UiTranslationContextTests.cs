using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Godot does not pass a Control's <c>translation_context</c> on to its children, so a library
/// component that shows its own text through an inner control hands its context on (#777).
/// </summary>
public sealed class UiTranslationContextTests
{
    private const string _library = "ui/lib/";
    private static readonly HashSet<string> _textProperties = ["Text", "TooltipText", "PlaceholderText"];

    [Fact]
    public void Inner_text_shares_the_component_context()
    {
        var compilation = CSharpSources.ProjectCompilation;
        var violations = CSharpSources.Project
            .Where(source => source.Path.StartsWith(_library, StringComparison.Ordinal))
            .SelectMany(source => Unshared(source, compilation.GetSemanticModel(source.Tree)))
            .ToList();

        violations.ShouldBeEmpty(
            "Call UiTranslation.ShareContext(this, inner) for an inner control that shows the component's own text.");
    }

    [Theory]
    [InlineData("[Export] public string Title { get; set; } = \"\"; private Label _label = new(); void M() => _label.Text = Title;")]
    [InlineData("[Export] public string Title { get; set; } = \"\"; void M(Label label) => label.Text = $\"{Title}!\";")]
    [InlineData("[Export] public string Help { get; set; } = \"\"; private Button _button = new(); void M() => _button.TooltipText = Help;")]
    [InlineData("private Label _label = new(); void M() => _label.Text = TooltipText;")]
    [InlineData("[Export] public string Note { get; set; } = \"\"; private Label _label = new(); void M() { var note = Note; _label.Text = note; }")]
    [InlineData("[Export] public string[] Steps { get; set; } = []; void M(Label label) { foreach (var step in Steps) label.Text = step; }")]
    [InlineData("[Export] public string Help { get; set; } = \"\"; void M() { var button = new Button { TooltipText = Help }; AddChild(button); }")]
    [InlineData("[Export] public string Help { get; set; } = \"\"; static Label Make(string text) => new() { Text = text }; void M() => AddChild(Make(Help));")]
    public void Inner_text_without_context_is_flagged(string member) =>
        UnsharedIn(member).ShouldHaveSingleItem();

    [Theory]
    [InlineData("[Export] public string Title { get; set; } = \"\"; private Label _label = new(); void M() { UiTranslation.ShareContext(this, _label); _label.Text = Title; }")]
    [InlineData("[Export] public string Title { get; set; } = \"\"; void M(Label label) { UiTranslation.ShareContext(this, label); label.Text = Title; }")]
    [InlineData("private Label _label = new(); void M(string text) => _label.Text = text;")]
    [InlineData("void M() => Text = TooltipText;")]
    [InlineData("[Export] public string Help { get; set; } = \"\"; static Label Make(string text) => new() { Text = text }; void M() { var label = Make(Help); UiTranslation.ShareContext(this, label); }")]
    [InlineData("[Export] public string Help { get; set; } = \"\"; void M() { var button = new Button { TooltipText = Help }; UiTranslation.ShareContext(this, button); }")]
    public void Inner_text_with_context_passes(string member) =>
        UnsharedIn(member).ShouldBeEmpty();

    private static IEnumerable<string> UnsharedIn(string member)
    {
        var snippet = CSharpSources.Snippet(member);
        var model = CSharpSources.Compile([.. CSharpSources.Project, snippet]).GetSemanticModel(snippet.Tree);
        return Unshared(snippet, model);
    }

    private static IEnumerable<string> Unshared(CSharpSources.Source source, SemanticModel model) =>
        source.Tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
            .SelectMany(type =>
            {
                var owner = model.GetDeclaredSymbol(type) as INamedTypeSymbol;
                var shared = type.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Where(call => CSharpSources.Symbol(model, call) is IMethodSymbol { Name: "ShareContext", ContainingType.Name: "UiTranslation" }
                        && call.ArgumentList.Arguments.Count == 2)
                    .Select(call => CSharpSources.Symbol(model, call.ArgumentList.Arguments[1].Expression))
                    .ToHashSet(SymbolEqualityComparer.Default);
                var assigned = type.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                    .Where(assignment => InnerText(assignment, model) is { Found: true } inner
                        && IsOwnText(assignment.Right, owner, model, depth: 0)
                        && !shared.Contains(inner.Target))
                    .Select(source.Describe);
                var built = type.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Where(call => CSharpSources.Symbol(model, call) is IMethodSymbol { ReturnType: INamedTypeSymbol made }
                        && CSharpSources.DerivesFrom(made, model.Compilation.GetTypeByMetadataName("Godot.Control"))
                        && call.ArgumentList.Arguments.Any(argument => IsOwnText(argument.Expression, owner, model, depth: 0))
                        && !shared.Contains(Holder(call, model)))
                    .Select(source.Describe);
                return assigned.Concat(built);
            });

    // The variable a created control lands in, so a ShareContext call can name it.
    private static ISymbol? Holder(ExpressionSyntax created, SemanticModel model) => created.Parent switch
    {
        EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } => model.GetDeclaredSymbol(declarator),
        AssignmentExpressionSyntax assignment => CSharpSources.Symbol(model, assignment.Left),
        _ => null,
    };

    // The inner control whose text property the assignment sets, also inside its object initializer.
    private static (bool Found, ISymbol? Target) InnerText(AssignmentExpressionSyntax assignment, SemanticModel model)
    {
        switch (assignment.Left)
        {
            case MemberAccessExpressionSyntax { Expression: not ThisExpressionSyntax } target
                when _textProperties.Contains(target.Name.Identifier.Text) && CSharpSources.Symbol(model, target) is IPropertySymbol:
                return (true, CSharpSources.Symbol(model, target.Expression));
            case IdentifierNameSyntax name
                when _textProperties.Contains(name.Identifier.Text)
                    && assignment.Parent is InitializerExpressionSyntax { Parent: BaseObjectCreationExpressionSyntax creation }:
                return (true, Holder(creation, model));
            default:
                return (false, null);
        }
    }

    // Text the component itself holds, read directly or through a local it was put in.
    private static bool IsOwnText(SyntaxNode expression, INamedTypeSymbol? owner, SemanticModel model, int depth) =>
        depth < 4 && expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
            .Where(OnThis)
            .Any(name => CSharpSources.Symbol(model, name) switch
            {
                IPropertySymbol { IsStatic: false } property => IsText(property.Type) && CSharpSources.DerivesFrom(owner, property.ContainingType),
                IFieldSymbol { IsStatic: false, IsConst: false } field => IsText(field.Type) && SymbolEqualityComparer.Default.Equals(field.ContainingType, owner),
                ILocalSymbol local => local.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).Any(declaration => declaration switch
                {
                    VariableDeclaratorSyntax { Initializer.Value: var value } => IsOwnText(value, owner, model, depth + 1),
                    ForEachStatementSyntax loop => IsOwnText(loop.Expression, owner, model, depth + 1),
                    _ => false,
                }),
                _ => false,
            });

    private static bool OnThis(IdentifierNameSyntax name) =>
        name.Parent is not MemberAccessExpressionSyntax access || access.Name != name || access.Expression is ThisExpressionSyntax;

    private static bool IsText(ITypeSymbol type) =>
        type.SpecialType == SpecialType.System_String
        || type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String }
        || type is INamedTypeSymbol { Name: "IEnumerable" or "IReadOnlyList", TypeArguments: [{ SpecialType: SpecialType.System_String }] };
}
