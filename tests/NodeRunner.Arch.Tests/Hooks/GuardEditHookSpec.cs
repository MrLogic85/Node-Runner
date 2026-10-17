using System.Text.Json.Nodes;

namespace NodeRunner.Arch.Tests.Hooks;

/// <summary>
/// .claude/hooks/guard-edit.sh denies edit-tool writes into /tmp and the issues/ archive, in
/// each payload shape the tools send.
/// </summary>
public sealed class GuardEditHookSpec(HookFixture fixture) : IClassFixture<HookFixture>
{
    private const string _hook = "guard-edit.sh";
    private const string _tmp = "Do not write scratch files to /tmp";
    private const string _issues = "issues/ is a read-only archive";

    private readonly HookFixture _fixture = fixture;

    /// <summary>A null field sends the path as an apply_patch string.</summary>
    [Theory]
    [InlineData("file_path", "/tmp/notes.md", _tmp)]
    [InlineData("path", "/tmp", _tmp)]
    [InlineData("path", "/private/tmp/shot.png", _tmp)]
    [InlineData("file_path", "/var/../tmp/notes.md", _tmp)]
    [InlineData("path", "{root}/issues/0001.md", _issues)]
    [InlineData("file_path", "docs/../issues/0001.md", _issues)]
    [InlineData("notebook_path", "issues/0001.ipynb", _issues)]
    [InlineData(null, "*** Begin Patch\n*** Update File: issues/0001.md\n@@\n-a\n+b\n*** End Patch\n", _issues)]
    [InlineData(null, "*** Begin Patch\n*** Update File: docs/a.md\n*** Move to: /tmp/a.md\n*** End Patch\n", _tmp)]
    [InlineData("file_path", ".scratch/shot.png", null)]
    [InlineData("path", "{root}/docs/REVIEW.md", null)]
    [InlineData("file_path", "issues-notes.md", null)]
    [InlineData("path", "docs/issues/notes.md", null)]
    [InlineData("file_path", "/tmpfiles/notes.md", null)]
    [InlineData(null, "*** Begin Patch\n*** Update File: docs/a.md\n@@\n+*** Add File: /tmp/x\n*** End Patch\n", null)]
    public void Edit_IsDeniedOnlyIntoTmpOrTheArchive(string? field, string path, string? reason) =>
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

    private JsonObject Edit(string? field, string path)
    {
        path = path.Replace("{root}", _fixture.Root, StringComparison.Ordinal);
        return new()
        {
            ["tool_name"] = "Edit",
            ["tool_input"] = field is null ? path : new JsonObject { [field] = path },
        };
    }
}
