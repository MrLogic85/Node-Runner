using System.Text.Json.Nodes;

namespace NodeRunner.Arch.Tests.Hooks;

/// <summary>
/// .claude/hooks/guard-edit.sh denies edit-tool writes into /tmp and the issues/ archive, in
/// each payload shape the tools send (#1320).
/// </summary>
public sealed class GuardEditHookSpec(HookFixture fixture) : IClassFixture<HookFixture>
{
    private const string _hook = "guard-edit.sh";
    private const string _tmp = "Do not write scratch files to /tmp";
    private const string _issues = "issues/ is a read-only archive";

    private readonly HookFixture _fixture = fixture;

    [Theory]
    [InlineData("/tmp/notes.md", _tmp)]
    [InlineData("/tmp", _tmp)]
    [InlineData("/private/tmp/shot.png", _tmp)]
    [InlineData("/var/../tmp/notes.md", _tmp)]
    [InlineData("{root}/issues/0001.md", _issues)]
    [InlineData("issues/0001.md", _issues)]
    [InlineData("docs/../issues/0001.md", _issues)]
    public void Write_ToAForbiddenPath_IsDenied(string path, string reason)
    {
        var claude = Edit("Write", "file_path", path);
        var copilot = Edit("Write", "path", path);

        HookFixture.DenyReason(_fixture.Run(_hook, claude)).ShouldNotBeNull().ShouldStartWith(reason);
        HookFixture.DenyReason(_fixture.Run(_hook, copilot)).ShouldNotBeNull().ShouldStartWith(reason);
    }

    [Theory]
    [InlineData(".scratch/shot.png")]
    [InlineData("{root}/docs/REVIEW.md")]
    [InlineData("issues-notes.md")]
    [InlineData("docs/issues/notes.md")]
    [InlineData("/tmpfiles/notes.md")]
    public void Edit_ToAnAllowedPath_IsAllowed(string path)
    {
        var claude = Edit("Edit", "file_path", path);
        var copilot = Edit("Edit", "path", path);

        HookFixture.DenyReason(_fixture.Run(_hook, claude)).ShouldBeNull();
        HookFixture.DenyReason(_fixture.Run(_hook, copilot)).ShouldBeNull();
    }

    [Theory]
    [InlineData("MultiEdit", "file_path")]
    [InlineData("NotebookEdit", "notebook_path")]
    public void OtherEditTools_ToAForbiddenPath_AreDenied(string tool, string field)
    {
        HookFixture.DenyReason(_fixture.Run(_hook, Edit(tool, field, "issues/0001.ipynb")))
            .ShouldNotBeNull().ShouldStartWith(_issues);
    }

    [Fact]
    public void Write_RelativeToASubfolder_IsResolvedFromThere()
    {
        var docs = Path.Combine(_fixture.Root, "docs");
        Directory.CreateDirectory(docs);

        HookFixture.DenyReason(_fixture.Run(_hook, Edit("Write", "file_path", "../issues/0001.md"), cwd: docs))
            .ShouldNotBeNull().ShouldStartWith(_issues);
        HookFixture.DenyReason(_fixture.Run(_hook, Edit("Write", "file_path", "issues/0001.md"), cwd: docs)).ShouldBeNull();
    }

    [Theory]
    [InlineData("*** Begin Patch\n*** Update File: issues/0001.md\n@@\n-a\n+b\n*** End Patch\n", _issues)]
    [InlineData("*** Begin Patch\n*** Add File: /tmp/notes.md\n+a\n*** End Patch\n", _tmp)]
    [InlineData("*** Begin Patch\n*** Update File: docs/a.md\n*** Move to: /tmp/a.md\n*** End Patch\n", _tmp)]
    public void ApplyPatch_ToAForbiddenPath_IsDenied(string patch, string reason)
    {
        var payload = new JsonObject { ["tool_name"] = "Edit", ["tool_input"] = patch };

        HookFixture.DenyReason(_fixture.Run(_hook, payload)).ShouldNotBeNull().ShouldStartWith(reason);
    }

    [Fact]
    public void ApplyPatch_ThatOnlyMentionsAForbiddenPath_IsAllowed()
    {
        var patch = "*** Begin Patch\n*** Update File: docs/a.md\n@@\n+*** Add File: /tmp/x\n*** End Patch\n";
        var payload = new JsonObject { ["tool_name"] = "Edit", ["tool_input"] = patch };

        HookFixture.DenyReason(_fixture.Run(_hook, payload)).ShouldBeNull();
    }

    private JsonObject Edit(string tool, string field, string path) => new()
    {
        ["tool_name"] = tool,
        ["tool_input"] = new JsonObject { [field] = path.Replace("{root}", _fixture.Root, StringComparison.Ordinal) },
    };
}
