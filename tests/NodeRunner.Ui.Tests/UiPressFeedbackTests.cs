using Godot;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiPressFeedbackTests
{
    [Theory]
    [InlineData(BaseButton.DrawMode.HoverPressed, false, true, true)]
    [InlineData(BaseButton.DrawMode.Hover, false, false, false)]
    [InlineData(BaseButton.DrawMode.Disabled, false, false, false)]
    [InlineData(BaseButton.DrawMode.Hover, true, false, true)]
    [InlineData(BaseButton.DrawMode.Pressed, true, true, false)]
    public void Only_a_press_shows_the_tint_and_a_held_toggle_draws_the_state_it_would_switch_to(
        BaseButton.DrawMode mode, bool toggledOn, bool shows, bool held)
    {
        UiPressFeedback.Shows(mode).ShouldBe(shows);
        UiPressFeedback.IsHeld(mode, toggledOn).ShouldBe(held);
    }

    [Theory]
    [InlineData(UiTokens.Color.Accent, false, UiTokens.Color.OnAccent)]
    [InlineData(UiTokens.Color.Panel, false, UiTokens.Color.Accent)]
    [InlineData(UiTokens.Color.Transparent, true, UiTokens.Color.Danger)]
    public void The_tint_shows_on_every_fill_and_keeps_danger_red(UiTokens.Color fill, bool danger, UiTokens.Color tint) =>
        UiPressFeedback.TintColorOver(fill, danger).ShouldBe(tint);

    [Fact]
    public void No_control_draws_a_hover_look()
    {
        var violations = CSharpSources.Project
            .SelectMany(source => source.Find(node =>
                node is MemberAccessExpressionSyntax { Name.Identifier.Text: "Hover" } access
                && access.Expression.ToString().EndsWith("DrawMode", StringComparison.Ordinal)))
            .ToList();

        violations.ShouldBeEmpty();
    }

    // Hover has no look of its own: a hover stylebox repeats the normal one, and the
    // pressed one is repeated for hover_pressed, so Godot's default theme never shows through.
    [Fact]
    public void Hover_styleboxes_repeat_their_plain_state()
    {
        var violations = new List<string>();
        foreach (var (source, (hoverState, plainState)) in CSharpSources.Project.SelectMany(source =>
                     new[] { ("hover", "normal"), ("hover_pressed", "pressed") }.Select(pair => (source, pair))))
        {
            var overrides = StyleboxOverrides(source.Tree).ToList();
            foreach (var (call, receiver, state, style) in overrides.Where(o => o.State == hoverState))
            {
                if (!overrides.Any(o => o.Receiver == receiver && o.State == plainState && o.Style == style))
                {
                    violations.Add(source.Describe(call));
                }
            }
            // Only buttons have a pressed state; labels and fields have a normal one but no hover.
            foreach (var (call, receiver, _, _) in overrides.Where(o => o.State == plainState && plainState == "pressed"))
            {
                if (!overrides.Any(o => o.Receiver == receiver && o.State == hoverState))
                {
                    violations.Add(source.Describe(call));
                }
            }
            foreach (var states in StateLists(source.Tree))
            {
                if (states.Contains(plainState) && !states.Contains(hoverState))
                {
                    violations.Add($"{source.Path}: [{string.Join(", ", states)}]");
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    private static IEnumerable<(InvocationExpressionSyntax Call, string Receiver, string State, string Style)> StyleboxOverrides(SyntaxTree tree) =>
        tree.GetRoot().DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(call => MethodName(call) == "AddThemeStyleboxOverride"
                && call.ArgumentList.Arguments.Count == 2
                && call.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax)
            .Select(call => (
                call,
                call.Expression is MemberAccessExpressionSyntax access ? access.Expression.ToString() : "this",
                ((LiteralExpressionSyntax)call.ArgumentList.Arguments[0].Expression).Token.ValueText,
                call.ArgumentList.Arguments[1].Expression.ToString()));

    // Arrays of state names a loop gives one stylebox, like UiMenuActionItem's empty states.
    private static IEnumerable<string[]> StateLists(SyntaxTree tree) =>
        tree.GetRoot().DescendantNodes()
            .OfType<InitializerExpressionSyntax>()
            .Select(list => list.Expressions
                .OfType<LiteralExpressionSyntax>()
                .Where(literal => literal.IsKind(SyntaxKind.StringLiteralExpression))
                .Select(literal => literal.Token.ValueText)
                .ToArray())
            .Where(states => states.Contains("normal") && states.Contains("disabled"));

    private static string? MethodName(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
        IdentifierNameSyntax name => name.Identifier.Text,
        _ => null,
    };
}
