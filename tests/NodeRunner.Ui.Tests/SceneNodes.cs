using System.Text.RegularExpressions;

namespace NodeRunner.Ui.Tests;

/// <summary>Reads authored <c>.tscn</c> node blocks as text; no Godot runtime involved.</summary>
internal static partial class SceneNodes
{
    public sealed record Node(string Scene, string Header, string Body)
    {
        public override string ToString() => $"{Scene}: {Header}";
    }

    /// <summary>Saved theme overrides that pin a palette color or text style on one node.</summary>
    public static IReadOnlyList<string> PaletteOverrideGroups { get; } =
    [
        "theme_override_colors/",
        "theme_override_fonts/",
        "theme_override_font_sizes/",
    ];

    public static IEnumerable<Node> WithScript(string scriptFileName)
    {
        var scenes = Path.Combine(FindRepositoryRoot(), "project", "scenes");
        foreach (var path in Directory.EnumerateFiles(scenes, "*.tscn", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            var scriptIds = ExtResource().Matches(text)
                .Where(match => match.Groups["path"].Value.EndsWith("/" + scriptFileName, StringComparison.Ordinal))
                .Select(match => match.Groups["id"].Value)
                .ToHashSet();
            foreach (var block in NodeBlock().Split(text).Where(block => block.StartsWith("[node ", StringComparison.Ordinal)))
            {
                var script = ScriptRef().Match(block);
                if (script.Success && scriptIds.Contains(script.Groups["id"].Value))
                {
                    yield return new Node(Path.GetFileName(path), block[..block.IndexOf('\n')], block);
                }
            }
        }
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

        throw new DirectoryNotFoundException("Could not find NodeRunner.slnx.");
    }

    [GeneratedRegex("""\[ext_resource[^\]]*path="(?<path>[^"]+)"[^\]]*id="(?<id>[^"]+)"\]""")]
    private static partial Regex ExtResource();

    [GeneratedRegex(@"(?m)^(?=\[node )")]
    private static partial Regex NodeBlock();

    [GeneratedRegex("""script = ExtResource\("(?<id>[^"]+)"\)""")]
    private static partial Regex ScriptRef();
}
