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
    /// The one library file that pins a colour override: UiIcons' tinted overloads, which
    /// lib components pass a Theme colour.
    /// </summary>
    private const string _tintHelper = "ui/lib/UiIcons.cs";

    [Fact]
    public void Source_takes_colours_from_the_theme()
    {
        var violations = ColorLiterals(CSharpSources.Project).ToList();

        violations.ShouldBeEmpty(
            "Take colours from the Theme (UiThemeLookup.Color, VisualTheme) and derive with WithAlpha/ScaleAlpha; " +
            "only Colors.White and Colors.Transparent are neutral.");
    }

    [Fact]
    public void Colour_guard_binds_every_type_the_project_names()
    {
        var unresolved = CSharpSources.ProjectCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Id == "CS0246")
            .Select(diagnostic => diagnostic.ToString())
            .ToList();

        unresolved.ShouldBeEmpty("A type the guard cannot bind hides the colours created through it; add its reference or using.");
    }

    [Fact]
    public void Component_library_names_every_number()
    {
        var violations = CSharpSources.Project
            .Where(source => FollowsLibraryRules(source.Path))
            .SelectMany(source => source.Find(node => IsUnnamedNumber(source.Path, node)))
            .ToList();

        violations.ShouldBeEmpty(
            "Take dimensions from UiSize, UiLayout or UiSpacing; name any other value as a const or static readonly field.");
    }

    [Fact]
    public void Only_widgets_that_draw_skip_the_number_rule() =>
        RewrittenUi.DrawnWidgets
            .Where(path => !RewrittenUi.Widgets.Contains(path) || !CSharpSources.Project.Single(source => source.Path == path)
                .Tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.Text == "_Draw"))
            .ShouldBeEmpty("A drawn widget is a rewritten widget that overrides _Draw.");

    [Fact]
    public void Rewritten_screens_declare_no_numbers()
    {
        var violations = CSharpSources.Project
            .Where(source => RewrittenUi.Screens.Contains(source.Path))
            .SelectMany(source => source.Find(IsScreenNumber))
            .ToList();

        violations.ShouldBeEmpty(
            "A rewritten screen takes numbers from UiSize, UiLayout, UiSpacing or its view-model; " +
            "a layout value belongs in the scene (#310).");
    }

    [Fact]
    public void Rewritten_screens_neither_build_nor_restyle_controls()
    {
        var compilation = CSharpSources.ProjectCompilation;
        var violations = CSharpSources.Project
            .Where(source => RewrittenUi.Screens.Contains(source.Path) || RewrittenUi.Widgets.Contains(source.Path))
            .SelectMany(source =>
            {
                var model = compilation.GetSemanticModel(source.Tree);
                return source.Find(node => BuildsOrRestyles(source.Path, node, model));
            })
            .ToList();

        violations.ShouldBeEmpty(
            "Author static layout in the scene; code instantiates only library components and widgets " +
            "for runtime content, and never sets theme overrides, styleboxes or sizes (#310).");
    }

    private const string _drawnWidget = "ui/widgets/BuildCanvas.cs";

    [Theory]
    [InlineData(_drawnWidget, "void _Draw() { DrawCircle(Vector2.Zero, 1.7f, Colors.White); }", false)]
    [InlineData(_drawnWidget, "void Select(Rect2 rect) { var tap = rect.Size.X < 8; }", true)]
    [InlineData("ui/lib/UiCard.cs", "void DrawGlow() { var radius = 1.35f; }", true)]
    public void Number_outside_a_drawn_widget_drawing_is_flagged(string path, string member, bool flagged) =>
        CSharpSources.Snippet(member).Find(node => IsUnnamedNumber(path, node)).Count().ShouldBe(flagged ? 1 : 0);

    [Theory]
    [InlineData("const float RowHeight = 48;", true)]
    [InlineData("float M(float x) => (x * 0.5f) - 1 + 2;", false)]
    [InlineData("enum Kind { A = 4 }", false)]
    public void Screen_number_is_flagged(string member, bool flagged) =>
        CSharpSources.Snippet(member).Find(IsScreenNumber).Count().ShouldBe(flagged ? 1 : 0);

    // A drawn widget may place a control over its drawing and size the viewport it draws, never size a control.
    [Theory]
    [InlineData("ui/screens/Snippet.cs", "Control M() => new HBoxContainer();", true)]
    [InlineData("ui/screens/Snippet.cs", "StyleBox M() => new StyleBoxFlat();", true)]
    [InlineData("ui/screens/Snippet.cs", "void M(Label label) { label.AddThemeFontSizeOverride(\"font_size\", 1); }", true)]
    [InlineData("ui/screens/Snippet.cs", "UiButton M() => new UiButton { Position = Vector2.One };", true)]
    [InlineData(_drawnWidget, "void M(Control handle) { handle.Position = Vector2.One; }", false)]
    [InlineData(_drawnWidget, "void M(Control handle) { handle.Size = Vector2.One; }", true)]
    [InlineData(_drawnWidget, "void M(SubViewport picture) { picture.Size = Vector2I.One; }", false)]
    public void Building_or_restyling_is_flagged(string path, string member, bool flagged) =>
        BuildsOrRestyles(member, path).Count().ShouldBe(flagged ? 1 : 0);

    [Fact]
    public void Component_library_selects_colours_by_theme_variation()
    {
        var violations = CSharpSources.Project
            .Where(source => FollowsLibraryRules(source.Path) && source.Path != _tintHelper)
            .SelectMany(source => source.Find(IsPinnedColour))
            .ToList();

        violations.ShouldBeEmpty(
            "Select a generated Theme variation (ThemeTypeVariation, UiThemeLookup.ApplyTextStyle, " +
            "UiIcons.Apply without a tint) so a Theme swap restyles the node; add the variation to UiThemeExpander.");
    }

    [Fact]
    public void Ui_orders_drawing_by_tree_not_z_index()
    {
        var violations = CSharpSources.Project
            .Where(source => source.Path.StartsWith("ui/", StringComparison.Ordinal))
            .SelectMany(source => source.Find(SetsZIndex))
            .ToList();

        violations.ShouldBeEmpty(
            "ZIndex sorts across the whole CanvasLayer, so a raised part draws through every screen and " +
            "overlay above it (#463); order children in the tree instead (InternalMode.Back draws last), " +
            "and float an overlay on a UiLayers level with UiLevelLayer (#768).");
    }

    // A screen's own queue is freed with its scene, dropping a notification raised just before
    // the router changes scene. Popup Gallery keeps one so its specimens follow its theme.
    [Fact]
    public void Notifications_are_queued_on_the_app_layer()
    {
        var owners = new[] { "ui/lib/UiNotificationLayer.cs", "ui/screens/PopupGalleryScreen.cs" };
        var compilation = CSharpSources.ProjectCompilation;
        var violations = CSharpSources.Project
            .Where(source => !owners.Contains(source.Path))
            .SelectMany(source =>
            {
                var model = compilation.GetSemanticModel(source.Tree);
                return source.Find(node => CreatesNotificationQueue(node, model));
            })
            .ToList();

        violations.ShouldBeEmpty("Queue notifications with UiNotificationLayer.Enqueue so they outlive scene changes (#472).");
        SceneNodes.WithScript("UiNotification.cs").ShouldBeEmpty();
    }

    [Fact]
    public void Notification_queue_and_z_index_guards_flag_their_mistake()
    {
        var snippet = CSharpSources.Snippet(
            "UiNotification N = new(); Label L = new Label { ZIndex = 1 }; void M(Control control) { control.ZIndex = 1; }");
        var model = CSharpSources.Compile([.. CSharpSources.Project, snippet]).GetSemanticModel(snippet.Tree);

        snippet.Find(node => CreatesNotificationQueue(node, model)).ShouldHaveSingleItem();
        snippet.Find(SetsZIndex).Count().ShouldBe(2);
    }

    [Theory]
    [InlineData("void M(Label label) { label.AddThemeColorOverride(\"font_color\", Colors.White); }", true)]
    [InlineData("void M(Button button, Color tint) => UiIcons.Apply(button, UiIconId.Edit, UiIconSize.Small, tint);", true)]
    [InlineData("void M(Button button) => UiIcons.Apply(button, UiIconId.Edit, UiIconSize.Small);", false)]
    public void Pinned_colour_is_flagged(string member, bool flagged) =>
        CSharpSources.Snippet(member).Find(IsPinnedColour).Count().ShouldBe(flagged ? 1 : 0);

    [Theory]
    [InlineData("Color M() => Colors.Red;", true)]
    [InlineData("static readonly Color[] Palette = [new(1, 0, 0)];", true)]
    [InlineData("void M(ColorRect rect) { rect.Color = new(0, 0, 0, 0.5f); }", true)]
    [InlineData("Color M() => Color.Color8(255, 0, 0);", true)]
    [InlineData("Color M() => Colors.White with { A = 0.5f };", false)]
    [InlineData("Color M(Color themed) => new(themed, 0.5f);", false)]
    public void Colour_literal_is_flagged(string member, bool flagged) =>
        ColorLiterals([CSharpSources.Snippet(member)]).Count().ShouldBe(flagged ? 1 : 0);

    [Theory]
    [InlineData("void M() { var seconds = 0.8f; }", true)]
    [InlineData("static float Width = 12;", true)]
    [InlineData("static readonly float Width = 12;", false)]
    [InlineData("[System.ComponentModel.DefaultValue(4)] float Width { get; set; }", false)]
    public void Inline_number_is_flagged(string member, bool flagged) =>
        CSharpSources.Snippet(member).Find(IsInlineNumber).Count().ShouldBe(flagged ? 1 : 0);

    private static IEnumerable<string> ColorLiterals(IReadOnlyList<CSharpSources.Source> sources)
    {
        var compilation = CSharpSources.Compile(sources);
        return sources.SelectMany(source =>
        {
            var model = compilation.GetSemanticModel(source.Tree);
            return source.Find(node => IsColorLiteral(node, model));
        });
    }

    private static bool IsColorLiteral(SyntaxNode node, SemanticModel model) => node switch
    {
        BaseObjectCreationExpressionSyntax creation =>
            IsGodotType(model.GetTypeInfo(creation).Type, "Color") && StartsWithLiteral(creation.ArgumentList),
        InvocationExpressionSyntax invocation =>
            CSharpSources.Symbol(model, invocation) is IMethodSymbol { IsStatic: true } method
            && IsGodotType(method.ContainingType, "Color")
            && _colorFactories.Contains(method.Name),
        MemberAccessExpressionSyntax access =>
            CSharpSources.Symbol(model, access) is { IsStatic: true } member and (IPropertySymbol or IFieldSymbol)
            && IsGodotType(member.ContainingType, "Colors")
            && !_neutralColors.Contains(member.Name),
        _ => false,
    };

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

    private static bool CreatesNotificationQueue(SyntaxNode node, SemanticModel model) =>
        node is BaseObjectCreationExpressionSyntax creation
        && model.GetTypeInfo(creation).Type is { Name: "UiNotification", ContainingNamespace.Name: "Lib" };

    private static bool SetsZIndex(SyntaxNode node) => node is AssignmentExpressionSyntax
    {
        Left: IdentifierNameSyntax { Identifier.Text: "ZIndex" }
            or MemberAccessExpressionSyntax { Name.Identifier.Text: "ZIndex" },
    };

    private static IEnumerable<string> BuildsOrRestyles(string member, string path = "ui/screens/Snippet.cs")
    {
        var snippet = CSharpSources.Snippet(member);
        var compilation = CSharpSources.Compile([.. CSharpSources.Project, snippet]);
        var model = compilation.GetSemanticModel(snippet.Tree);
        return snippet.Find(node => BuildsOrRestyles(path, node, model));
    }

    // A drawn widget may move scene-authored controls (the Select handles) to where its drawing is,
    // and size a SubViewport it draws from (ArenaShadows): its pixels, not a layout.
    private static bool BuildsOrRestyles(string path, SyntaxNode node, SemanticModel model) => node switch
    {
        BaseObjectCreationExpressionSyntax creation => model.GetTypeInfo(creation).Type is { } type
            && (IsGodotSubclass(type, "StyleBox")
                || ((IsGodotSubclass(type, "Control") || IsGodotSubclass(type, "Window")) && !IsLibraryType(type))),
        InvocationExpressionSyntax invocation =>
            CSharpSources.Symbol(model, invocation) is IMethodSymbol { Name: var name } method
            && name.StartsWith("AddTheme", StringComparison.Ordinal)
            && name.EndsWith("Override", StringComparison.Ordinal)
            && method.ContainingType.ContainingNamespace.Name == "Godot",
        AssignmentExpressionSyntax assignment =>
            CSharpSources.Symbol(model, assignment.Left) is IPropertySymbol { Name: "CustomMinimumSize" or "Size" or "Position" } property
            && property.ContainingType.ContainingNamespace.Name == "Godot"
            && (property.Name != "Position" || IsGodotSubclass(property.ContainingType, "Control") || IsGodotSubclass(property.ContainingType, "Window"))
            && !(property.Name == "Position" && RewrittenUi.DrawnWidgets.Contains(path))
            && !(property.Name == "Size" && IsGodotSubclass(property.ContainingType, "SubViewport") && RewrittenUi.DrawnWidgets.Contains(path)),
        _ => false,
    };

    private static bool IsGodotSubclass(ITypeSymbol type, string godotBase)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (IsGodotType(current, godotBase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLibraryType(ITypeSymbol type) =>
        type.ContainingNamespace.ToDisplayString() is "NodeRunner.Ui.Lib" or "NodeRunner.Ui.Widgets";

    private static bool IsScreenNumber(SyntaxNode node) =>
        node is LiteralExpressionSyntax literal
        && literal.IsKind(SyntaxKind.NumericLiteralExpression)
        && !_inlineNumbers.Contains(Convert.ToDouble(literal.Token.Value, System.Globalization.CultureInfo.InvariantCulture))
        && !literal.Ancestors().Any(ancestor => ancestor is EnumMemberDeclarationSyntax or AttributeSyntax);

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

    // A drawn widget's _Draw and Draw* helpers keep their drawing literals inline.
    private static bool IsUnnamedNumber(string path, SyntaxNode node) =>
        IsInlineNumber(node) && !(RewrittenUi.DrawnWidgets.Contains(path) && IsInDrawingMethod(node));

    private static bool IsInDrawingMethod(SyntaxNode node) =>
        node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.Text is { } name
        && (name == "_Draw" || name.StartsWith("Draw", StringComparison.Ordinal));

    private static bool FollowsLibraryRules(string path) =>
        path.StartsWith("ui/lib/", StringComparison.Ordinal) || RewrittenUi.Widgets.Contains(path);
}
