namespace NodeRunner.Arch.Tests;

/// <summary>
/// Rule files load for the files they govern: docs/REVIEW.md → "Agent, rule and skill files" (#1315). The
/// fixtures live in a temp directory, so neither tool loads them as rules.
/// </summary>
public sealed class RuleFilesSpec : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("rule-files-").FullName;

    public RuleFilesSpec()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".claude", "rules"));
        Directory.CreateDirectory(Path.Combine(_root, "src", "ui"));
        File.WriteAllText(Path.Combine(_root, "project.godot"), "");
        File.WriteAllText(Rule, "---\npaths:\n  - \"src/ui/**\"\n  - \"project.godot\"\n---\n\n# UI rules\n");
    }

    private string Rule => Path.Combine(_root, ".claude", "rules", "ui.md");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Repository_RuleFilesMatchExistingPaths()
    {
        var problems = RuleFiles.Problems(RepositoryRoot.Find());

        problems.ShouldBeEmpty("See docs/REVIEW.md → \"Agent, rule and skill files\".");
    }

    [Fact]
    public void Fixture_WithALocalRule_SkipsIt()
    {
        File.WriteAllText(Path.Combine(_root, ".claude", "rules", "local_device.md"), "No frontmatter.\n");

        var problems = RuleFiles.Problems(_root);

        problems.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("---\n---\n# UI rules\n", ".claude/rules/ui.md: starts with frontmatter that has only paths:")]
    [InlineData("# UI rules\n", ".claude/rules/ui.md: starts with frontmatter that has only paths:")]
    [InlineData("---\npaths:\n---\n", ".claude/rules/ui.md: starts with frontmatter that has only paths:")]
    [InlineData("---\npaths:\n  - \"src/gui/**\"\n---\n", ".claude/rules/ui.md: src/gui/** is not an existing")]
    [InlineData("---\npaths:\n  - \"src/ui/*.cs\"\n---\n", ".claude/rules/ui.md: src/ui/*.cs is not an existing")]
    [InlineData("---\npaths:\n  - \"Project.cfg\"\n---\n", ".claude/rules/ui.md: Project.cfg is not an existing")]
    [InlineData("---\npaths: [\"src/ui/**\"]\n---\n", ".claude/rules/ui.md: starts with frontmatter that has only paths:")]
    [InlineData("---\npaths:\n  - src/ui/**\n---\n", ".claude/rules/ui.md: lists each path on its own line")]
    public void Fixture_WithABrokenRule_NamesTheFileAndRule(string text, string expected)
    {
        File.WriteAllText(Rule, text);

        var problems = RuleFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith(expected);
    }

    [Fact]
    public void Fixture_WithoutTheRulesFolder_NamesIt()
    {
        Directory.Delete(Path.Combine(_root, ".claude", "rules"), recursive: true);

        var problems = RuleFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith(".claude/rules: missing");
    }

    [Fact]
    public void Fixture_WithAStrayFile_NamesIt()
    {
        File.WriteAllText(Path.Combine(_root, ".claude", "rules", "notes.txt"), "Notes.\n");

        var problems = RuleFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith(".claude/rules/notes.txt: holds only rule files");
    }
}
