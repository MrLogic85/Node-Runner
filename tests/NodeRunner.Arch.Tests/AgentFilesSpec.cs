namespace NodeRunner.Arch.Tests;

/// <summary>
/// Agent files load in both Copilot CLI and Claude Code: docs/REVIEW.md → "Agent, rule and skill files".
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
            model: haiku
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
    public void Fixture_WithEveryRuleKept_HasNoProblemsAndSkipsLocalAndDotFiles()
    {
        File.WriteAllText(Path.Combine(AgentsDirectory, "local_device.md"), "No frontmatter.\n");
        File.WriteAllText(Path.Combine(AgentsDirectory, ".DS_Store"), "");

        var problems = AgentFiles.Problems(_root);

        problems.ShouldBeEmpty();
    }

    [Fact]
    public void Fixture_WithAgentsOutsideTheirFolder_NamesEach()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".github", "agents"));
        File.WriteAllText(Path.Combine(AgentsDirectory, "notes.txt"), "Notes.\n");
        Directory.CreateDirectory(Path.Combine(AgentsDirectory, "old.md"));
        File.WriteAllText(Path.Combine(AgentsDirectory, "localnotes.md"), "Notes.\n");

        var problems = AgentFiles.Problems(_root);

        problems.Count.ShouldBe(4);
        problems[0].ShouldStartWith(".github/agents: remove it");
        problems[1].ShouldStartWith(".claude/agents/localnotes.md: no frontmatter");
        problems[2].ShouldStartWith(".claude/agents/notes.txt: holds only agent files");
        problems[3].ShouldStartWith(".claude/agents/old.md: holds only agent files");
    }

    [Fact]
    public void Fixture_WithoutTheAgentsFolder_NamesIt()
    {
        Directory.Delete(AgentsDirectory, recursive: true);

        var problems = AgentFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith(".claude/agents: missing");
    }

    [Theory]
    [InlineData(_reviewer, "---\n", "", "no frontmatter")]
    [InlineData(_reviewer, $"name: {_reviewer}", "name: other-reviewer", $"name must be {_reviewer}")]
    [InlineData(_reviewer, "description: Reviews a fixture diff — one focus, read-only.", "description:", "needs a one-line description")]
    [InlineData(_reviewer, "diff — one focus", "diff: one focus", "description has an unquoted")]
    [InlineData(_reviewer, "description: Reviews", "description: >\n  Reviews", "description must be one line")]
    [InlineData(_reviewer, "tools:", "description: Again.\ntools:", "sets description twice")]
    [InlineData(_reviewer, "tools:", "model: claude-haiku-4.5\ntools:", "model must be haiku")]
    [InlineData(_reviewer, "tools:", "color: blue\ntools:", "sets color")]
    [InlineData(_reviewer, AgentFiles.ReviewerTools, "Read, Bash", "a reviewer has tools")]
    [InlineData(_reviewer, AgentFiles.ReviewerLspHookScript, "guard-edit.sh", "a reviewer registers reviewer-bash.sh and reviewer-lsp.sh")]
    [InlineData(_lead, "\n    Stop:\n    - hooks:\n        - type: command\n          command: \"./done.sh\"", "", "hooks must use Claude Code's shape")]
    [InlineData(_lead, "    - hooks:\n        - type: command\n          command:", "    - type: command\n      bash:", "hooks must use Claude Code's shape")]
    public void Fixture_WithOneRuleBroken_NamesTheFileAndRule(string agent, string from, string to, string expected)
    {
        var path = Path.Combine(AgentsDirectory, agent + ".md");
        var text = File.ReadAllText(path);
        text.ShouldContain(from);
        File.WriteAllText(path, text.Replace(from, to, StringComparison.Ordinal));

        var problems = AgentFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith($".claude/agents/{agent}.md: {expected}");
    }

    private void WriteAgent(string name, string frontmatter)
    {
        File.WriteAllText(Path.Combine(AgentsDirectory, name + ".md"),
            $"---\n{frontmatter.ReplaceLineEndings("\n")}\n---\n\nYou are {name}.\n");
    }
}
