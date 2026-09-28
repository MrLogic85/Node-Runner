using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Guards C# UI source against hardcoded colours and sizes. Colours are resolved through a
/// Roslyn semantic model over <c>project/src</c> against GodotSharp, so target-typed
/// <c>new(...)</c> is caught in any position; numbers are a syntax-only check. No Godot
/// runtime is involved.
/// </summary>
public sealed class UiSourceGuardTests
{
    /// <summary>Neutral modulation and masks; every palette colour comes from the Theme.</summary>
    private static readonly string[] _neutralColors = ["White", "Transparent"];

    private static readonly string[] _colorFactories =
        ["Color8", "FromHtml", "FromHsv", "FromOkHsl", "FromString", "FromRgbe9995"];

    /// <summary>Identity, halving and doubling (and their negatives) read clearer inline.</summary>
    private static readonly double[] _inlineNumbers = [0, 1, 2, 0.5];

    /// <summary>
    /// Folders whose dimensions must come from UiSize, UiLayout or UiSpacing. Game screens and
    /// widgets join as they are migrated to the component library (#310).
    /// </summary>
    private static readonly string[] _sizeGuardedFolders = ["ui/lib"];

    /// <summary>
    /// The one library file that pins a colour override: UiIcons' tint helper, which game screens
    /// and widgets still call until they move to the component library (#310).
    /// </summary>
    private const string _tintHelper = "ui/lib/UiIcons.cs";

    private static readonly Lazy<IReadOnlyList<MetadataReference>> _references = new(LoadReferences);

    /// <summary>The SDK's implicit usings (<c>ImplicitUsings</c> in Directory.Build.props).</summary>
    private static readonly SyntaxTree _implicitUsings = CSharpSyntaxTree.ParseText(
        """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Net.Http;
        global using System.Threading;
        global using System.Threading.Tasks;
        """);

    [Fact]
    public void Source_takes_colours_from_the_theme()
    {
        var violations = ColorLiterals(ProjectSources()).ToList();

        violations.ShouldBeEmpty(
            "Take colours from the Theme (UiThemeLookup.Color, VisualTheme) and derive with WithAlpha/ScaleAlpha; " +
            "only Colors.White and Colors.Transparent are neutral.");
    }

    [Fact]
    public void Colour_guard_binds_every_type_the_project_names()
    {
        var unresolved = Compile(ProjectSources()).GetDiagnostics()
            .Where(diagnostic => diagnostic.Id == "CS0246")
            .Select(diagnostic => diagnostic.ToString())
            .ToList();

        unresolved.ShouldBeEmpty("A type the guard cannot bind hides the colours created through it; add its reference or using.");
    }

    [Fact]
    public void Component_library_names_every_number()
    {
        var violations = ProjectSources()
            .Where(source => _sizeGuardedFolders.Any(folder => source.Path.StartsWith(folder + "/", StringComparison.Ordinal)))
            .SelectMany(source => source.Find(IsInlineNumber))
            .ToList();

        violations.ShouldBeEmpty(
            "Take dimensions from UiSize, UiLayout or UiSpacing; name any other value as a const or static readonly field.");
    }

    [Fact]
    public void Component_library_selects_colours_by_theme_variation()
    {
        var violations = ProjectSources()
            .Where(source => source.Path.StartsWith("ui/lib/", StringComparison.Ordinal) && source.Path != _tintHelper)
            .SelectMany(source => source.Find(IsPinnedColour))
            .ToList();

        violations.ShouldBeEmpty(
            "Select a generated Theme variation (ThemeTypeVariation, UiThemeLookup.ApplyTextStyle, " +
            "UiIcons.Apply without a tint) so a Theme swap restyles the node; add the variation to UiThemeExpander.");
    }

    [Theory]
    [InlineData("void M(Label label) { label.AddThemeColorOverride(\"font_color\", Colors.White); }")]
    [InlineData("void M(Button button, Color tint) => UiIcons.Apply(button, UiIconId.Edit, UiIconSize.Small, tint);")]
    public void Pinned_colour_is_flagged(string member) =>
        Snippet(member).Find(IsPinnedColour).ShouldHaveSingleItem();

    [Theory]
    [InlineData("void M(Label label) { label.RemoveThemeColorOverride(\"font_color\"); }")]
    [InlineData("void M(Button button) => UiIcons.Apply(button, UiIconId.Edit, UiIconSize.Small);")]
    public void Theme_variation_passes(string member) =>
        Snippet(member).Find(IsPinnedColour).ShouldBeEmpty();

    [Theory]
    [InlineData("Color M() => new Color(0.1f, 0.2f, 0.3f);")]
    [InlineData("Color M() => new Godot.Color(\"#ffffff\");")]
    [InlineData("Color M() => Color.Color8(1, 2, 3);")]
    [InlineData("Color M() => Color.FromHtml(\"#ffffff\");")]
    [InlineData("Color M() => Colors.Red;")]
    [InlineData("Color M() => new(0.1f, 0.2f, 0.3f);")]
    [InlineData("Color M() { return new(0.1f, 0.2f, 0.3f); }")]
    [InlineData("void M(ColorRect rect) { rect.Color = new(0, 0, 0, 0.5f); }")]
    [InlineData("ColorRect M() => new() { Color = new(0, 0, 0, 0.5f) };")]
    [InlineData("void M(Control control) => control.DrawRect(new Rect2(), new(1, 0, 0));")]
    [InlineData("void M() { Color Local() => new(1, 0, 0); }")]
    [InlineData("static readonly Color[] Palette = [new(1, 0, 0)];")]
    [InlineData("Dictionary<int, Color> Map = new() { [1] = new(1, 0, 0) };")]
    public void Colour_literal_is_flagged(string member) =>
        ColorLiterals([Snippet(member)]).ShouldHaveSingleItem();

    [Theory]
    [InlineData("Color M() => Colors.White with { A = 0.5f };")]
    [InlineData("Color M() => Colors.Transparent;")]
    [InlineData("Color M(Color themed) => new(themed, 0.5f);")]
    [InlineData("Color M(Color themed) => themed.Lerp(Colors.White, 0.25f);")]
    [InlineData("Vector2 M() => new(3, 4);")]
    public void Theme_derived_colour_passes(string member) =>
        ColorLiterals([Snippet(member)]).ShouldBeEmpty();

    [Theory]
    [InlineData("float M() => 12;")]
    [InlineData("void M() { var seconds = 0.8f; }")]
    [InlineData("float Width = 3;")]
    [InlineData("float Width { get; set; } = 3;")]
    [InlineData("static float Width = 3;")]
    public void Inline_number_is_flagged(string member) =>
        Snippet(member).Find(IsInlineNumber).ShouldHaveSingleItem();

    [Theory]
    [InlineData("const float Width = 12;")]
    [InlineData("static readonly float Width = 12;")]
    [InlineData("void M() { const int points = 32; }")]
    [InlineData("float M(float x) => (x * 0.5f) - 1 + 2 - 0.5f;")]
    [InlineData("enum Kind { A = 4 }")]
    [InlineData("[System.ComponentModel.DefaultValue(4)] float Width { get; set; }")]
    public void Named_number_passes(string member) =>
        Snippet(member).Find(IsInlineNumber).ShouldBeEmpty();

    private static IEnumerable<string> ColorLiterals(IReadOnlyList<Source> sources)
    {
        var compilation = Compile(sources);
        return sources.SelectMany(source =>
        {
            var model = compilation.GetSemanticModel(source.Tree);
            return source.Find(node => IsColorLiteral(node, model));
        });
    }

    private static CSharpCompilation Compile(IReadOnlyList<Source> sources) => CSharpCompilation.Create(
        "UiSourceGuard",
        sources.Select(source => source.Tree).Append(_implicitUsings),
        _references.Value,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static bool IsColorLiteral(SyntaxNode node, SemanticModel model) => node switch
    {
        BaseObjectCreationExpressionSyntax creation =>
            IsGodotType(model.GetTypeInfo(creation).Type, "Color") && StartsWithLiteral(creation.ArgumentList),
        InvocationExpressionSyntax invocation =>
            Symbol(model, invocation) is IMethodSymbol { IsStatic: true } method
            && IsGodotType(method.ContainingType, "Color")
            && _colorFactories.Contains(method.Name),
        MemberAccessExpressionSyntax access =>
            Symbol(model, access) is { IsStatic: true } member and (IPropertySymbol or IFieldSymbol)
            && IsGodotType(member.ContainingType, "Colors")
            && !_neutralColors.Contains(member.Name),
        _ => false,
    };

    private static ISymbol? Symbol(SemanticModel model, SyntaxNode node)
    {
        var info = model.GetSymbolInfo(node);
        return info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
    }

    private static bool IsGodotType(ITypeSymbol? type, string name) =>
        type is { ContainingNamespace.Name: "Godot" } && type.Name == name;

    private static bool StartsWithLiteral(ArgumentListSyntax? arguments) =>
        arguments?.Arguments.FirstOrDefault()?.Expression switch
        {
            LiteralExpressionSyntax => true,
            PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax } => true,
            _ => false,
        };

    private static bool IsPinnedColour(SyntaxNode node) => node is InvocationExpressionSyntax invocation
        && invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name.Identifier.Text: "AddThemeColorOverride" } => true,
            IdentifierNameSyntax { Identifier.Text: "AddThemeColorOverride" } => true,
            MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "UiIcons" }, Name.Identifier.Text: "Apply" }
                => invocation.ArgumentList.Arguments.Count == 4,
            _ => false,
        };

    private static bool IsInlineNumber(SyntaxNode node) =>
        node is LiteralExpressionSyntax literal
        && literal.IsKind(SyntaxKind.NumericLiteralExpression)
        && !_inlineNumbers.Contains(Convert.ToDouble(literal.Token.Value, System.Globalization.CultureInfo.InvariantCulture))
        && !literal.Ancestors().Any(IsNamedValue);

    private static bool IsNamedValue(SyntaxNode node) => node switch
    {
        FieldDeclarationSyntax field =>
            field.Modifiers.Any(SyntaxKind.ConstKeyword)
            || (field.Modifiers.Any(SyntaxKind.StaticKeyword) && field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)),
        LocalDeclarationStatementSyntax local => local.IsConst,
        EnumMemberDeclarationSyntax or AttributeSyntax => true,
        _ => false,
    };

    private static IReadOnlyList<Source> ProjectSources()
    {
        var root = Path.Combine(SceneNodes.FindRepositoryRoot(), "project", "src");
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => new Source(
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                CSharpSyntaxTree.ParseText(File.ReadAllText(path))))
            .ToList();
    }

    private static Source Snippet(string member) => new(
        "snippet.cs",
        CSharpSyntaxTree.ParseText(
            $"using Godot; partial class Snippet {{ {member} }}"));

    /// <summary>
    /// The BCL, GodotSharp and the pure libs; the Godot project itself is the compiled source.
    /// Generated Godot partials are absent, which only fails unrelated binds.
    /// </summary>
    private static IReadOnlyList<MetadataReference> LoadReferences()
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return platform
            .Concat(Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll")
                .Where(path => Path.GetFileName(path) is "GodotSharp.dll" or "NodeRunner.App.dll" or "NodeRunner.Domain.dll" or "NodeRunner.ML.dll"))
            .Distinct()
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
    }

    private sealed record Source(string Path, SyntaxTree Tree)
    {
        public IEnumerable<string> Find(Func<SyntaxNode, bool> isViolation) =>
            Tree.GetRoot().DescendantNodes()
                .Where(isViolation)
                .Select(node => $"{Path}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {node}");
    }
}
