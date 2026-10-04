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
    [InlineData("[Export] public string Help { get; set; } = \"\"; private UiChoiceRow _row = new(); void M() => _row.Subtext = Help;")]
    [InlineData("[Export] public string Help { get; set; } = \"\"; private UiIconCaption _content = UiIconCaption.Ensure(new Control()); void M() => _content.Apply(Help, UiIconId.None, UiIconSize.Small, UiTokens.Color.Ink, Colors.White);")]
    [InlineData("[Export] public string Help { get; set; } = \"\"; private Label _label = new(); static void Show(Label label, string text) => label.Text = text; void M() => Show(_label, Help);")]
    public void Inner_text_without_context_is_flagged(string member) =>
        UnsharedIn(member).ShouldHaveSingleItem();

    [Theory]
    [InlineData("[Export] public string Title { get; set; } = \"\"; private Label _label = new(); void M() { UiTranslation.ShareContext(this, _label); _label.Text = Title; }")]
    [InlineData("[Export] public string Title { get; set; } = \"\"; void M(Label label) { UiTranslation.ShareContext(this, label); label.Text = Title; }")]
    [InlineData("private Label _label = new(); void M(string text) => _label.Text = text;")]
    [InlineData("void M() => Text = TooltipText;")]
    [InlineData("[Export] public string Help { get; set; } = \"\"; static Label Make(string text) => new() { Text = text }; void M() { var label = Make(Help); UiTranslation.ShareContext(this, label); }")]
    [InlineData("[Export] public string Help { get; set; } = \"\"; void M() { var button = new Button { TooltipText = Help }; UiTranslation.ShareContext(this, button); }")]
    [InlineData("[Export] public string Help { get; set; } = \"\"; private UiIconCaption _content = UiIconCaption.Ensure(new Control()); void M() { UiTranslation.ShareContext(this, _content.Label); _content.Apply(Help, UiIconId.None, UiIconSize.Small, UiTokens.Color.Ink, Colors.White); }")]
    [InlineData("[Export] public string Help { get; set; } = \"\"; void M() => SetMeta(\"help\", string.IsNullOrEmpty(Help));")]
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
                var control = model.Compilation.GetTypeByMetadataName("Godot.Control");
                // A shared control also covers the helper that holds it, such as _content for _content.Label.
                var shared = type.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Where(call => CSharpSources.Symbol(model, call) is IMethodSymbol { Name: "ShareContext", ContainingType.Name: "UiTranslation" }
                        && call.ArgumentList.Arguments.Count == 2)
                    .SelectMany(call => call.ArgumentList.Arguments[1].Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                    .Select(name => CSharpSources.Symbol(model, name))
                    .ToHashSet(SymbolEqualityComparer.Default);
                var assigned = type.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                    .Where(assignment => InnerText(assignment, control, model) is { Found: true } inner
                        && IsOwnText(assignment.Right, owner, model, depth: 0)
                        && !shared.Contains(inner.Target))
                    .Select(source.Describe);
                var passed = type.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Where(call => call.ArgumentList.Arguments.Any(argument => model.GetTypeInfo(argument.Expression).Type is { } argumentType
                            && IsText(argumentType) && IsOwnText(argument.Expression, owner, model, depth: 0))
                        && Receiver(call, control, model) is { Found: true } inner
                        && !shared.Contains(inner.Target))
                    .Select(source.Describe);
                return assigned.Concat(passed);
            });

    // The variable a created control lands in, so a ShareContext call can name it.
    private static ISymbol? Holder(ExpressionSyntax created, SemanticModel model) => created.Parent switch
    {
        EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } => model.GetDeclaredSymbol(declarator),
        AssignmentExpressionSyntax assignment => CSharpSources.Symbol(model, assignment.Left),
        _ => null,
    };

    // The inner control whose text property the assignment sets, also inside its object initializer.
    private static (bool Found, ISymbol? Target) InnerText(AssignmentExpressionSyntax assignment, INamedTypeSymbol? control, SemanticModel model) =>
        CSharpSources.Symbol(model, assignment.Left) is IPropertySymbol property && IsText(property.Type) && CSharpSources.DerivesFrom(property.ContainingType, control)
            ? assignment.Left switch
            {
                MemberAccessExpressionSyntax { Expression: not ThisExpressionSyntax } target => (true, CSharpSources.Symbol(model, target.Expression)),
                IdentifierNameSyntax when assignment.Parent is InitializerExpressionSyntax { Parent: BaseObjectCreationExpressionSyntax creation } =>
                    (true, Holder(creation, model)),
                _ => (false, null),
            }
            : (false, null);

    // Where a call that receives the component's text puts it: a control it makes, the inner control
    // or library helper it is called on, or a control passed next to the text.
    private static (bool Found, ISymbol? Target) Receiver(InvocationExpressionSyntax call, INamedTypeSymbol? control, SemanticModel model)
    {
        if (CSharpSources.Symbol(model, call) is not IMethodSymbol method)
        {
            return (false, null);
        }

        if (CSharpSources.DerivesFrom(method.ReturnType, control))
        {
            return (true, Holder(call, model));
        }

        if (!method.IsStatic && call.Expression is MemberAccessExpressionSyntax { Expression: not ThisExpressionSyntax and not BaseExpressionSyntax } access
            && model.GetTypeInfo(access.Expression).Type is { } receiver
            && (CSharpSources.DerivesFrom(receiver, control) || receiver.ContainingNamespace?.ToDisplayString() == "NodeRunner.Ui.Lib"))
        {
            return (true, CSharpSources.Symbol(model, access.Expression));
        }

        return call.ArgumentList.Arguments
            .Select(argument => argument.Expression)
            .Where(argument => argument is not ThisExpressionSyntax && CSharpSources.DerivesFrom(model.GetTypeInfo(argument).Type, control))
            .Select(argument => (true, CSharpSources.Symbol(model, argument)))
            .FirstOrDefault();
    }

    // Text the component itself holds, read directly or through a local it was put in.
    private static bool IsOwnText(SyntaxNode expression, INamedTypeSymbol? owner, SemanticModel model, int depth) =>
        depth < 4 && expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
            .Where(OnThis)
            .Any(name => CSharpSources.Symbol(model, name) switch
            {
                IPropertySymbol { IsStatic: false } property => IsText(property.Type) && CSharpSources.DerivesFrom(owner, property.ContainingType),
                IFieldSymbol { IsStatic: false, IsConst: false } field => IsText(field.Type) && SymbolEqualityComparer.Default.Equals(field.ContainingType, owner),
                ILocalSymbol local when IsText(local.Type) => local.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).Any(declaration => declaration switch
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
