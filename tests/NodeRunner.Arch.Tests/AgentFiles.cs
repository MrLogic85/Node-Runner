namespace NodeRunner.Arch.Tests;

/// <summary>
/// Checks the agent-file rules in docs/REVIEW.md → "Agent, rule and skill files": a broken one silently
/// removes an agent from Copilot CLI or Claude Code (#1314, #1334). Reads only the frontmatter keys
/// these files use; it is not a YAML parser.
/// </summary>
internal static class AgentFiles
{
    internal const string ReviewerTools = "Read, Grep, Glob, Bash";

    private const string _agentSuffix = ".agent.md";

    /// <summary>Git-ignored machine-local agents and rules: AGENTS.md → "Machine-local setup" (#1342).</summary>
    internal const string LocalPrefix = "local_";
    private static readonly string[] _allowedKeys = ["name", "description", "tools", "hooks"];

    /// <summary>Every broken rule under <paramref name="root"/>, each naming its file.</summary>
    internal static List<string> Problems(string root)
    {
        var problems = new List<string>();
        var agents = Path.Combine(root, ".github", "agents");
        var links = Path.Combine(root, ".claude", "agents");
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (!Directory.Exists(agents))
        {
            return [".github/agents: missing; the review agents live there."];
        }

        foreach (var path in Directory.EnumerateFileSystemEntries(agents).Order(StringComparer.Ordinal))
        {
            var file = Path.GetFileName(path);
            if (file.StartsWith(LocalPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (!file.EndsWith(_agentSuffix, StringComparison.Ordinal) || Directory.Exists(path))
            {
                problems.Add($".github/agents/{file}: holds only agent files ending in {_agentSuffix}.");
                continue;
            }

            var name = file[..^_agentSuffix.Length];
            names.Add(name);
            CheckFrontmatter($".github/agents/{file}", name, File.ReadAllLines(path), problems);
            CheckLink(links, name, problems);
        }

        if (Directory.Exists(links))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(links).Order(StringComparer.Ordinal))
            {
                var file = Path.GetFileName(path);
                if (!file.StartsWith(LocalPrefix, StringComparison.Ordinal)
                    && (!file.EndsWith(".md", StringComparison.Ordinal) || !names.Contains(file[..^".md".Length])))
                {
                    problems.Add($".claude/agents/{file}: no matching .github/agents/*{_agentSuffix}.");
                }
            }
        }

        return problems;
    }

    private static void CheckLink(string links, string name, List<string> problems)
    {
        var link = new FileInfo(Path.Combine(links, name + ".md"));
        var shown = $".claude/agents/{name}.md";
        var expected = $"../../.github/agents/{name}{_agentSuffix}";

        if (link.LinkTarget is { } target)
        {
            if (target != expected)
            {
                problems.Add($"{shown}: links to {target}, not the relative {expected}.");
            }
        }
        else
        {
            problems.Add(link.Exists
                ? $"{shown}: a copy, not a symlink to {expected}."
                : $"{shown}: missing; add a symlink to {expected}.");
        }
    }

    /// <summary>The index of the line closing the frontmatter, or -1 when the file has none.</summary>
    internal static int FrontmatterEnd(string[] lines) =>
        lines.Length > 0 && lines[0] == "---" ? Array.IndexOf(lines, "---", 1) : -1;

    private static void CheckFrontmatter(string shown, string name, string[] lines, List<string> problems)
    {
        var end = FrontmatterEnd(lines);
        if (end < 0)
        {
            problems.Add($"{shown}: no frontmatter between two --- lines.");
            return;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var hooks = new List<string>();
        string? current = null;
        foreach (var line in lines[1..end].Where(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith('#')))
        {
            if (line[0] != ' ' && line[0] != '-')
            {
                var colon = line.IndexOf(':', StringComparison.Ordinal);
                current = colon > 0 ? line[..colon] : line;
                var value = colon > 0 ? line[(colon + 1)..].Trim() : "";
                if (!values.TryAdd(current, value))
                {
                    problems.Add($"{shown}: sets {current} twice.");
                }

                CheckScalar(shown, current, value, problems);
            }
            else if (current == "hooks")
            {
                hooks.Add(line);
            }
            else
            {
                problems.Add($"{shown}: {current} must be one line.");
            }
        }

        foreach (var key in values.Keys.Where(key => !_allowedKeys.Contains(key)))
        {
            problems.Add($"{shown}: sets {key}; agent frontmatter has only {string.Join(", ", _allowedKeys)}.");
        }

        if (!values.TryGetValue("name", out var actual) || actual != name)
        {
            problems.Add($"{shown}: name must be {name}, the file name.");
        }

        if (!values.TryGetValue("description", out var description) || description.Length == 0)
        {
            problems.Add($"{shown}: needs a one-line description.");
        }

        if (name.EndsWith("-reviewer", StringComparison.Ordinal)
            && (!values.TryGetValue("tools", out var tools) || tools != ReviewerTools))
        {
            problems.Add($"{shown}: a reviewer has tools: {ReviewerTools}.");
        }

        if (values.ContainsKey("hooks"))
        {
            CheckHooks(shown, hooks, problems);
        }
    }

    /// <summary>An unquoted <c>: </c> or <c> #</c> makes the line invalid YAML or cuts the value short.</summary>
    internal static void CheckScalar(string shown, string key, string value, List<string> problems)
    {
        var quoted = value.Length >= 2 && value[0] is '"' or '\'' && value[^1] == value[0];
        if (!quoted && (value.Contains(": ", StringComparison.Ordinal) || value.Contains(" #", StringComparison.Ordinal)))
        {
            problems.Add($"{shown}: {key} has an unquoted \": \" or \" #\"; reword it, for example with \" — \".");
        }
    }

    /// <summary>
    /// Claude Code's shape: each event lists entries that hold an inner <c>hooks</c> list, with a
    /// <c>matcher</c> where the event takes one. Copilot's flat shape puts <c>type</c> and the
    /// command in the entry itself, and makes Claude Code drop the whole agent (#1314).
    /// </summary>
    private static void CheckHooks(string shown, List<string> lines, List<string> problems)
    {
        var items = lines.Where(line => line.TrimStart().StartsWith("- ", StringComparison.Ordinal)).ToList();
        var entryIndent = items.Count == 0 ? -1 : items.Min(Indent);
        var entries = new List<List<string>>();
        var keyIndent = -1;
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (Indent(line) == entryIndent && trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                var first = trimmed[1..].TrimStart();
                keyIndent = line.Length - first.Length;
                entries.Add([Key(first)]);
            }
            else if (Indent(line) == keyIndent)
            {
                entries[^1].Add(Key(trimmed));
            }
        }

        if (entries.Count == 0 || entries.Any(entry => !entry.Contains("hooks")))
        {
            problems.Add($"{shown}: hooks must use Claude Code's shape, entries with an inner hooks list.");
        }

        static int Indent(string line) => line.Length - line.TrimStart().Length;

        static string Key(string line) => line.Split(':')[0];
    }
}
