using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// UI text crosses from App as <c>UiText</c> and is translated by Godot in one place (#682), and
/// text the player wrote is never translated.
/// </summary>
public sealed class UiTextTranslationTests
{
    private const string _translation = "ui/widgets/UiTextTranslation.cs";

    /// <summary>Labels that show a name the player gave, so a creation named "Train" stays "Train".</summary>
    [Theory]
    [InlineData("widgets/CreationCard.tscn", "Name")]
    [InlineData("screens/TrainingScreen.tscn", "CreationName")]
    public void Player_names_are_not_auto_translated(string scene, string node) =>
        SceneNodes.InScene(scene).Single(entry => entry.Name == node).Node.Body
            .ShouldContain("\nauto_translate_mode = 2", customMessage: "AutoTranslateMode.Disabled on the label itself.");

    /// <summary>
    /// Callout text is set in code, already translated or as the player wrote it (a part they named "Thigh"),
    /// so the layer's callouts must not translate it again.
    /// </summary>
    [Fact]
    public void Callout_layers_turn_auto_translation_off()
    {
        var source = CSharpSources.Project.Single(source => source.Path == "ui/lib/UiCalloutLayer.cs");
        var model = CSharpSources.ProjectCompilation.GetSemanticModel(source.Tree);
        var assigned = source.Tree.GetRoot().DescendantNodes().OfType<ConstructorDeclarationSyntax>().Single()
            .DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Select(assignment => (Property: CSharpSources.Symbol(model, assignment.Left)?.Name, Value: CSharpSources.Symbol(model, assignment.Right)?.ToDisplayString()));
        assigned.ShouldContain(("AutoTranslateMode", "Godot.Node.AutoTranslateModeEnum.Disabled"));

        // A mode saved on a layer in its scene would replace the constructor's.
        var layers = SceneNodes.WithScript("UiCalloutLayer.cs").ToList();
        layers.ShouldNotBeEmpty();
        layers.ShouldAllBe(layer => !layer.Body.Contains("\nauto_translate_mode"));
    }

    [Fact]
    public void Ui_text_is_shown_only_through_UiTextTranslation()
    {
        var compilation = CSharpSources.ProjectCompilation;
        var violations = CSharpSources.Project
            .Where(source => source.Path != _translation)
            .SelectMany(source =>
            {
                var model = compilation.GetSemanticModel(source.Tree);
                return source.Find(node => BypassesShowText(node, model));
            })
            .ToList();

        violations.ShouldBeEmpty(
            "Hand a UiText only to UiTextTranslation (ShowText, or Source for a component's …Source property), which translates it and keeps it for the next language change.");
    }

    /// <summary>
    /// Popup text is translated whole, so it is never put together in code (#773): "Delete {0}?" crosses as a
    /// UiText through a …Source property, since "Delete Walker?" has no translation. The design galleries
    /// show sample text for development and are left out.
    /// </summary>
    [Fact]
    public void Popup_text_is_not_built_in_code()
    {
        var compilation = CSharpSources.ProjectCompilation;
        var violations = CSharpSources.Project
            .Where(source => !source.Path.StartsWith("ui/screens/", StringComparison.Ordinal) || !source.Path.Contains("Gallery", StringComparison.Ordinal))
            .SelectMany(source =>
            {
                var model = compilation.GetSemanticModel(source.Tree);
                return source.Find(node => BuildsPopupText(node, model));
            })
            .ToList();

        violations.ShouldBeEmpty("Pass a UiText through UiTextTranslation.Source to the popup's …Source property instead.");
    }

    [Theory]
    [InlineData("object M(string name) => new UiNotificationSpec(UiPopupType.Default, \"Examples\", $\"Could not copy {name}.\");")]
    [InlineData("object M(string name) => new UiDialogSpec(UiPopupType.Danger, \"Delete \" + name + \"?\", \"Gone for good.\");")]
    [InlineData("object M(string name) => UiDialogResult.Failure(string.Format(\"Could not delete {0}.\", name));")]
    public void Building_popup_text_is_flagged(string member) =>
        PopupTextBuilds(member).ShouldHaveSingleItem();

    [Theory]
    [InlineData("object M() => new UiNotificationSpec(UiPopupType.Default, \"Examples\", \"Could not copy.\");")]
    [InlineData("object M(string title) => new UiDialogSpec(UiPopupType.Default, title, string.Empty);")]
    public void Whole_popup_text_passes(string member) =>
        PopupTextBuilds(member).ShouldBeEmpty();

    [Fact]
    public void UiTextTranslation_translates_with_TranslationServer()
    {
        var source = CSharpSources.Project.Single(source => source.Path == _translation);
        var model = CSharpSources.ProjectCompilation.GetSemanticModel(source.Tree);

        var calls = source.Tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Select(call => CSharpSources.Symbol(model, call))
            .Where(symbol => symbol?.ContainingType.ToDisplayString() == "Godot.TranslationServer")
            .Select(symbol => symbol!.Name)
            .ToHashSet();

        calls.ShouldBe(["Translate", "TranslatePlural", "FormatNumber", "GetLocale"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("string M(NodeRunner.App.ViewModels.UiText text) => text.Message;")]
    [InlineData("int M(NodeRunner.App.ViewModels.UiText text) => text.Args.Count;")]
    [InlineData("string M(NodeRunner.App.ViewModels.UiText text) => text.ToString();")]
    [InlineData("string M(NodeRunner.App.ViewModels.UiText text) => $\"{text}\";")]
    [InlineData("string M(NodeRunner.App.ViewModels.UiText text) => \"Best \" + text;")]
    [InlineData("object M(NodeRunner.App.ViewModels.UiText text) => text;")]
    [InlineData("void M(NodeRunner.App.ViewModels.UiText text) => System.Console.WriteLine(text);")]
    public void Bypassing_UiTextTranslation_is_flagged(string member) =>
        Bypasses(member).ShouldHaveSingleItem();

    [Theory]
    [InlineData("void M(UiLabel label, NodeRunner.App.ViewModels.UiText text) => NodeRunner.Ui.Widgets.UiTextTranslation.ShowText(label, text);")]
    [InlineData("void M(UiLabel label, NodeRunner.App.ViewModels.CreationCardTraining training) => NodeRunner.Ui.Widgets.UiTextTranslation.ShowText(label, training.GenerationsText);")]
    [InlineData("void M(UiLabel label, NodeRunner.App.ViewModels.UiText? text) => NodeRunner.Ui.Widgets.UiTextTranslation.ShowText(label, text ?? NodeRunner.App.ViewModels.UiText.Plain(\"Delete\"));")]
    [InlineData("void M(UiStageCard card, NodeRunner.App.ViewModels.UiText? text, bool show) => card.NoteSource = NodeRunner.Ui.Widgets.UiTextTranslation.Source(show ? text : null);")]
    [InlineData("string M() => nameof(NodeRunner.App.ViewModels.SignalFlowPresentationViewModel.DistanceNote);")]
    [InlineData("bool M(NodeRunner.App.ViewModels.BuildViewModel build) => build.CanPlacePart(NodeRunner.App.ViewModels.BuildPart.Camera, default, out _);")]
    public void Showing_ui_text_passes(string member) =>
        Bypasses(member).ShouldBeEmpty();

    private static IEnumerable<string> Bypasses(string member)
    {
        var snippet = CSharpSources.Snippet(member);
        var model = CSharpSources.Compile([.. CSharpSources.Project, snippet]).GetSemanticModel(snippet.Tree);
        return snippet.Find(node => BypassesShowText(node, model));
    }

    // The outermost expression of type UiText must be an argument to a UiTextTranslation method; type names, a
    // nameof, a discard, and the inner parts of a larger UiText expression (a member access, ?? or ?:) are not counted.
    private static bool BypassesShowText(SyntaxNode node, SemanticModel model)
    {
        if (node is not ExpressionSyntax expression
            || !IsUiText(model.GetTypeInfo(expression).Type)
            || CSharpSources.Symbol(model, expression) is ITypeSymbol
            || IsInNameof(expression, model)
            || model.GetSymbolInfo(expression).Symbol is IDiscardSymbol
            || expression.Parent is ExpressionSyntax parent && IsUiText(model.GetTypeInfo(parent).Type))
        {
            return false;
        }

        return !(expression.Parent is ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax call }
            && CSharpSources.Symbol(model, call) is IMethodSymbol method
            && (method.ReducedFrom ?? method).ContainingType.ToDisplayString() == "NodeRunner.Ui.Widgets.UiTextTranslation");
    }

    private static IEnumerable<string> PopupTextBuilds(string member)
    {
        var snippet = CSharpSources.Snippet(member);
        var model = CSharpSources.Compile([.. CSharpSources.Project, snippet]).GetSemanticModel(snippet.Tree);
        return snippet.Find(node => BuildsPopupText(node, model));
    }

    private static readonly string[] _popupTypes =
        ["NodeRunner.Ui.Lib.UiDialogSpec", "NodeRunner.Ui.Lib.UiNotificationSpec", "NodeRunner.Ui.Lib.UiDialogResult"];

    private static bool BuildsPopupText(SyntaxNode node, SemanticModel model) =>
        node is ArgumentSyntax { Parent.Parent: ExpressionSyntax call } argument
        && CSharpSources.Symbol(model, call) is IMethodSymbol method
        && _popupTypes.Contains(method.ContainingType.ToDisplayString())
        && IsBuiltText(argument.Expression, model);

    private static bool IsBuiltText(ExpressionSyntax expression, SemanticModel model) =>
        expression is InterpolatedStringExpressionSyntax
        || expression is BinaryExpressionSyntax { RawKind: (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.AddExpression }
            && model.GetTypeInfo(expression).Type?.SpecialType == SpecialType.System_String
        || expression is InvocationExpressionSyntax call
            && CSharpSources.Symbol(model, call) is IMethodSymbol { Name: "Format" or "Concat" or "Join" } method
            && method.ContainingType.SpecialType == SpecialType.System_String;

    private static bool IsInNameof(ExpressionSyntax expression, SemanticModel model) =>
        expression.Ancestors().OfType<InvocationExpressionSyntax>().Any(call =>
            call.Expression is IdentifierNameSyntax { Identifier.Text: "nameof" } && model.GetSymbolInfo(call).Symbol is null);

    private static bool IsUiText(ITypeSymbol? type) =>
        type is { Name: "UiText" } && type.ContainingNamespace.ToDisplayString() == "NodeRunner.App.ViewModels";
}
