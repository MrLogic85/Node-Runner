using System.Text.Json.Nodes;

namespace NodeRunner.Arch.Tests.Hooks;

/// <summary>
/// .claude/hooks/guard-edit.sh denies edit-tool writes into /tmp and the issues/ archive.
/// </summary>
public sealed class GuardEditHookSpec(HookFixture fixture) : IClassFixture<HookFixture>
{
    private const string _hook = "guard-edit.sh";
    private const string _tmp = "Do not write scratch files to /tmp";
    private const string _issues = "issues/ is a read-only archive";

    private readonly HookFixture _fixture = fixture;

    [Theory]
    [InlineData("file_path", "/tmp/notes.md", _tmp)]
    [InlineData("file_path", "/tmp", _tmp)]
    [InlineData("file_path", "/private/tmp/shot.png", _tmp)]
    [InlineData("file_path", "/var/../tmp/notes.md", _tmp)]
    [InlineData("file_path", "{root}/issues/0001.md", _issues)]
    [InlineData("file_path", "docs/../issues/0001.md", _issues)]
    [InlineData("notebook_path", "issues/0001.ipynb", _issues)]
    [InlineData("file_path", ".scratch/shot.png", null)]
    [InlineData("file_path", "issues-notes.md", null)]
    [InlineData("file_path", "docs/issues/notes.md", null)]
    [InlineData("file_path", "/tmpfiles/notes.md", null)]
    public void Edit_IsDeniedOnlyIntoTmpOrTheArchive(string field, string path, string? reason) =>
        HookFixture.ShouldDecide(_fixture.Run(_hook, Edit(field, path)), reason);

    [Fact]
    public void Write_RelativeToASubfolder_IsResolvedFromThere()
    {
        var docs = Path.Combine(_fixture.Root, "docs");
        Directory.CreateDirectory(docs);

        HookFixture.DenyReason(_fixture.Run(_hook, Edit("file_path", "../issues/0001.md"), cwd: docs))
            .ShouldNotBeNull().ShouldStartWith(_issues);
        HookFixture.DenyReason(_fixture.Run(_hook, Edit("file_path", "issues/0001.md"), cwd: docs)).ShouldBeNull();
    }

    private JsonObject Edit(string field, string path)
    {
        path = path.Replace("{root}", _fixture.Root, StringComparison.Ordinal);
        return new()
        {
            ["tool_name"] = "Edit",
            ["tool_input"] = new JsonObject { [field] = path },
        };
    }
}
