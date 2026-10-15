namespace NodeRunner.Arch.Tests;

/// <summary>
/// Agent files load in both Copilot CLI and Claude Code: docs/REVIEW.md → "Agent files" (#1334).
/// The fixtures live in a temp directory, so neither tool loads them as agents.
/// </summary>
public sealed class AgentFilesSpec : IDisposable
{
    private const string _reviewer = "fixture-reviewer";
    private const string _lead = "fixture-lead";

    private readonly string _root = Directory.CreateTempSubdirectory("agent-files-").FullName;

    public AgentFilesSpec()
    {
        Directory.CreateDirectory(AgentsDirectory);
        Directory.CreateDirectory(LinksDirectory);
        WriteAgent(_reviewer, $"""
            name: {_reviewer}
            description: Reviews a fixture diff — one focus, read-only.
            tools: {AgentFiles.ReviewerTools}
            hooks:
              PreToolUse:
                - matcher: "Bash"
                  hooks:
                    - type: command
                      command: "./deny.sh"
            """);
        // Valid YAML the checks must not flag: a comment, a quoted ": " and " #", and hooks
        // indented by four with an entry that takes no matcher.
        WriteAgent(_lead, $"""
            # A comment line.
            name: {_lead}
            description: "Proposes fixture designs: layouts # and colours."
            tools: Read
            hooks:
                Stop:
                - hooks:
                    - type: command
                      command: "./done.sh"
            """);
    }

    private string AgentsDirectory => Path.Combine(_root, ".github", "agents");

    private string LinksDirectory => Path.Combine(_root, ".claude", "agents");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Repository_AgentFilesFollowTheRules()
    {
        var problems = AgentFiles.Problems(RepositoryRoot.Find());

        problems.ShouldBeEmpty("See docs/REVIEW.md → \"Agent files\".");
    }

    [Fact]
    public void Fixture_WithEveryRuleKept_HasNoProblems()
    {
        var problems = AgentFiles.Problems(_root);

        problems.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("missing link", $".claude/agents/{_reviewer}.md: missing")]
    [InlineData("copied file", $".claude/agents/{_reviewer}.md: a copy, not a symlink")]
    [InlineData("absolute link", $".claude/agents/{_reviewer}.md: links to /")]
    [InlineData("stray link", ".claude/agents/old-reviewer.md: no matching")]
    [InlineData("stray file in links", ".claude/agents/notes.txt: no matching")]
    [InlineData("no agents folder", ".github/agents: missing")]
    [InlineData("wrong suffix", ".github/agents/notes.md: holds only agent files")]
    [InlineData("folder in agents", ".github/agents/old.agent.md: holds only agent files")]
    [InlineData("no frontmatter", $".github/agents/{_reviewer}.agent.md: no frontmatter")]
    [InlineData("no name", $".github/agents/{_reviewer}.agent.md: name must be {_reviewer}")]
    [InlineData("wrong name", $".github/agents/{_reviewer}.agent.md: name must be {_reviewer}")]
    [InlineData("no description", $".github/agents/{_reviewer}.agent.md: needs a one-line description")]
    [InlineData("colon in description", $".github/agents/{_reviewer}.agent.md: description has an unquoted")]
    [InlineData("folded description", $".github/agents/{_reviewer}.agent.md: description must be one line")]
    [InlineData("duplicate key", $".github/agents/{_reviewer}.agent.md: sets description twice")]
    [InlineData("model", $".github/agents/{_reviewer}.agent.md: sets model")]
    [InlineData("reviewer tools", $".github/agents/{_reviewer}.agent.md: a reviewer has tools")]
    [InlineData("no tools", $".github/agents/{_reviewer}.agent.md: a reviewer has tools")]
    [InlineData("empty hooks", $".github/agents/{_reviewer}.agent.md: hooks must use Claude Code's shape")]
    [InlineData("flat hooks", $".github/agents/{_reviewer}.agent.md: hooks must use Claude Code's shape")]
    public void Fixture_WithOneRuleBroken_NamesTheFileAndRule(string broken, string expected)
    {
        Break(broken);

        var problems = AgentFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith(expected);
    }

    private void Break(string broken)
    {
        var link = Path.Combine(LinksDirectory, _reviewer + ".md");
        var agent = Path.Combine(AgentsDirectory, _reviewer + ".agent.md");
        switch (broken)
        {
            case "missing link":
                File.Delete(link);
                break;
            case "copied file":
                File.Delete(link);
                File.Copy(agent, link);
                break;
            case "absolute link":
                File.Delete(link);
                File.CreateSymbolicLink(link, agent);
                break;
            case "stray link":
                File.CreateSymbolicLink(Path.Combine(LinksDirectory, "old-reviewer.md"), "../../.github/agents/old-reviewer.agent.md");
                break;
            case "stray file in links":
                File.WriteAllText(Path.Combine(LinksDirectory, "notes.txt"), "Notes.\n");
                break;
            case "no agents folder":
                Directory.Delete(AgentsDirectory, recursive: true);
                break;
            case "folder in agents":
                Directory.CreateDirectory(Path.Combine(AgentsDirectory, "old.agent.md"));
                break;
            case "wrong suffix":
                File.WriteAllText(Path.Combine(AgentsDirectory, "notes.md"), "Notes.\n");
                break;
            case "no frontmatter":
                File.WriteAllText(agent, "You review fixtures.\n");
                break;
            case "no name":
                Replace(agent, $"name: {_reviewer}\n", "");
                break;
            case "wrong name":
                Replace(agent, $"name: {_reviewer}", "name: other-reviewer");
                break;
            case "no description":
                Replace(agent, "description: Reviews a fixture diff — one focus, read-only.", "description:");
                break;
            case "colon in description":
                Replace(agent, "diff — one focus", "diff: one focus");
                break;
            case "folded description":
                Replace(agent, "description: Reviews", "description: >\n  Reviews");
                break;
            case "duplicate key":
                Replace(agent, "tools:", "description: Again.\ntools:");
                break;
            case "model":
                Replace(agent, "tools:", "model: gpt-5\ntools:");
                break;
            case "reviewer tools":
                Replace(agent, AgentFiles.ReviewerTools, "Read, Bash");
                break;
            case "no tools":
                Replace(agent, $"tools: {AgentFiles.ReviewerTools}\n", "");
                break;
            case "empty hooks":
                Replace(agent, "\n  PreToolUse:\n    - matcher: \"Bash\"\n      hooks:\n        - type: command\n          command: \"./deny.sh\"", "");
                break;
            case "flat hooks":
                Replace(agent, "    - matcher: \"Bash\"\n      hooks:\n        - type: command\n          command:",
                    "    - type: command\n      bash:");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(broken), broken, "No such fixture break.");
        }
    }

    private void WriteAgent(string name, string frontmatter)
    {
        File.WriteAllText(Path.Combine(AgentsDirectory, name + ".agent.md"),
            $"---\n{frontmatter.ReplaceLineEndings("\n")}\n---\n\nYou are {name}.\n");
        File.CreateSymbolicLink(Path.Combine(LinksDirectory, name + ".md"), $"../../.github/agents/{name}.agent.md");
    }

    private static void Replace(string path, string from, string to)
    {
        var text = File.ReadAllText(path);
        text.ShouldContain(from);
        File.WriteAllText(path, text.Replace(from, to, StringComparison.Ordinal));
    }
}
