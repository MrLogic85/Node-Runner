using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;

namespace NodeRunner.Arch.Tests.Hooks;

/// <summary>
/// session-start.sh tells the agent the branch and uncommitted changes; after-push.sh reports,
/// once after a git push, the emulators and APKs a session left behind, compared with its start
/// (#1320, #1356).
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
        Reminder(Push()).ShouldContain($"emulator pid {during.Id}");
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
        context.ShouldEndWith("the after-push check for emulators and APKs is off.");
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

    [Theory]
    [InlineData("git push")]
    [InlineData("git status")]
    public void AfterPush_OfAnyCommand_KeepsALongSessionFromLookingStale(string command)
    {
        StartSession();
        Directory.SetLastWriteTimeUtc(State, DateTime.UtcNow.AddDays(-4));

        Push(command);

        Directory.GetLastWriteTimeUtc(State).ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
    }

    [Theory]
    [InlineData("after-push.sh")]
    [InlineData("session-end.sh")]
    public void WithoutASessionId_IsSilent(string hook)
    {
        StartSession();
        StartEmulator();

        var input = new JsonObject
        {
            ["cwd"] = _fixture.Root,
            ["tool_input"] = new JsonObject { ["command"] = "git push" },
        };

        _fixture.RunRaw(hook, input.ToJsonString()).ShouldBeEmpty();
        Directory.Exists(State).ShouldBeTrue();
    }

    [Fact]
    public void SessionStart_WithoutASessionId_SaysTheAfterPushCheckIsOff()
    {
        var output = _fixture.RunRaw("session-start.sh", new JsonObject { ["cwd"] = _fixture.Root }.ToJsonString());

        JsonNode.Parse(output)!["additionalContext"]!.GetValue<string>()
            .ShouldEndWith("the after-push check for emulators and APKs is off.");
    }

    [Fact]
    public void SessionStart_WithAToolManifest_RestoresTheToolsFromTheRoot()
    {
        var log = StubDotnet("exit 0");

        var context = StartSession(path: StubPath);

        File.ReadAllText(log).ShouldBe($"{_fixture.Root} tool restore\n");
        context.ShouldNotContain("dotnet tool restore");
    }

    [Fact]
    public void SessionStart_WhenTheToolRestoreFails_SaysToUseGitGrep()
    {
        StubDotnet("exit 1");

        var context = StartSession(path: StubPath);

        context.ShouldEndWith("`dotnet tool restore` failed, so the LSP tool cannot start the C# language server; find callers and definitions with `git grep` instead.");
    }

    [Fact]
    public void SessionStart_WhenTheToolRestoreStalls_StopsWaitingAndSaysToUseGitGrep()
    {
        // The hook leaves the stalled restore running, so it sleeps only as long as the bound below.
        StubDotnet("exec sleep 6");
        var clock = Stopwatch.StartNew();

        var context = StartSession(path: StubPath, environment: new Dictionary<string, string> { ["NODE_RUNNER_RESTORE_WAIT"] = "1" });

        clock.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(6));
        context.ShouldEndWith("`dotnet tool restore` is still running, so the LSP tool may fail to start the C# language server for a while; find callers and definitions with `git grep` until it works.");
    }

    [Fact]
    public void SessionStart_WithoutDotnet_SkipsTheRestoreQuietly()
    {
        File.WriteAllText(Path.Combine(_fixture.Root, "dotnet-tools.json"), "{}\n");

        var context = StartSession(path: PathWithoutDotnet());

        context.ShouldStartWith("Working copy at session start: branch feature/1320-hooks");
        context.ShouldNotContain("dotnet tool restore");
    }

    [Fact]
    public void SessionStart_WithoutAToolManifest_DoesNotRestore()
    {
        var log = StubDotnet("exit 1");
        File.Delete(Path.Combine(_fixture.Root, "dotnet-tools.json"));

        var context = StartSession(path: StubPath);

        File.Exists(log).ShouldBeFalse();
        context.ShouldNotContain("dotnet tool restore");
    }

    [Fact]
    public void SessionStart_OnResume_DoesNotRestoreTheTools()
    {
        var log = StubDotnet("exit 1");

        var context = StartSession("resume", StubPath);

        File.Exists(log).ShouldBeFalse();
        context.ShouldNotContain("dotnet tool restore");
    }

    [Fact]
    public void AfterPush_WithQemuOnlyAsAnArgument_IsSilent()
    {
        StartSession();
        var start = new ProcessStartInfo("bash");
        foreach (var argument in new[] { "-c", "sleep 60; :", "qemu-system-argument" })
        {
            start.ArgumentList.Add(argument);
        }

        _emulators.Add(Process.Start(start)!);
        Thread.Sleep(100);

        Push().ShouldBeEmpty();
    }

    [Fact]
    public void AfterPush_WithNothingLeft_IsSilent()
    {
        StartSession();

        Push().ShouldBeEmpty();
    }

    [Fact]
    public void AfterPush_WithANewApk_ReportsItOnce()
    {
        Directory.CreateDirectory(Build);
        var old = Path.Combine(Build, "node-runner-0.14.2.apk");
        File.WriteAllText(old, "");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-1));
        StartSession();
        File.WriteAllText(Path.Combine(Build, "node-runner-0.14.3.apk"), "");
        File.SetLastWriteTimeUtc(Path.Combine(Build, "node-runner-0.14.3.apk"), DateTime.UtcNow.AddMinutes(1));

        var first = Reminder(Push());

        first.ShouldContain("build/node-runner-0.14.3.apk");
        first.ShouldNotContain("0.14.2");
        Push().ShouldBeEmpty();
    }

    [Fact]
    public void AfterPush_WithAnEmulatorStartedThisSession_ReportsOnlyThatOne()
    {
        var before = StartEmulator();
        StartSession();
        var during = StartEmulator();

        var reminder = Reminder(Push());

        reminder.ShouldContain($"emulator pid {during.Id}");
        reminder.ShouldNotContain($"emulator pid {before.Id}");
    }

    [Theory]
    [InlineData("git status")]
    [InlineData("echo legit push")]
    [InlineData("git log --grep push")]
    [InlineData("git commit -m \"Remind after git push instead\"")]
    [InlineData("gh pr comment 1 --body 'then git push it'")]
    [InlineData("git push --dry-run")]
    [InlineData("git push -n origin main")]
    [InlineData("git push -nu origin main")]
    [InlineData("git push --help")]
    public void AfterPush_OfAnotherCommand_IsSilent(string command)
    {
        StartSession();
        StartEmulator();

        Push(command).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("git -C sub push -u origin feature/1320-hooks")]
    [InlineData("cd sub && git push")]
    [InlineData("git -c core.sshCommand=ssh push")]
    [InlineData("git --no-pager push; echo done")]
    [InlineData("GIT_TRACE=0 /usr/bin/git push 2>&1 | tail -5")]
    [InlineData("if git --git-dir .git push -u origin main; then :; fi")]
    [InlineData("env -C sub GIT_SSH_COMMAND=ssh git push")]
    [InlineData("{ time git push --force-with-lease; }")]
    public void AfterPush_OfAPushInALine_Reminds(string command)
    {
        StartSession();
        var during = StartEmulator();

        Reminder(Push(command)).ShouldContain($"emulator pid {during.Id}");
    }

    [Fact]
    public void SessionEnd_RemovesTheSessionState()
    {
        StartSession();
        Directory.Exists(State).ShouldBeTrue();

        _fixture.Run("session-end.sh", new JsonObject { ["hook_event_name"] = "SessionEnd" });

        Directory.Exists(State).ShouldBeFalse();
    }

    private static string Reminder(string output)
    {
        var json = JsonNode.Parse(output)!;
        var context = json["additionalContext"]!.GetValue<string>();
        json["hookSpecificOutput"]!["hookEventName"]!.GetValue<string>().ShouldBe("PostToolUse");
        json["hookSpecificOutput"]!["additionalContext"]!.GetValue<string>().ShouldBe(context);
        context.ShouldStartWith("Hook after-push.sh (#1356)");
        return context;
    }

    private string StubBin => Path.Combine(_fixture.Root, ".git", "stub-bin");

    private string StubPath => $"{StubBin}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";

    /// <summary>
    /// Adds a tool manifest and a stub <c>dotnet</c> that logs its folder and arguments, writes
    /// to stdout and stderr as the real one does, then runs <paramref name="end"/>; returns the
    /// log's path.
    /// </summary>
    private string StubDotnet(string end)
    {
        File.WriteAllText(Path.Combine(_fixture.Root, "dotnet-tools.json"), "{}\n");
        Directory.CreateDirectory(StubBin);
        var log = Path.Combine(StubBin, "dotnet.log");
        var stub = Path.Combine(StubBin, "dotnet");
        File.WriteAllText(stub, $"#!/bin/sh\necho \"$PWD $*\" >'{log}'\necho Restoring\necho warning >&2\n{end}\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(stub, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return log;
    }

    /// <summary>
    /// A PATH of one folder that links every program on the real PATH except <c>dotnet</c>.
    /// </summary>
    private string PathWithoutDotnet()
    {
        Directory.CreateDirectory(StubBin);
        var linked = new HashSet<string>(StringComparer.Ordinal) { "dotnet" };
        foreach (var folder in Environment.GetEnvironmentVariable("PATH")!.Split(Path.PathSeparator).Where(Directory.Exists))
        {
            foreach (var program in Directory.EnumerateFiles(folder).Where(program => linked.Add(Path.GetFileName(program))))
            {
                File.CreateSymbolicLink(Path.Combine(StubBin, Path.GetFileName(program)), program);
            }
        }

        return StubBin;
    }

    private string StartSession(string source = "startup", string? path = null, IReadOnlyDictionary<string, string>? environment = null)
    {
        var payload = new JsonObject
        {
            ["hook_event_name"] = "SessionStart",
            ["source"] = source,
            ["session_id"] = HookFixture.SessionId,
            ["cwd"] = _fixture.Root,
        };
        var json = JsonNode.Parse(_fixture.RunRaw("session-start.sh", payload.ToJsonString(), path, environment))!;
        var context = json["additionalContext"]!.GetValue<string>();
        json["hookSpecificOutput"]!["hookEventName"]!.GetValue<string>().ShouldBe("SessionStart");
        json["hookSpecificOutput"]!["additionalContext"]!.GetValue<string>().ShouldBe(context);
        return context;
    }

    private string Push(string command = "git push") =>
        _fixture.Run("after-push.sh", new JsonObject
        {
            ["hook_event_name"] = "PostToolUse",
            ["tool_name"] = "Bash",
            ["tool_input"] = new JsonObject { ["command"] = command },
        });

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
