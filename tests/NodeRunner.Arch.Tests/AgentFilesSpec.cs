namespace NodeRunner.Arch.Tests;

/// <summary>
/// Agent files load in both Copilot CLI and Claude Code: docs/REVIEW.md → "Agent, rule and skill files"
/// (#1334). The fixtures live in a temp directory, so neither tool loads them as agents.
/// </summary>
public sealed class AgentFilesSpec : IDisposable
{
    private const string _reviewer = "fixture-reviewer";
    private const string _lead = "fixture-lead";

    private readonly string _root = Directory.CreateTempSubdirectory("agent-files-").FullName;

    public AgentFilesSpec()
    {
        Directory.CreateDirectory(AgentsDirectory);
        WriteAgent(_reviewer, $"""
            name: {_reviewer}
            description: Reviews a fixture diff — one focus, read-only.
            tools: {AgentFiles.ReviewerTools}
            {AgentFiles.ReviewerHook}
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

    private string AgentsDirectory => Path.Combine(_root, ".claude", "agents");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Repository_AgentFilesFollowTheRules()
    {
        var problems = AgentFiles.Problems(RepositoryRoot.Find());

        problems.ShouldBeEmpty("See docs/REVIEW.md → \"Agent, rule and skill files\".");
    }

    [Fact]
    public void Fixture_WithEveryRuleKept_HasNoProblems()
    {
        var problems = AgentFiles.Problems(_root);

        problems.ShouldBeEmpty();
    }

    [Fact]
    public void Fixture_WithLocalAgents_SkipsThem()
    {
        File.WriteAllText(Path.Combine(AgentsDirectory, "local_device.md"), "No frontmatter.\n");

        var problems = AgentFiles.Problems(_root);

        problems.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("old agents folder", ".github/agents: remove it")]
    [InlineData("no agents folder", ".claude/agents: missing")]
    [InlineData("wrong suffix", ".claude/agents/notes.txt: holds only agent files")]
    [InlineData("folder in agents", ".claude/agents/old.md: holds only agent files")]
    [InlineData("near-miss local name", ".claude/agents/localnotes.md: no frontmatter")]
    [InlineData("no frontmatter", $".claude/agents/{_reviewer}.md: no frontmatter")]
    [InlineData("no name", $".claude/agents/{_reviewer}.md: name must be {_reviewer}")]
    [InlineData("wrong name", $".claude/agents/{_reviewer}.md: name must be {_reviewer}")]
    [InlineData("no description", $".claude/agents/{_reviewer}.md: needs a one-line description")]
    [InlineData("colon in description", $".claude/agents/{_reviewer}.md: description has an unquoted")]
    [InlineData("folded description", $".claude/agents/{_reviewer}.md: description must be one line")]
    [InlineData("duplicate key", $".claude/agents/{_reviewer}.md: sets description twice")]
    [InlineData("model", $".claude/agents/{_reviewer}.md: sets model")]
    [InlineData("reviewer tools", $".claude/agents/{_reviewer}.md: a reviewer has tools")]
    [InlineData("no tools", $".claude/agents/{_reviewer}.md: a reviewer has tools")]
    [InlineData("no reviewer hook", $".claude/agents/{_reviewer}.md: a reviewer registers reviewer-bash.sh")]
    [InlineData("other reviewer hook", $".claude/agents/{_reviewer}.md: a reviewer registers reviewer-bash.sh")]
    [InlineData("other reviewer LSP hook", $".claude/agents/{_reviewer}.md: a reviewer registers reviewer-bash.sh and reviewer-lsp.sh")]
    [InlineData("empty hooks", $".claude/agents/{_lead}.md: hooks must use Claude Code's shape")]
    [InlineData("flat hooks", $".claude/agents/{_lead}.md: hooks must use Claude Code's shape")]
    public void Fixture_WithOneRuleBroken_NamesTheFileAndRule(string broken, string expected)
    {
        Break(broken);

        var problems = AgentFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith(expected);
    }

    private void Break(string broken)
    {
        var agent = Path.Combine(AgentsDirectory, _reviewer + ".md");
        var lead = Path.Combine(AgentsDirectory, _lead + ".md");
        switch (broken)
        {
            case "old agents folder":
                Directory.CreateDirectory(Path.Combine(_root, ".github", "agents"));
                break;
            case "no agents folder":
                Directory.Delete(AgentsDirectory, recursive: true);
                break;
            case "wrong suffix":
                File.WriteAllText(Path.Combine(AgentsDirectory, "notes.txt"), "Notes.\n");
                break;
            case "folder in agents":
                Directory.CreateDirectory(Path.Combine(AgentsDirectory, "old.md"));
                break;
            case "near-miss local name":
                File.WriteAllText(Path.Combine(AgentsDirectory, "localnotes.md"), "Notes.\n");
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
            case "no reviewer hook":
                Replace(agent, "\n" + AgentFiles.ReviewerHook.ReplaceLineEndings("\n"), "");
                break;
            case "other reviewer hook":
                Replace(agent, AgentFiles.ReviewerHookScript, "guard-bash.sh");
                break;
            case "other reviewer LSP hook":
                Replace(agent, AgentFiles.ReviewerLspHookScript, "guard-edit.sh");
                break;
            case "empty hooks":
                Replace(lead, "\n    Stop:\n    - hooks:\n        - type: command\n          command: \"./done.sh\"", "");
                break;
            case "flat hooks":
                Replace(lead, "    - hooks:\n        - type: command\n          command:", "    - type: command\n      bash:");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(broken), broken, "No such fixture break.");
        }
    }

    private void WriteAgent(string name, string frontmatter)
    {
        File.WriteAllText(Path.Combine(AgentsDirectory, name + ".md"),
            $"---\n{frontmatter.ReplaceLineEndings("\n")}\n---\n\nYou are {name}.\n");
    }

    private static void Replace(string path, string from, string to)
    {
        var text = File.ReadAllText(path);
        text.ShouldContain(from);
        File.WriteAllText(path, text.Replace(from, to, StringComparison.Ordinal));
    }
}
