using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>The app icon (#820) is wired into the Android export and drawn from the Neon palette.</summary>
public sealed partial class AppIconTests
{
    private const string _iconRoot = "res://assets/icons/app/";
    private static readonly string[] _shapes = ["path", "circle", "ellipse", "rect", "line", "polyline", "polygon"];
    private static readonly string[] _paintAttributes = ["fill", "stroke", "stop-color"];

    [Theory]
    [InlineData("launcher_icons/main_192x192", "main.svg", 192)]
    [InlineData("launcher_icons/adaptive_foreground_432x432", "foreground.svg", 432)]
    [InlineData("launcher_icons/adaptive_background_432x432", "background.svg", 432)]
    [InlineData("launcher_icons/adaptive_monochrome_432x432", "monochrome.svg", 432)]
    [InlineData("splash_screen/icon", "splash.svg", 432)]
    public void Android_export_uses_the_app_icon_at_its_native_size(string option, string file, int size)
    {
        Setting("export_presets.cfg", option).ShouldBe(_iconRoot + file);

        // Godot imports an SVG at its declared size, and the export resizes from that image.
        var svg = XElement.Parse(Source(file));
        ((string?)svg.Attribute("width")).ShouldBe(size.ToString(CultureInfo.InvariantCulture));
        ((string?)svg.Attribute("height")).ShouldBe(size.ToString(CultureInfo.InvariantCulture));
        ((string?)svg.Attribute("viewBox")).ShouldBe($"0 0 {size} {size}");
    }

    [Fact]
    public void Project_icon_is_the_launcher_icon() =>
        Setting("project.godot", "config/icon").ShouldBe(_iconRoot + "main.svg");

    [Theory]
    [InlineData("main.svg")]
    [InlineData("foreground.svg")]
    [InlineData("background.svg")]
    [InlineData("splash.svg")]
    public void Colours_come_from_the_Neon_palette(string file)
    {
        var neon = ThemeFile.For(UiTokenType.Neon);
        var palette = Enum.GetValues<UiTokens.Color>()
            .Select(neon.Color)
            .Where(color => color.A >= 1f)
            .Select(color => color.ToHtml(false).ToUpperInvariant())
            .ToHashSet();

        Paints(file).Where(paint => !palette.Contains(paint)).ShouldBeEmpty();
    }

    [Fact]
    public void Monochrome_layer_is_flat_white() =>
        Paints("monochrome.svg").Distinct().ShouldBe(["FFFFFF"]);

    [Theory]
    [InlineData("main.svg")]
    [InlineData("splash.svg")]
    public void Icon_draws_the_foreground_art_unchanged(string file)
    {
        var art = Children(XElement.Parse(Source("foreground.svg")));

        // The file draws its own backdrop, then ends one group (or the root) with foreground.svg's art,
        // element for element in paint order.
        var copied = XElement.Parse(Source(file)).DescendantsAndSelf()
            .Select(Children)
            .Any(children => children.Count >= art.Count && children.TakeLast(art.Count).SequenceEqual(art));
        copied.ShouldBeTrue($"{file} should end a group with foreground.svg's elements in order");
    }

    private static string Source(string file) =>
        File.ReadAllText(Path.Combine(ThemeFile.ProjectRoot, "assets", "icons", "app", file));

    private static List<string> Children(XElement element) =>
        [.. element.Elements().Select(child => child.ToString())];

    /// <summary>Every paint in the SVG as upper-case RRGGBB; any other colour form is returned as written, so it fails a palette check.</summary>
    private static List<string> Paints(string file)
    {
        var svg = XElement.Parse(Source(file));
        svg.Descendants().Where(element => element.Attribute("style") is not null).ShouldBeEmpty("paint is set by attributes, not styles");
        svg.Descendants()
            .Where(element => _shapes.Contains(element.Name.LocalName))
            .Where(shape => shape.AncestorsAndSelf().All(element => element.Attribute("fill") is null))
            .ShouldBeEmpty("an unset fill paints black");

        return [.. svg.DescendantsAndSelf().Attributes()
            .Where(attribute => _paintAttributes.Contains(attribute.Name.LocalName))
            .Select(attribute => attribute.Value)
            .Where(value => value != "none" && !value.StartsWith("url(#", StringComparison.Ordinal))
            .Select(value => HexColour().IsMatch(value) ? value[1..].ToUpperInvariant() : value)];
    }

    private static string Setting(string file, string key)
    {
        var values = File.ReadLines(Path.Combine(ThemeFile.ProjectRoot, file))
            .Where(line => line.StartsWith(key + "=", StringComparison.Ordinal))
            .Select(line => line[(key.Length + 1)..].Trim('"'))
            .Distinct()
            .ToList();
        values.Count.ShouldBe(1, $"{file} should set {key} to one value");
        return values[0];
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColour();
}
