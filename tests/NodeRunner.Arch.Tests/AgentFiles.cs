namespace NodeRunner.Arch.Tests;

/// <summary>
/// Checks the agent-file rules in docs/REVIEW.md → "Agent, rule and skill files": a broken one silently
/// removes an agent from Copilot CLI or Claude Code. Reads only the frontmatter keys
/// these files use; it is not a YAML parser.
/// </summary>
internal static class AgentFiles
{
    internal const string ReviewerTools = "Read, Grep, Glob, Bash, LSP";

    /// <summary>The agent-scoped hook that keeps a reviewer's Bash to reading commands.</summary>
    internal const string ReviewerHookScript = "reviewer-bash.sh";

    /// <summary>The agent-scoped hook that keeps a reviewer's LSP tool to lookups.</summary>
    internal const string ReviewerLspHookScript = "reviewer-lsp.sh";

    /// <summary>
    /// The frontmatter each reviewer registers its hooks with, in Claude Code's shape. The LSP tool
    /// is <c>LSP</c> in Claude Code and <c>lsp</c> in Copilot, whose matchers are case-sensitive.
    /// </summary>
    internal const string ReviewerHook = $$"""
        hooks:
          PreToolUse:
            - matcher: "Bash"
              hooks:
                - type: command
                  command: '"${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"/.claude/hooks/{{ReviewerHookScript}}'
            - matcher: "LSP|lsp"
              hooks:
                - type: command
                  command: '"${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"/.claude/hooks/{{ReviewerLspHookScript}}'
        """;

    /// <summary>Git-ignored machine-local agents and rules: AGENTS.md → "Machine-local setup".</summary>
    internal const string LocalPrefix = "local_";

    /// <summary>
    /// Entries the checks skip: machine-local files, and dot-files such as Finder's git-ignored
    /// <c>.DS_Store</c>, which CI never sees.
    /// </summary>
    internal static bool IsSkipped(string name) =>
        name.StartsWith(LocalPrefix, StringComparison.Ordinal) || name.StartsWith('.');

    private static readonly string[] _allowedKeys = ["name", "description", "tools", "hooks", "model"];

    private static readonly string[] _models = ["sonnet", "opus", "haiku"];

    /// <summary>Every broken rule under <paramref name="root"/>, each naming its file.</summary>
    internal static List<string> Problems(string root)
    {
        var problems = new List<string>();
        var agents = Path.Combine(root, ".claude", "agents");
        if (Directory.Exists(Path.Combine(root, ".github", "agents")))
        {
            // Copilot would load it first, and Claude Code not at all.
            problems.Add(".github/agents: remove it; every agent lives in .claude/agents (#1359).");
        }

        if (!Directory.Exists(agents))
        {
            problems.Add(".claude/agents: missing; the review agents live there.");
            return problems;
        }

        foreach (var path in Directory.EnumerateFileSystemEntries(agents).Order(StringComparer.Ordinal))
        {
            var file = Path.GetFileName(path);
            if (IsSkipped(file))
            {
                continue;
            }

            if (!file.EndsWith(".md", StringComparison.Ordinal) || Directory.Exists(path))
            {
                problems.Add($".claude/agents/{file}: holds only agent files ending in .md.");
                continue;
            }

            CheckFrontmatter($".claude/agents/{file}", file[..^".md".Length], File.ReadAllLines(path), problems);
        }

        return problems;
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

        if (values.TryGetValue("model", out var model) && !_models.Contains(model))
        {
            problems.Add($"{shown}: model must be one of {string.Join(", ", _models)}.");
        }

        if (name.EndsWith("-reviewer", StringComparison.Ordinal)
            && (!values.TryGetValue("tools", out var tools) || tools != ReviewerTools))
        {
            problems.Add($"{shown}: a reviewer has tools: {ReviewerTools}.");
        }

        if (name.EndsWith("-reviewer", StringComparison.Ordinal)
            && !string.Join('\n', lines[1..end]).Contains(ReviewerHook.ReplaceLineEndings("\n"), StringComparison.Ordinal))
        {
            problems.Add($"{shown}: a reviewer registers {ReviewerHookScript} and {ReviewerLspHookScript} with the hooks block in docs/REVIEW.md → \"Agent, rule and skill files\".");
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
    /// command in the entry itself, and makes Claude Code drop the whole agent.
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
