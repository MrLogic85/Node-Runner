using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Icons sample Linear so they stay smooth at fractional UI sizes; text keeps the project's Nearest
/// ("Icon filtering" in .claude/rules/ui-lib.md). Calls are bound with Roslyn, per type: the filter
/// is set on the node that draws, which the call that loads the icon does not name.
/// </summary>
public sealed partial class UiIconFilterGuardTests
{
    // These make icons for others; their callers are the hosts.
    private static readonly string[] _providers = ["UiIcons", "UiNotificationIcon", "UiChoiceTheme", "UiLinearIcon"];

    // Their icons draw on scene-authored nodes, which set the filter in the scene.
    private static readonly Dictionary<string, (string Scene, string[] Nodes)> _sceneFiltered = new()
    {
        ["UiSidePanel"] = ("ui/UiSidePanel.tscn", ["SidePanelIcon", "SidePanelChevron", "SidePanelTabChevron"]),
        ["UiDialogContent"] = ("ui/UiDialogContent.tscn", ["SemanticIcon"]),
        ["UiNotificationContent"] = ("ui/UiNotificationContent.tscn", ["SemanticIcon"]),
    };

    // Hosts that put the icon filter on a node with text children.
    private static readonly string[] _textUnderIcon = ["UiChoiceRow", "UiProgressRing"];

    [Fact]
    public void TextFilter_IsTheProjectDefault_AndIconsDiffer()
    {
        var settings = File.ReadAllText(Path.Combine(SceneNodes.FindRepositoryRoot(), "project", "project.godot"));
        var match = DefaultTextureFilter().Match(settings);
        // The setting counts Nearest = 0, Linear = 1 (Godot's default); TextureFilterEnum adds ParentNode first.
        var projectFilter = (Godot.CanvasItem.TextureFilterEnum)((match.Success ? int.Parse(match.Groups[1].Value) : 1) + 1);

        UiIcons.TextFilter.ShouldBe(projectFilter);
        UiIcons.IconFilter.ShouldNotBe(projectFilter);
    }

    [Fact]
    public void EveryIconHost_SetsTheIconFilter_AndKeepsTextUnderItOnTheTextFilter()
    {
        var hosts = Hosts(CSharpSources.Project, CSharpSources.ProjectCompilation);
        var inCode = hosts.Where(host => !_providers.Contains(host.Type) && !_sceneFiltered.ContainsKey(host.Type)).ToList();
        var textUnderIcon = hosts.Where(host => _textUnderIcon.Contains(host.Type)).ToList();
        var inScenes = _sceneFiltered.SelectMany(entry => entry.Value.Nodes
            .Where(name => !SceneNodes.InScene(entry.Value.Scene).Single(node => node.Name == name).Node.Body
                .Contains($"\ntexture_filter = {(int)UiIcons.IconFilter}\n", StringComparison.Ordinal))
            .Select(name => $"{entry.Value.Scene}: {name} does not set the icon filter"));

        inCode.ShouldNotBeEmpty();
        textUnderIcon.Select(host => host.Type).Distinct().Order().ShouldBe(_textUnderIcon.Order());
        _sceneFiltered.Keys.Except(hosts.Select(host => host.Type)).ShouldBeEmpty("A scene-filtered host no longer loads an icon.");
        inCode.Where(host => !host.SetsIconFilter).Select(host => $"{host.Where}: use UiIcons.UseIconFilter, UiIcons.IconFilter or UiLinearIcon")
            .Concat(textUnderIcon.Where(host => !host.SetsTextFilter).Select(host => $"{host.Where}: text under it calls UiIcons.UseTextFilter"))
            .Concat(inScenes)
            .ShouldBeEmpty("#734");
    }

    [Theory]
    [InlineData("TextureRect _r = new(); void A() { _r.Texture = UiIcons.Load(UiIconId.Back, UiIconSize.Standard); }", false)]
    [InlineData("void A() { AddThemeIconOverride(\"x\", new UiNotificationIcon(UiIconId.Back).Load(UiIconSize.Large)); }", false)]
    [InlineData("TextureRect _r = new(); void A() { _r.Texture = UiIcons.Load(UiIconId.Back, UiIconSize.Standard); UiIcons.UseIconFilter(_r); }", true)]
    [InlineData("void A(Button b) { UiIcons.Apply(b, UiIconId.Back, UiIconSize.Standard); b.Icon = new UiLinearIcon(b) { Icon = b.Icon }; }", true)]
    public void Guard_tells_whether_an_icon_host_sets_the_filter(string member, bool setsFilter) =>
        Snippet(member).ShouldHaveSingleItem().SetsIconFilter.ShouldBe(setsFilter);

    private static List<Host> Snippet(string member)
    {
        var snippet = CSharpSources.Snippet(member);
        return Hosts([snippet], CSharpSources.Compile([.. CSharpSources.Project, snippet]));
    }

    private static List<Host> Hosts(IEnumerable<CSharpSources.Source> sources, Compilation compilation)
    {
        var hosts = new List<Host>();
        foreach (var source in sources)
        {
            var model = compilation.GetSemanticModel(source.Tree);
            foreach (var type in source.Tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                         .Where(type => type.Parent is not ClassDeclarationSyntax))
            {
                var symbols = type.DescendantNodes()
                    .Where(node => node is SimpleNameSyntax or ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax)
                    .Select(node => (Node: node, Symbol: CSharpSources.Symbol(model, node)))
                    .Where(entry => entry.Symbol is not null)
                    .ToList();
                var load = symbols.FirstOrDefault(entry => IsIconSource(entry.Symbol!));
                if (load.Node is null)
                {
                    continue;
                }

                hosts.Add(new Host(
                    type.Identifier.Text,
                    source.Describe(load.Node),
                    symbols.Any(entry => SetsIconFilter(entry.Symbol!)),
                    symbols.Any(entry => Is(entry.Symbol!, "UiIcons", nameof(UiIcons.UseTextFilter)))));
            }
        }

        return hosts;
    }

    private static bool IsIconSource(ISymbol symbol) =>
        Is(symbol, "UiIcons", nameof(UiIcons.Load)) || Is(symbol, "UiIcons", nameof(UiIcons.Apply))
        || Is(symbol, "UiIcons", nameof(UiIcons.Create)) || Is(symbol, "UiNotificationIcon", "Load")
        || Is(symbol, "UiChoiceTheme", "Create");

    private static bool SetsIconFilter(ISymbol symbol) =>
        Is(symbol, "UiIcons", nameof(UiIcons.IconFilter)) || Is(symbol, "UiIcons", nameof(UiIcons.UseIconFilter))
        || Is(symbol, "UiIcons", nameof(UiIcons.Create)) || Is(symbol, "UiLinearIcon", ".ctor");

    private static bool Is(ISymbol symbol, string type, string member) =>
        symbol.Name == member && symbol.ContainingType?.Name == type
        && symbol.ContainingNamespace?.ToDisplayString() == "NodeRunner.Ui.Lib";

    private sealed record Host(string Type, string Where, bool SetsIconFilter, bool SetsTextFilter);

    [GeneratedRegex(@"(?m)^textures/canvas_textures/default_texture_filter=(\d+)")]
    private static partial Regex DefaultTextureFilter();
}
