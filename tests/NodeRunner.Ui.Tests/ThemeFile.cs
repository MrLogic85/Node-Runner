using System.Globalization;
using System.Text.RegularExpressions;
using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Reads a theme <c>.tres</c> as text so pure tests can check the committed resource files
/// without the Godot runtime.
/// </summary>
internal sealed partial class ThemeFile
{
    private readonly Dictionary<string, string> _items;
    private readonly Dictionary<string, Dictionary<string, string>> _fonts;
    private readonly Dictionary<string, string> _extPaths;

    private ThemeFile(
        Dictionary<string, string> items,
        Dictionary<string, Dictionary<string, string>> fonts,
        Dictionary<string, string> extPaths)
    {
        _items = items;
        _fonts = fonts;
        _extPaths = extPaths;
    }

    public static ThemeFile For(UiTokenType type) => Load(UiThemes.PathFor(type));

    public static ThemeFile Load(string resourcePath) =>
        Parse(File.ReadAllText(Path.Combine(ProjectRoot, resourcePath["res://".Length..])));

    public static string ProjectRoot { get; } = Path.Combine(FindRepositoryRoot(), "project");

    public IEnumerable<string> ItemKeys => _items.Keys;

    public bool Has(string type, string kind, string name) => _items.ContainsKey($"{type}/{kind}/{name}");

    public Color Color(string type, string name) => ParseColor(Item(type, "colors", name));

    public Color Color(UiTokens.Color token) => Color(UiThemes.TokenType, UiTokens.Name(token));

    public int Constant(string type, string name) => int.Parse(Item(type, "constants", name), CultureInfo.InvariantCulture);

    public float Alpha(UiTokens.Alpha token) => Constant(UiThemes.TokenType, UiTokens.Name(token)) / 255f;

    public bool Flag(UiTokens.Flag token) => Constant(UiThemes.TokenType, UiTokens.Name(token)) != 0;

    public int FontSize(string type) => int.Parse(Item(type, "font_sizes", "font_size"), CultureInfo.InvariantCulture);

    public string FontPath(string type) =>
        _extPaths[ExtResourceReference().Match(Font(type)["base_font"]).Groups[1].Value];

    public int SpacingGlyph(string type) =>
        Font(type).TryGetValue("spacing_glyph", out var value) ? int.Parse(value, CultureInfo.InvariantCulture) : 0;

    private Dictionary<string, string> Font(string type)
    {
        var subResource = SubResourceReference().Match(Item(type, "fonts", "font"));
        subResource.Success.ShouldBeTrue($"{type}/fonts/font is not a sub-resource");
        return _fonts[subResource.Groups[1].Value];
    }

    public string BaseType(string type) => Item(type, "base_type", null).TrimStart('&').Trim('"');

    private string Item(string type, string kind, string? name)
    {
        var key = name is null ? $"{type}/{kind}" : $"{type}/{kind}/{name}";
        _items.TryGetValue(key, out var value).ShouldBeTrue($"Theme file has no {key}");
        return value!;
    }

    private static ThemeFile Parse(string text)
    {
        var extPaths = ExtResource().Matches(text).ToDictionary(match => match.Groups["id"].Value, match => match.Groups["path"].Value);
        var fonts = new Dictionary<string, Dictionary<string, string>>();
        var items = new Dictionary<string, string>();
        string? section = null;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('['))
            {
                var sub = SubResourceHeader().Match(line);
                section = sub.Success ? sub.Groups[1].Value : line == "[resource]" ? string.Empty : null;
                if (sub.Success)
                {
                    fonts[section!] = new Dictionary<string, string>();
                }
                continue;
            }
            var separator = line.IndexOf(" = ", StringComparison.Ordinal);
            if (section is null || separator < 0)
            {
                continue;
            }
            var key = line[..separator];
            var value = line[(separator + 3)..];
            if (section.Length == 0)
            {
                items[key] = value;
            }
            else
            {
                fonts[section][key] = value;
            }
        }
        return new ThemeFile(items, fonts, extPaths);
    }

    private static Color ParseColor(string value)
    {
        var match = ColorLiteral().Match(value);
        match.Success.ShouldBeTrue($"Not a color literal: {value}");
        var parts = match.Groups[1].Value.Split(',').Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
        return new Color(parts[0], parts[1], parts[2], parts[3]);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NodeRunner.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the Node Runner repository root.");
    }

    [GeneratedRegex("""\[ext_resource [^\]]*path="(?<path>[^"]+)" id="(?<id>[^"]+)"\]""")]
    private static partial Regex ExtResource();

    [GeneratedRegex("""^\[sub_resource type="FontVariation" id="([^"]+)"\]$""")]
    private static partial Regex SubResourceHeader();

    [GeneratedRegex("""SubResource\("([^"]+)"\)""")]
    private static partial Regex SubResourceReference();

    [GeneratedRegex("""ExtResource\("([^"]+)"\)""")]
    private static partial Regex ExtResourceReference();

    [GeneratedRegex("""^Color\(([^)]*)\)$""")]
    private static partial Regex ColorLiteral();
}
