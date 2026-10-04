using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Guards #733: a stroke drawn with a <c>CanvasItem</c> primitive is antialiased in window pixels,
/// so its feather is one device pixel, or is a one-pixel hairline (width -1). Window pixels means
/// in a method that opens a <c>UiPixelPen</c>, calls <c>UiPixelSpace.Enter</c>, or takes its
/// <c>Transform2D toPixels</c>; a helper that takes <c>toPixels</c> is called from such a method.
/// </summary>
public sealed class UiStrokeGuardTests
{
    private static readonly string[] _strokes =
        ["DrawLine", "DrawPolyline", "DrawPolylineColors", "DrawArc", "DrawDashedLine", "DrawCircle", "DrawMultiline", "DrawMultilineColors"];

    /// <summary>The debug bounds overlay outlines each control in a hard rectangle on purpose.</summary>
    private const string _debugOverlay = "ui/lib/UiBoundsDebugOverlay.cs";

    /// <summary>The pen itself, which draws through its own window-pixel map.</summary>
    private const string _pen = "ui/lib/UiPixelPen.cs";

    [Fact]
    public void Strokes_are_antialiased_or_hairlines()
    {
        var compilation = CSharpSources.ProjectCompilation;
        var violations = CSharpSources.Project
            .Where(source => source.Path is not (_debugOverlay or _pen))
            .SelectMany(source =>
            {
                var model = compilation.GetSemanticModel(source.Tree);
                return source.Find(node => IsHardStroke(node, model));
            })
            .ToList();

        violations.ShouldBeEmpty("Draw strokes through UiPixelPen (#733), or as a -1 hairline.");
    }

    [Theory]
    [InlineData("void _Draw() { DrawLine(Vector2.Zero, Vector2.One, Colors.White, 2); }")]
    [InlineData("void _Draw() { DrawArc(Vector2.Zero, 4, 0, 1, 8, Colors.White, 1, antialiased: false); }")]
    [InlineData("void _Draw() { DrawCircle(Vector2.Zero, 4, Colors.White); }")]
    [InlineData("void _Draw() { DrawRect(new Rect2(), Colors.White, filled: false, width: 1); }")]
    [InlineData("void _Draw() { DrawSetTransform(Vector2.Zero, 0, Vector2.One * 3); DrawLine(Vector2.Zero, Vector2.One, Colors.White, 2, antialiased: true); }")]
    [InlineData("void _Draw() { DrawArc(Vector2.Zero, 4, -1, 1, 8, Colors.White, 2); }")]
    [InlineData("void Ring(float toPixels) { DrawArc(Vector2.Zero, 4, 0, 1, 8, Colors.White, 1, antialiased: true); }")]
    [InlineData("void _Draw() { UiDashedBorder.DrawRoundedRect(this, new Rect2(), 2, Colors.White, 1, Transform2D.Identity, 4, 3); }")]
    public void Hard_stroke_is_flagged(string member) =>
        HardStrokes(member).ShouldHaveSingleItem();

    [Theory]
    [InlineData("void _Draw() { DrawLine(Vector2.Zero, Vector2.One, Colors.White, -1); }")]
    [InlineData("void _Draw() { var toPixels = UiPixelSpace.Enter(this, Transform2D.Identity); DrawPolyline([], Colors.White, 2, antialiased: true); }")]
    [InlineData("void Ring(Transform2D toPixels) { DrawArc(toPixels * Vector2.Zero, 4, 0, 1, 8, Colors.White, 1, antialiased: true); }")]
    [InlineData("void _Draw() { using var pen = UiPixelPen.Begin(this); UiDashedBorder.DrawRoundedRect(this, new Rect2(), 2, Colors.White, 1, pen.ToPixels, 4, 3); }")]
    [InlineData("void _Draw() { DrawRect(new Rect2(), Colors.White); }")]
    [InlineData("void _Draw() { using var pen = UiPixelPen.Begin(this); pen.Line(Vector2.Zero, Vector2.One, Colors.White, 2); }")]
    public void Smooth_stroke_or_fill_passes(string member) =>
        HardStrokes(member).ShouldBeEmpty();

    private static IEnumerable<string> HardStrokes(string member)
    {
        var snippet = CSharpSources.Snippet(member);
        var compilation = CSharpSources.Compile([.. CSharpSources.Project, snippet]);
        var model = compilation.GetSemanticModel(snippet.Tree);
        return snippet.Find(node => IsHardStroke(node, model));
    }

    private static bool IsHardStroke(SyntaxNode node, SemanticModel model)
    {
        if (node is not InvocationExpressionSyntax invocation || CSharpSources.Symbol(model, invocation) is not IMethodSymbol method)
        {
            return false;
        }

        // A helper that draws through a window-pixel map is only as good as the map its caller passes.
        if (TakesPixelMap(method))
        {
            return !InWindowPixels(invocation, model);
        }

        if (method.ContainingType is not { Name: "CanvasItem", ContainingNamespace.Name: "Godot" })
        {
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments;
        var isStroke = _strokes.Contains(method.Name)
            || (method.Name == "DrawRect" && arguments.Any(argument => ParameterOf(argument, method)?.Name == "filled" && argument.Expression.IsKind(SyntaxKind.FalseLiteralExpression)));
        var isHairline = arguments.Any(argument => ParameterOf(argument, method)?.Name == "width" && argument.Expression.ToString() == "-1");
        var isAntialiased = arguments.Any(argument => ParameterOf(argument, method)?.Name == "antialiased" && !argument.Expression.IsKind(SyntaxKind.FalseLiteralExpression));
        return isStroke && !isHairline && !(isAntialiased && InWindowPixels(invocation, model));
    }

    // In a method that maps to window pixels: it opens a pen, enters UiPixelSpace, or is handed the map.
    private static bool InWindowPixels(SyntaxNode node, SemanticModel model)
    {
        var declaration = node.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();
        if (declaration is null)
        {
            return false;
        }

        return (model.GetDeclaredSymbol(declaration) is IMethodSymbol method && TakesPixelMap(method))
            || declaration.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                .Any(access => access.ToString() is "UiPixelSpace.Enter" or "UiPixelPen.Begin");
    }

    private static bool TakesPixelMap(IMethodSymbol method) =>
        method.Parameters.Any(parameter => parameter is { Name: "toPixels", Type.Name: "Transform2D" });

    private static IParameterSymbol? ParameterOf(ArgumentSyntax argument, IMethodSymbol method)
    {
        if (argument.NameColon is { } name)
        {
            return method.Parameters.FirstOrDefault(parameter => parameter.Name == name.Name.Identifier.Text);
        }

        var index = ((ArgumentListSyntax)argument.Parent!).Arguments.IndexOf(argument);
        return index < method.Parameters.Length ? method.Parameters[index] : null;
    }
}
