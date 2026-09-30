using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// One Android Back press sends several go-back signals (#506; see UiBackPressTracker), so every
/// place that acts on Back must take the press through <c>UiBackPress.TryTake</c>; otherwise a
/// repeat acts a second time. References are bound with Roslyn, so any spelling of
/// <c>NotificationWMGoBackRequest</c> or <c>Window.GoBackRequested</c> counts, and each listener is
/// checked on its own. UiBackPress itself is the gate, so it is the one file left out.
/// </summary>
public sealed class UiBackPressGuardTests
{
    [Fact]
    public void Every_back_listener_takes_the_press_once()
    {
        var compilation = CSharpSources.ProjectCompilation;
        var listeners = CSharpSources.Project
            .Where(source => !source.Path.EndsWith("/UiBackPress.cs", StringComparison.Ordinal))
            .SelectMany(source => BackListeners(source, compilation.GetSemanticModel(source.Tree)))
            .ToList();

        listeners.ShouldNotBeEmpty();
        listeners.Where(listener => !listener.TakesPress).Select(listener => listener.Where).ShouldBeEmpty(
            "Act on Android Back only after UiBackPress.TryTake(this) returns true.");
    }

    [Theory]
    [InlineData("void N(long what) { switch (what) { case NotificationWMGoBackRequest: Hide(); break; } }")]
    [InlineData("void N(long what) { if (Node.NotificationWMGoBackRequest == what) { Hide(); } if (UiBackPress.TryTake(this)) { Show(); } }")]
    [InlineData("Window _w; void A() { _w.GoBackRequested += OnBack; } void OnBack() { Hide(); } void B() { UiBackPress.TryTake(this); }")]
    [InlineData("Window _w; void A() { _w.Connect(Window.SignalName.GoBackRequested, Callable.From(Hide)); }")]
    public void Guard_reports_a_listener_that_does_not_take_the_press(string member) =>
        Snippet(member).ShouldHaveSingleItem().TakesPress.ShouldBeFalse();

    [Theory]
    [InlineData("void N(long what) { switch (what) { case NotificationWMGoBackRequest: if (UiBackPress.TryTake(this)) { Hide(); } break; } }")]
    [InlineData("void N(long what) { if (what == NotificationWMGoBackRequest && UiBackPress.TryTake(this)) { Hide(); } }")]
    [InlineData("Window _w; void A() { _w.GoBackRequested += OnBack; _w.GoBackRequested -= OnBack; } void OnBack() { if (UiBackPress.TryTake(this)) { Hide(); } }")]
    [InlineData("Window _w; void A() { _w.GoBackRequested += () => { if (UiBackPress.TryTake(this)) { Hide(); } }; }")]
    public void Guard_accepts_a_listener_that_takes_the_press(string member) =>
        Snippet(member).ShouldHaveSingleItem().TakesPress.ShouldBeTrue();

    private static List<Listener> Snippet(string member)
    {
        var snippet = CSharpSources.Snippet(member);
        var model = CSharpSources.Compile([.. CSharpSources.Project, snippet]).GetSemanticModel(snippet.Tree);
        return [.. BackListeners(snippet, model)];
    }

    private static IEnumerable<Listener> BackListeners(CSharpSources.Source source, SemanticModel model)
    {
        foreach (var name in source.Tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var symbol = CSharpSources.Symbol(model, name);
            if (symbol is IFieldSymbol { Name: "NotificationWMGoBackRequest" } field && IsGodot(field.ContainingType, "Node"))
            {
                yield return new Listener(source.Describe(name), Takes(EnclosingBranch(name)));
            }
            else if (symbol is { Name: "GoBackRequested" } && IsWindowMember(symbol) && SubscriptionOf(name) is var subscription)
            {
                if (subscription?.IsKind(SyntaxKind.SubtractAssignmentExpression) == true)
                {
                    continue;
                }

                var handler = subscription is null ? EnclosingBranch(name) : HandlerBody(subscription.Right, model);
                yield return new Listener(source.Describe(name), handler is not null && Takes(handler));
            }
        }
    }

    // The if, case or statement that acts on the notification.
    private static SyntaxNode EnclosingBranch(SyntaxNode node) =>
        node.Ancestors().FirstOrDefault(ancestor => ancestor is IfStatementSyntax or SwitchSectionSyntax or SwitchExpressionArmSyntax)
        ?? node.Ancestors().OfType<StatementSyntax>().First();

    private static AssignmentExpressionSyntax? SubscriptionOf(SyntaxNode name) =>
        name.Ancestors().OfType<AssignmentExpressionSyntax>().FirstOrDefault(assignment => assignment.Left.DescendantNodesAndSelf().Contains(name));

    private static SyntaxNode? HandlerBody(ExpressionSyntax handler, SemanticModel model) => handler is AnonymousFunctionExpressionSyntax lambda
        ? lambda
        : CSharpSources.Symbol(model, handler)?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();

    private static bool Takes(SyntaxNode scope) => scope.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any(invocation =>
        invocation.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.Text: "UiBackPress" },
            Name.Identifier.Text: "TryTake",
        });

    private static bool IsWindowMember(ISymbol symbol)
    {
        for (var type = symbol.ContainingType; type is not null; type = type.ContainingType)
        {
            if (IsGodot(type, "Window"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGodot(ITypeSymbol? type, string name) =>
        type is { Name: var typeName, ContainingNamespace.Name: "Godot" } && typeName == name;

    private sealed record Listener(string Where, bool TakesPress);
}
