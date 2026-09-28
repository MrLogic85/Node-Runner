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

    /// <summary>A node block with the resources its header and body point to.</summary>
    public sealed record SceneNode(Node Node, string? Script, string? Instance)
    {
        public string Name => Attribute("name") ?? string.Empty;

        /// <summary>The native type; absent on an instanced scene or a node it already owns.</summary>
        public string? Type => Attribute("type");

        public bool IsRoot => Attribute("parent") is null;

        public bool IsUnique => Node.Body.Contains("\nunique_name_in_owner = true", StringComparison.Ordinal);

        private string? Attribute(string name)
        {
            var match = HeaderAttribute().Matches(Node.Header).FirstOrDefault(match => match.Groups["name"].Value == name);
            return match?.Groups["value"].Value;
        }
    }

    public static IEnumerable<Node> All() => Read().Select(entry => entry.Node);

    /// <summary>The nodes of one scene, by its path under <c>project/scenes</c>.</summary>
    public static IReadOnlyList<SceneNode> InScene(string scenePath)
    {
        var path = Path.Combine(FindRepositoryRoot(), "project", "scenes", scenePath);
        return Parse(Path.GetFileName(path), File.ReadAllText(path)).ToList();
    }

    public static IEnumerable<Node> WithScript(string scriptFileName) =>
        Read().Where(entry => entry.Script?.EndsWith("/" + scriptFileName, StringComparison.Ordinal) == true)
            .Select(entry => entry.Node);

    /// <summary>Every authored scene file, by file name, with its full text.</summary>
    public static IEnumerable<(string Scene, string Text)> Files() =>
        Directory.EnumerateFiles(Path.Combine(FindRepositoryRoot(), "project", "scenes"), "*.tscn", SearchOption.AllDirectories)
            .Select(path => (Path.GetFileName(path), File.ReadAllText(path)));

    private static IEnumerable<(Node Node, string? Script)> Read() =>
        Files().SelectMany(file => Parse(file.Scene, file.Text)).Select(node => (node.Node, node.Script));

    private static IEnumerable<SceneNode> Parse(string scene, string text)
    {
        var resourcePaths = ExtResource().Matches(text)
            .ToDictionary(match => match.Groups["id"].Value, match => match.Groups["path"].Value);
        foreach (var block in NodeBlock().Split(text).Where(block => block.StartsWith("[node ", StringComparison.Ordinal)))
        {
            var header = block[..block.IndexOf('\n')];
            yield return new SceneNode(
                new Node(scene, header, block),
                Resource(ScriptRef().Match(block)),
                Resource(InstanceRef().Match(header)));
        }

        string? Resource(Match reference) =>
            reference.Success ? resourcePaths.GetValueOrDefault(reference.Groups["id"].Value) : null;
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

    [GeneratedRegex("""instance=ExtResource\("(?<id>[^"]+)"\)""")]
    private static partial Regex InstanceRef();

    [GeneratedRegex(@"(?<name>\w+)=""(?<value>[^""]*)""")]
    private static partial Regex HeaderAttribute();
}
