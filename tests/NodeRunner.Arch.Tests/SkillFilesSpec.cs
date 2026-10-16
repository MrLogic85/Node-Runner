namespace NodeRunner.Arch.Tests;

/// <summary>
/// Skills load in both Copilot CLI and Claude Code: docs/REVIEW.md → "Agent, rule and skill files"
/// (#1317). The fixtures live in a temp directory, so neither tool loads them as skills.
/// </summary>
public sealed class SkillFilesSpec : IDisposable
{
    private const string _name = "fixture-skill";

    // The local_ in the folder name proves only paths inside the repository count as local.
    private readonly string _root = Directory.CreateTempSubdirectory("local_skill-files-").FullName;

    public SkillFilesSpec()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".claude", "skills", _name));
        File.WriteAllText(Skill, $"---\n# A comment line.\nname: {_name}\ndescription: \"Does fixtures: use # when asked.\"\n---\n\n# Fixture\n");
    }

    private string Skill => Path.Combine(_root, ".claude", "skills", _name, "SKILL.md");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Repository_SkillFilesFollowTheRules()
    {
        var problems = SkillFiles.Problems(RepositoryRoot.Find());

        problems.ShouldBeEmpty("See docs/REVIEW.md → \"Agent, rule and skill files\".");
    }

    [Fact]
    public void Fixture_WithEveryRuleKept_HasNoProblems()
    {
        var problems = SkillFiles.Problems(_root);

        problems.ShouldBeEmpty();
    }

    [Fact]
    public void Fixture_WithALocalSkill_SkipsIt()
    {
        var local = Path.Combine(_root, ".claude", "skills", "local_device");
        Directory.CreateDirectory(local);
        File.WriteAllText(Path.Combine(local, "SKILL.md"), "No frontmatter.\n");

        var problems = SkillFiles.Problems(_root);

        problems.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("# Fixture\n", "no frontmatter")]
    [InlineData($"---\nname: other\ndescription: Does fixtures.\n---\n", "name must be fixture-skill")]
    [InlineData($"---\ndescription: Does fixtures.\n---\n", "name must be fixture-skill")]
    [InlineData($"---\nname: {_name}\n---\n", "needs a one-line description")]
    [InlineData($"---\nname: {_name}\ndescription: >\n  Does fixtures.\n---\n", "description must be one line")]
    [InlineData($"---\nname: {_name}\ndescription: Does fixtures: when asked.\n---\n", "description has an unquoted")]
    [InlineData($"---\nname: {_name}\ndescription: Does fixtures.\nallowed-tools: Bash\n---\n", "sets allowed-tools")]
    [InlineData($"---\nname: {_name}\nname: {_name}\ndescription: Does fixtures.\n---\n", "sets name twice")]
    public void Fixture_WithABrokenSkill_NamesTheFileAndRule(string text, string expected)
    {
        File.WriteAllText(Skill, text);

        var problems = SkillFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith($".claude/skills/{_name}/SKILL.md: {expected}");
    }

    [Fact]
    public void Fixture_WithATooLongDescription_NamesIt()
    {
        File.WriteAllText(Skill, $"---\nname: {_name}\ndescription: {new string('a', 1025)}\n---\n");

        var problems = SkillFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldEndWith("description is over 1024 characters.");
    }

    [Theory]
    [InlineData("Fixture_Skill", "name the folder in lowercase")]
    [InlineData("fixture--skill", "name the folder in lowercase")]
    public void Fixture_WithABadFolderName_NamesIt(string folder, string expected)
    {
        var path = Path.Combine(_root, ".claude", "skills", folder);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "SKILL.md"), $"---\nname: {folder}\ndescription: Does fixtures.\n---\n");

        var problems = SkillFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith($".claude/skills/{folder}: {expected}");
    }

    [Fact]
    public void Fixture_WithATooLongFolderName_NamesIt()
    {
        var folder = new string('a', 65);
        var path = Path.Combine(_root, ".claude", "skills", folder);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "SKILL.md"), $"---\nname: {folder}\ndescription: Does fixtures.\n---\n");

        var problems = SkillFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldContain("at most 64");
    }

    [Fact]
    public void Fixture_WhenDocsNameAMissingSkill_NamesTheDoc()
    {
        Directory.CreateDirectory(Path.Combine(_root, "docs"));
        File.WriteAllText(Path.Combine(_root, "AGENTS.md"), $"Use the `{_name}` skill.\n");
        File.WriteAllText(Path.Combine(_root, "docs", "FLOW.md"), "Run the `ship-it`\nskill.\n");

        var problems = SkillFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldBe("docs/FLOW.md: names the ship-it skill, which has no .claude/skills/ship-it/SKILL.md.");
    }

    [Fact]
    public void Fixture_WhenAnAgentNamesAMissingSkill_NamesTheAgent()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".claude", "agents"));
        File.WriteAllText(Path.Combine(_root, ".claude", "agents", "fixture-lead.md"), "Run the `ship-it` skill.\n");

        var problems = SkillFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldBe(".claude/agents/fixture-lead.md: names the ship-it skill, which has no .claude/skills/ship-it/SKILL.md.");
    }

    [Fact]
    public void Fixture_WithAFolderWithoutSkillFile_NamesIt()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".claude", "skills", "empty"));

        var problems = SkillFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith(".claude/skills/empty/SKILL.md: missing");
    }

    [Fact]
    public void Fixture_WithAStrayFile_NamesIt()
    {
        File.WriteAllText(Path.Combine(_root, ".claude", "skills", "notes.md"), "Notes.\n");

        var problems = SkillFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith(".claude/skills/notes.md: holds only skill folders");
    }

    [Fact]
    public void Fixture_WithoutTheSkillsFolder_NamesIt()
    {
        Directory.Delete(Path.Combine(_root, ".claude", "skills"), recursive: true);

        var problems = SkillFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith(".claude/skills: missing");
    }
}
