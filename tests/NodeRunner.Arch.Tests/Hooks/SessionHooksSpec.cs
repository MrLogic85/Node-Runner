using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;

namespace NodeRunner.Arch.Tests.Hooks;

/// <summary>
/// session-start.sh tells the agent the branch and uncommitted changes; turn-end.sh reports,
/// once, the emulators and APKs a session left behind, compared with its start (#1320).
/// </summary>
public sealed class SessionHooksSpec : IDisposable
{
    private readonly HookFixture _fixture = new("feature/1320-hooks");
    private readonly List<Process> _emulators = [];

    private string Build => Path.Combine(_fixture.Root, "build");

    private string State => Path.Combine(_fixture.Root, ".git", "agent-hooks", HookFixture.SessionId);

    public void Dispose()
    {
        foreach (var emulator in _emulators)
        {
            if (!emulator.HasExited)
            {
                emulator.Kill();
            }

            emulator.Dispose();
        }

        _fixture.Dispose();
    }

    [Fact]
    public void SessionStart_WithACleanCheckout_NamesTheBranchInBothFields()
    {
        var context = StartSession();

        context.ShouldBe("Working copy at session start: branch feature/1320-hooks, no uncommitted changes.");
    }

    [Fact]
    public void SessionStart_WithUncommittedChanges_ListsThemAndSaysToAsk()
    {
        File.WriteAllText(Path.Combine(_fixture.Root, "notes.md"), "Notes.\n");

        var context = StartSession();

        context.ShouldContain("1 uncommitted changes:\n?? notes.md\n");
        context.ShouldContain("stop and ask the human");
    }

    [Fact]
    public void SessionStart_WithManyChanges_ListsTenAndCountsTheRest()
    {
        for (var i = 0; i < 12; i++)
        {
            File.WriteAllText(Path.Combine(_fixture.Root, $"note{i:00}.md"), "");
        }

        var context = StartSession();

        context.ShouldContain("12 uncommitted changes:\n?? note00.md\n");
        context.ShouldContain("?? note09.md\n… and 2 more.\n");
        context.ShouldNotContain("note10");
    }

    [Theory]
    [InlineData("resume")]
    [InlineData("compact")]
    public void SessionStart_OnResume_ReportsWithoutAskingAboutItsOwnChanges(string source)
    {
        File.WriteAllText(Path.Combine(_fixture.Root, "notes.md"), "Notes.\n");

        var context = StartSession(source);

        context.ShouldStartWith("Working copy now: branch feature/1320-hooks, 1 uncommitted changes:");
        context.ShouldNotContain("stop and ask");
    }

    [Fact]
    public void SessionStart_Again_KeepsTheFirstBaseline()
    {
        StartSession();
        var during = StartEmulator();
        File.WriteAllText(Path.Combine(_fixture.Root, "notes.md"), "Notes.\n");

        var context = StartSession("startup");

        context.ShouldNotContain("stop and ask");
        Block(EndTurn()).ShouldContain($"emulator pid {during.Id}");
    }

    [Fact]
    public void SessionStart_WithACamelCaseSessionId_KeepsState()
    {
        var payload = new JsonObject { ["sessionId"] = "camel", ["cwd"] = _fixture.Root };

        _fixture.RunRaw("session-start.sh", payload.ToJsonString());

        Directory.Exists(Path.Combine(_fixture.Root, ".git", "agent-hooks", "camel")).ShouldBeTrue();
    }

    [Fact]
    public void SessionStart_WhenGitFails_SaysToAsk()
    {
        Directory.Move(Path.Combine(_fixture.Root, ".git"), Path.Combine(_fixture.Root, "moved.git"));
        File.WriteAllText(Path.Combine(_fixture.Root, ".git"), "gitdir: missing.git\n");

        var context = StartSession();

        context.ShouldStartWith("Could not read the working copy");
        context.ShouldContain("Ask the human");
        context.ShouldEndWith("the end-of-turn check for emulators and APKs is off.");
    }

    [Fact]
    public void SessionStart_PrunesOnlyStaleSessions()
    {
        var sessions = Path.GetDirectoryName(State)!;
        var stale = Directory.CreateDirectory(Path.Combine(sessions, "stale")).FullName;
        var live = Directory.CreateDirectory(Path.Combine(sessions, "live")).FullName;
        Directory.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-49));
        Directory.SetLastWriteTimeUtc(live, DateTime.UtcNow.AddHours(-47));

        StartSession();

        Directory.Exists(stale).ShouldBeFalse();
        Directory.Exists(live).ShouldBeTrue();
    }

    [Fact]
    public void TurnEnd_KeepsALongSessionFromLookingStale()
    {
        StartSession();
        Directory.SetLastWriteTimeUtc(State, DateTime.UtcNow.AddDays(-4));

        EndTurn();

        Directory.GetLastWriteTimeUtc(State).ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
    }

    [Theory]
    [InlineData("turn-end.sh")]
    [InlineData("session-end.sh")]
    public void WithoutASessionId_IsSilent(string hook)
    {
        StartSession();
        StartEmulator();

        _fixture.RunRaw(hook, new JsonObject { ["cwd"] = _fixture.Root }.ToJsonString()).ShouldBeEmpty();
        Directory.Exists(State).ShouldBeTrue();
    }

    [Fact]
    public void SessionStart_WithoutASessionId_SaysTheTurnEndCheckIsOff()
    {
        var output = _fixture.RunRaw("session-start.sh", new JsonObject { ["cwd"] = _fixture.Root }.ToJsonString());

        JsonNode.Parse(output)!["additionalContext"]!.GetValue<string>()
            .ShouldEndWith("the end-of-turn check for emulators and APKs is off.");
    }

    [Fact]
    public void TurnEnd_WithQemuOnlyAsAnArgument_IsSilent()
    {
        StartSession();
        var start = new ProcessStartInfo("bash");
        foreach (var argument in new[] { "-c", "sleep 60; :", "qemu-system-argument" })
        {
            start.ArgumentList.Add(argument);
        }

        _emulators.Add(Process.Start(start)!);
        Thread.Sleep(100);

        EndTurn().ShouldBeEmpty();
    }

    [Fact]
    public void TurnEnd_WithNothingLeft_IsSilent()
    {
        StartSession();

        EndTurn().ShouldBeEmpty();
    }

    [Fact]
    public void TurnEnd_WithANewApk_ReportsItOnce()
    {
        Directory.CreateDirectory(Build);
        var old = Path.Combine(Build, "node-runner-0.14.2.apk");
        File.WriteAllText(old, "");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-1));
        StartSession();
        File.WriteAllText(Path.Combine(Build, "node-runner-0.14.3.apk"), "");
        File.SetLastWriteTimeUtc(Path.Combine(Build, "node-runner-0.14.3.apk"), DateTime.UtcNow.AddMinutes(1));

        var first = Block(EndTurn());

        first.ShouldContain("build/node-runner-0.14.3.apk");
        first.ShouldNotContain("0.14.2");
        EndTurn().ShouldBeEmpty();
    }

    [Fact]
    public void TurnEnd_WithAnEmulatorStartedThisSession_ReportsOnlyThatOne()
    {
        var before = StartEmulator();
        StartSession();
        var during = StartEmulator();

        var reason = Block(EndTurn());

        reason.ShouldContain($"emulator pid {during.Id}");
        reason.ShouldNotContain($"emulator pid {before.Id}");
    }

    [Fact]
    public void TurnEnd_WhenAlreadyContinuing_IsSilent()
    {
        StartSession();
        StartEmulator();

        EndTurn(stopHookActive: true).ShouldBeEmpty();
    }

    [Fact]
    public void SessionEnd_RemovesTheSessionState()
    {
        StartSession();
        Directory.Exists(State).ShouldBeTrue();

        _fixture.Run("session-end.sh", new JsonObject { ["hook_event_name"] = "SessionEnd" });

        Directory.Exists(State).ShouldBeFalse();
    }

    private static string Block(string output)
    {
        var json = JsonNode.Parse(output)!;
        json["decision"]!.GetValue<string>().ShouldBe("block");
        return json["reason"]!.GetValue<string>();
    }

    private string StartSession(string source = "startup")
    {
        var payload = new JsonObject { ["hook_event_name"] = "SessionStart", ["source"] = source };
        var json = JsonNode.Parse(_fixture.Run("session-start.sh", payload))!;
        var context = json["additionalContext"]!.GetValue<string>();
        json["hookSpecificOutput"]!["hookEventName"]!.GetValue<string>().ShouldBe("SessionStart");
        json["hookSpecificOutput"]!["additionalContext"]!.GetValue<string>().ShouldBe(context);
        return context;
    }

    private string EndTurn(bool stopHookActive = false) =>
        _fixture.Run("turn-end.sh", new JsonObject { ["hook_event_name"] = "Stop", ["stop_hook_active"] = stopHookActive });

    /// <summary>
    /// A sleep whose program name is the emulator's, which is what the hooks look for. A real
    /// emulator started while the specs run would also be reported.
    /// </summary>
    private Process StartEmulator()
    {
        var start = new ProcessStartInfo("bash");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add($"exec -a qemu-system-fixture{_emulators.Count} sleep 60");
        var process = Process.Start(start)!;
        _emulators.Add(process);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!ProgramOf(process).StartsWith("qemu-system", StringComparison.Ordinal) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(20);
        }

        return process;
    }

    private static string ProgramOf(Process process)
    {
        var start = new ProcessStartInfo("ps") { RedirectStandardOutput = true };
        foreach (var argument in new[] { "-o", "args=", "-p", process.Id.ToString(CultureInfo.InvariantCulture) })
        {
            start.ArgumentList.Add(argument);
        }

        using var ps = Process.Start(start)!;
        var args = ps.StandardOutput.ReadToEnd().Trim();
        ps.WaitForExit();
        return args;
    }
}
