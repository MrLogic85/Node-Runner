using System.Text.Json.Nodes;

namespace NodeRunner.Arch.Tests.Hooks;

/// <summary>
/// .claude/hooks/reviewer-lsp.sh lets a review agent use the LSP tool only for lookups, since
/// Copilot's rename writes the files (#1352). See docs/REVIEW.md → "Hooks".
/// </summary>
public sealed class ReviewerLspHookSpec(HookFixture fixture) : IClassFixture<HookFixture>
{
    private const string _hook = "reviewer-lsp.sh";

    [Theory]
    [InlineData("goToDefinition")]
    [InlineData("findReferences")]
    [InlineData("hover")]
    [InlineData("documentSymbol")]
    [InlineData("workspaceSymbol")]
    [InlineData("goToImplementation")]
    [InlineData("prepareCallHierarchy")]
    [InlineData("incomingCalls")]
    [InlineData("outgoingCalls")]
    public void Lookup_IsAllowed(string operation) =>
        HookFixture.DenyReason(fixture.Run(_hook, Payload(operation))).ShouldBeNull();

    [Theory]
    [InlineData("rename", "LSP rename is not allowed for a review agent")]
    [InlineData("Rename", "LSP Rename is not allowed for a review agent")]
    [InlineData("codeAction", "LSP codeAction is not allowed for a review agent")]
    [InlineData(null, "LSP without an operation is not allowed for a review agent")]
    public void OtherOperation_IsDenied(string? operation, string reason) =>
        HookFixture.DenyReason(fixture.Run(_hook, Payload(operation))).ShouldStartWith(reason);

    private static JsonObject Payload(string? operation)
    {
        var input = new JsonObject { ["file"] = "libs/A.cs", ["line"] = 6, ["character"] = 10 };
        if (operation is not null)
        {
            input["operation"] = operation;
        }

        return new JsonObject { ["hook_event_name"] = "PreToolUse", ["tool_name"] = "lsp", ["tool_input"] = input };
    }
}
