using System.Text.RegularExpressions;

namespace NodeRunner.Ui.Tests;

/// <summary>Reads authored <c>.tscn</c> node blocks as text; no Godot runtime involved.</summary>
internal static partial class SceneNodes
{
    public sealed record Node(string Scene, string Header, string Body)
    {
        public override string ToString() => $"{Scene}: {Header}";
    }

    /// <summary>Saved theme overrides that pin a stylebox, palette color or text style on one node.</summary>
    public static IReadOnlyList<string> StyleOverrideGroups { get; } =
    [
        "theme_override_styles/",
        "theme_override_colors/",
        "theme_override_fonts/",
        "theme_override_font_sizes/",
    ];

    public static IEnumerable<Node> All() => Read().Select(entry => entry.Node);

    public static IEnumerable<Node> WithScript(string scriptFileName) =>
        Read().Where(entry => entry.Script?.EndsWith("/" + scriptFileName, StringComparison.Ordinal) == true)
            .Select(entry => entry.Node);

    /// <summary>Every authored scene file, by file name, with its full text.</summary>
    public static IEnumerable<(string Scene, string Text)> Files() =>
        Directory.EnumerateFiles(Path.Combine(FindRepositoryRoot(), "project", "scenes"), "*.tscn", SearchOption.AllDirectories)
            .Select(path => (Path.GetFileName(path), File.ReadAllText(path)));

    private static IEnumerable<(Node Node, string? Script)> Read()
    {
        foreach (var (scene, text) in Files())
        {
            var scriptPaths = ExtResource().Matches(text)
                .ToDictionary(match => match.Groups["id"].Value, match => match.Groups["path"].Value);
            foreach (var block in NodeBlock().Split(text).Where(block => block.StartsWith("[node ", StringComparison.Ordinal)))
            {
                var script = ScriptRef().Match(block);
                var scriptPath = script.Success ? scriptPaths.GetValueOrDefault(script.Groups["id"].Value) : null;
                yield return (new Node(scene, block[..block.IndexOf('\n')], block), scriptPath);
            }
        }
    }

    internal static string FindRepositoryRoot()
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
