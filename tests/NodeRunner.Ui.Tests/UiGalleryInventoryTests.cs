using System.Text.RegularExpressions;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// The Colors &amp; Styles page is authored in its scene, so these read the scene text to keep it a
/// complete inventory: a new token, icon or text style fails here until the page shows it. They
/// find each specimen by its group or its script, not by how the scene arranges it.
/// </summary>
public sealed partial class UiGalleryInventoryTests
{
    private const string _neon = "res://assets/themes/Neon.tres";
    private const string _paper = "res://assets/themes/Paper.tres";

    private static readonly IReadOnlyList<SceneNodes.SceneNode> _page =
        SceneNodes.InScene("screens/ColorsAndStylesScreen.tscn");

    private static readonly IReadOnlyDictionary<string, SceneNodes.SceneNode> _byPath =
        _page.ToDictionary(PathOf);

    private static readonly IReadOnlyDictionary<string, string> _resources = ExtResource()
        .Matches(File.ReadAllText(Path.Combine(
            SceneNodes.FindRepositoryRoot(), "project", "scenes", "screens", "ColorsAndStylesScreen.tscn")))
        .ToDictionary(match => match.Groups["id"].Value, match => match.Groups["path"].Value);

    [Fact]
    public void Every_swatch_shows_the_neon_or_the_paper_theme()
    {
        var themes = Swatches().Select(swatch => swatch.Theme).Distinct();

        themes.ShouldBe([_neon, _paper], ignoreOrder: true);
    }

    [Theory]
    [InlineData(_neon)]
    [InlineData(_paper)]
    public void Colours_show_every_token_once_in_each_theme(string theme)
    {
        var tokens = Swatches().Where(swatch => swatch.Theme == theme).Select(swatch => swatch.Token);

        tokens.ShouldBe(UiThemeExpander.AuthoredColors, ignoreOrder: true);
    }

    [Fact]
    public void Icon_set_shows_every_icon_once()
    {
        var shown = InGroup("inventory_icon").Select(node => (UiIconId)Int(node, "IconId", (int)UiIconId.None));

        shown.ShouldBe(Enum.GetValues<UiIconId>().Where(id => id != UiIconId.None), ignoreOrder: true);
    }

    [Fact]
    public void Icon_sizes_show_every_size_once()
    {
        var shown = InGroup("inventory_icon_size")
            .Select(node => (UiIconSize)Int(node, "IconSize", (int)UiIconSize.Standard));

        shown.ShouldBe(Enum.GetValues<UiIconSize>(), ignoreOrder: true);
    }

    [Fact]
    public void Text_styles_show_every_style_once()
    {
        var shown = InGroup("inventory_text_style")
            .Select(node => (UiTokens.Typography)Int(node, "TextStyle", (int)UiTokens.Typography.Body));

        shown.ShouldBe(Enum.GetValues<UiTokens.Typography>(), ignoreOrder: true);
    }

    private static IEnumerable<SceneNodes.SceneNode> InGroup(string group) =>
        _page.Where(node => Groups().Match(node.Node.Header) is { Success: true } groups
            && groups.Groups["list"].Value.Split(", ").Contains($"\"{group}\""));

    private static IEnumerable<(string? Theme, UiTokens.Color Token)> Swatches() =>
        _page.Where(node => node.Script?.EndsWith("/UiSwatch.cs", StringComparison.Ordinal) == true)
            .Select(node => (ThemeAbove(node), (UiTokens.Color)Int(node, "Token", (int)UiTokens.Color.Accent)));

    // The nearest ancestor that sets a theme; ancestors inside an instanced scene are skipped.
    private static string? ThemeAbove(SceneNodes.SceneNode node)
    {
        for (var path = node.Parent; path is not null; path = path == "." ? null : ParentOf(path))
        {
            if (_byPath.TryGetValue(path, out var ancestor)
                && ThemeProperty().Match(ancestor.Node.Body) is { Success: true } theme)
            {
                return _resources[theme.Groups["id"].Value];
            }
        }
        return null;
    }

    private static string PathOf(SceneNodes.SceneNode node) => node.Parent switch
    {
        null => ".",
        "." => node.Name,
        var parent => $"{parent}/{node.Name}",
    };

    private static string ParentOf(string path) =>
        path.LastIndexOf('/') is var slash and >= 0 ? path[..slash] : ".";

    // Godot omits a property saved at its default, so an absent value reads as the default.
    private static int Int(SceneNodes.SceneNode node, string property, int fallback)
    {
        var match = Regex.Match(node.Node.Body, $@"\n{property} = (?<value>-?\d+)");
        return match.Success ? int.Parse(match.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture) : fallback;
    }

    [GeneratedRegex("""\ntheme = ExtResource\("(?<id>[^"]+)"\)""")]
    private static partial Regex ThemeProperty();

    [GeneratedRegex("""groups=\[(?<list>[^\]]*)\]""")]
    private static partial Regex Groups();

    [GeneratedRegex("""\[ext_resource[^\]]*path="(?<path>[^"]+)"[^\]]*id="(?<id>[^"]+)"\]""")]
    private static partial Regex ExtResource();
}
