using System.Text.RegularExpressions;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// The Colors &amp; Styles page is authored in its scene, so this reads the scene text to keep it a
/// complete inventory: a new token, icon, icon size or text style fails here until the page shows it.
/// It finds each specimen by its group or its script, not by how the scene arranges it.
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

    // Each row: what the page shows, and what it must show exactly once.
    [Theory]
    [InlineData("swatch themes")]
    [InlineData("neon colours")]
    [InlineData("paper colours")]
    [InlineData("icons")]
    [InlineData("icon sizes")]
    [InlineData("text styles")]
    public void Page_shows_every_entry_once(string inventory)
    {
        var (shown, expected) = inventory switch
        {
            "swatch themes" => (Swatches().Select(swatch => swatch.Theme ?? "none").Distinct(), [_neon, _paper]),
            "neon colours" => (Colours(_neon), Names(UiThemeExpander.AuthoredColors)),
            "paper colours" => (Colours(_paper), Names(UiThemeExpander.AuthoredColors)),
            "icons" => (Shown<UiIconId>("inventory_icon", "IconId", UiIconId.None),
                Names(Enum.GetValues<UiIconId>().Where(id => id != UiIconId.None))),
            "icon sizes" => (Shown<UiIconSize>("inventory_icon_size", "IconSize", UiIconSize.Standard),
                Names(Enum.GetValues<UiIconSize>())),
            "text styles" => (Shown<UiTokens.Typography>("inventory_text_style", "TextStyle", UiTokens.Typography.Body),
                Names(Enum.GetValues<UiTokens.Typography>())),
            _ => throw new ArgumentOutOfRangeException(nameof(inventory)),
        };

        shown.ShouldBe(expected, ignoreOrder: true, $"The Colors & Styles page must show each of its {inventory} once.");
    }

    private static string[] Names<T>(IEnumerable<T> values) => [.. values.Select(value => value!.ToString()!)];

    private static IEnumerable<string> Colours(string theme) =>
        Swatches().Where(swatch => swatch.Theme == theme).Select(swatch => swatch.Token.ToString());

    // Godot omits a property saved at its default, so an absent value reads as the default.
    private static IEnumerable<string> Shown<T>(string group, string property, T fallback)
        where T : struct, Enum =>
        _page.Where(node => Groups().Match(node.Node.Header) is { Success: true } groups
                && groups.Groups["list"].Value.Split(", ").Contains($"\"{group}\""))
            .Select(node => ((T)(object)Int(node, property, Convert.ToInt32(fallback, System.Globalization.CultureInfo.InvariantCulture))).ToString());

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
