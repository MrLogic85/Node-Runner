namespace NodeRunner.Arch.Tests.Hooks;

/// <summary>
/// .claude/hooks/guard-bash.sh denies the git commands AGENTS.md → "Working copy" forbids, work
/// that lands on main without a PR, and bad PR titles, but not text that only mentions them
/// (#1320).
/// </summary>
public sealed class GuardBashHookSpec(HookFixture fixture) : IClassFixture<HookFixture>
{
    private const string _hook = "guard-bash.sh";

    private readonly HookFixture _fixture = fixture;

    /// <summary>A second checkout inside the fixture, on a feature branch.</summary>
    private readonly string _feature = fixture.Checkout("feature", "feature");

    /// <summary>Gives the main checkout a local branch and a branch that exists only on a remote.</summary>
    private readonly HookFixture _branches = Branches(fixture);

    [Theory]
    [InlineData("")]
    [InlineData("{bad")]
    public void InvalidInput_IsAllowed(string stdin)
    {
        _fixture.RunRaw(_hook, stdin).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("git stash", "git stash is not allowed")]
    [InlineData("git stash push -m wip", "git stash is not allowed")]
    [InlineData("git -C feature stash", "git stash is not allowed")]
    [InlineData("/usr/bin/git --no-pager stash", "git stash is not allowed")]
    [InlineData("FOO=1 git stash", "git stash is not allowed")]
    [InlineData("env -u HOME GIT_TRACE=0 git stash", "git stash is not allowed")]
    [InlineData("git status && git stash", "git stash is not allowed")]
    [InlineData("ls; git stash", "git stash is not allowed")]
    [InlineData("ls\ngit stash", "git stash is not allowed")]
    [InlineData("(git stash)", "git stash is not allowed")]
    [InlineData("x=$(git stash)", "git stash is not allowed")]
    [InlineData("if git stash; then :; fi", "git stash is not allowed")]
    [InlineData("bash -lc 'git stash'", "git stash is not allowed")]
    [InlineData("git worktree add ../copy", "git worktree is not allowed")]
    [InlineData("git clone https://github.com/MrLogic85/Node-Runner.git copy", "git clone is not allowed")]
    [InlineData("git reset --hard origin/main", "git reset --hard is not allowed")]
    [InlineData("git commit -m Fix", "Do not commit on main")]
    [InlineData("git -c core.editor=true commit --amend", "Do not commit on main")]
    [InlineData("cd feature && cd .. && git commit -m Fix", "Do not commit on main")]
    [InlineData("git switch -c fix && git switch main && git commit -m Fix", "Do not commit on main")]
    [InlineData("git checkout -- notes.md && git commit -m Fix", "Do not commit on main")]
    [InlineData("git checkout notes.md && git commit -m Fix", "Do not commit on main")]
    [InlineData("git checkout HEAD -- notes.md && git commit -m Fix", "Do not commit on main")]
    [InlineData("env -C feature git log; git commit -m Fix", "Do not commit on main")]
    [InlineData("git -C feature switch feature && git commit -m Fix", "Do not commit on main")]
    [InlineData("git checkout -p other && git commit -m Fix", "Do not commit on main")]
    [InlineData("env -C feature -C . git commit -m Fix", "Do not commit on main")]
    [InlineData("env -C feature --chdir . git commit -m Fix", "Do not commit on main")]
    [InlineData("git switch -c fix && git switch -t origin/main && git commit -m Fix", "Do not commit on main")]
    [InlineData("git push", "Do not push to main")]
    [InlineData("git push origin main", "Do not push to main")]
    [InlineData("git push origin HEAD", "Do not push to main")]
    [InlineData("git push -u origin HEAD:main", "Do not push to main")]
    [InlineData("git push origin feature:refs/heads/main", "Do not push to main")]
    [InlineData("git push --force origin +main", "Do not push to main")]
    [InlineData("git push origin --delete main", "Do not push to main")]
    [InlineData("git push --all origin", "Do not push to main")]
    [InlineData("git -C feature push origin feature:main", "Do not push to main")]
    [InlineData("gh pr create --title \"add hooks\" --body x", "PR title \"add hooks\" fails")]
    [InlineData("gh pr create -t \"chore: Add hooks\"", "PR title \"chore: Add hooks\" fails")]
    [InlineData("gh pr create --title=\"chore(#12): add hooks\"", "PR title \"chore(#12): add hooks\" fails")]
    [InlineData("gh pr create --title \"wip(#12): Add hooks\"", "PR title \"wip(#12): Add hooks\" fails")]
    [InlineData("gh pr edit 12 --title \"chore(#12) Add hooks\"", "PR title \"chore(#12) Add hooks\" fails")]
    [InlineData("gh pr create --fill", "Pass the PR title with --title")]
    [InlineData("gh -R MrLogic85/Node-Runner pr create --title \"add hooks\"", "PR title \"add hooks\" fails")]
    public void OnMain_Command_IsDenied(string command, string reason)
    {
        var output = _fixture.Run(_hook, HookFixture.Bash(command));

        HookFixture.DenyReason(output).ShouldNotBeNull().ShouldStartWith(reason);
    }

    [Theory]
    [InlineData("echo \"git stash\"")]
    [InlineData("echo 'git push origin main' > notes.txt")]
    [InlineData("git log --grep 'git stash'")]
    [InlineData("ls # git stash")]
    [InlineData("cat <<'EOF'\ngit stash\ngit push origin main\nEOF\necho done")]
    [InlineData("git stash list")]
    [InlineData("git stash show -p")]
    [InlineData("git worktree list")]
    [InlineData("git reset HEAD~1")]
    [InlineData("git reset --soft HEAD~1")]
    [InlineData("git commit --dry-run -m Fix")]
    [InlineData("git push origin v0.14.3")]
    [InlineData("git push --tags")]
    [InlineData("git push origin release/v0.14")]
    [InlineData("git push origin feature")]
    [InlineData("gh pr create --title \"chore(#1320): Add hooks\" --body \"git stash\"")]
    [InlineData("gh pr create -t \"refactor(#497, #498): Split the solver\"")]
    [InlineData("gh pr edit 12 --body x")]
    [InlineData("gh pr view 12")]
    [InlineData("git switch -c fix && git commit -m Fix && git push -u origin HEAD")]
    [InlineData("git checkout -b fix; git commit -m Fix")]
    [InlineData("git switch feature && git commit -m Fix")]
    [InlineData("git switch - && git commit -m Fix")]
    [InlineData("git checkout HEAD~0 && git commit -m Fix")]
    [InlineData("env -C feature git commit -m Fix")]
    [InlineData("env --chdir=feature git commit -m Fix")]
    [InlineData("git checkout other && git commit -m Fix")]
    [InlineData("git checkout remote-only && git commit -m Fix")]
    [InlineData("git switch --create=fix && git commit -m Fix")]
    [InlineData("git checkout -bfix && git commit -m Fix")]
    [InlineData("git switch -cfix && git commit -m Fix")]
    [InlineData("git checkout -fb fix && git commit -m Fix")]
    [InlineData("git checkout --orphan=fix && git commit -m Fix")]
    [InlineData("git switch -t origin/remote-only && git commit -m Fix")]
    [InlineData("git switch @{-1} && git commit -m Fix")]
    [InlineData("env -C . -C feature git commit -m Fix")]
    [InlineData("env -Cfeature git commit -m Fix")]
    [InlineData("git checkout other -- && git commit -m Fix")]
    [InlineData("git switch --detach other && git commit -m Fix")]
    [InlineData("git switch -d other && git commit -m Fix")]
    [InlineData("git switch --track=direct origin/remote-only && git commit -m Fix")]
    [InlineData("gh pr create --title \"$TITLE\" --body-file body.md")]
    [InlineData("gh pr create --title \"$(head -1 title.txt)\"")]
    public void OnMain_Command_IsAllowed(string command)
    {
        var output = _fixture.Run(_hook, HookFixture.Bash(command));

        HookFixture.DenyReason(output).ShouldBeNull();
    }

    [Theory]
    [InlineData("git commit -m Fix")]
    [InlineData("git commit -F - <<'EOF'\nDeny git stash.\ngit push origin main\nEOF")]
    [InlineData("git push")]
    [InlineData("git push -u origin HEAD")]
    [InlineData("git -C . commit -m Fix")]
    [InlineData("git checkout main -- notes.md && git commit -m Fix")]
    [InlineData("git checkout main notes.md; git commit -m Fix")]
    [InlineData("env -C . git commit -m Fix")]
    public void OnAFeatureBranch_Command_IsAllowed(string command)
    {
        var output = _fixture.Run(_hook, HookFixture.Bash(command), cwd: _feature);

        HookFixture.DenyReason(output).ShouldBeNull();
    }

    [Theory]
    [InlineData("git -C .. commit -m Fix", "Do not commit on main")]
    [InlineData("git checkout main && git commit -m Fix", "Do not commit on main")]
    [InlineData("git switch --create=main && git commit -m Fix", "Do not commit on main")]
    [InlineData("git switch -cmain && git commit -m Fix", "Do not commit on main")]
    [InlineData("git checkout -bmain && git commit -m Fix", "Do not commit on main")]
    [InlineData("git checkout -fB main && git commit -m Fix", "Do not commit on main")]
    [InlineData("git checkout main -- && git commit -m Fix", "Do not commit on main")]
    [InlineData("git switch --force-create main && git commit -m Fix", "Do not commit on main")]
    [InlineData("git switch --orphan main && git commit -m Fix", "Do not commit on main")]
    [InlineData("git checkout -fqb main && git commit -m Fix", "Do not commit on main")]
    [InlineData("cd .. && git push", "Do not push to main")]
    public void OnAFeatureBranch_CommandInTheMainCheckout_IsDenied(string command, string reason)
    {
        var output = _fixture.Run(_hook, HookFixture.Bash(command), cwd: _feature);

        HookFixture.DenyReason(output).ShouldNotBeNull().ShouldStartWith(reason);
    }

    [Fact]
    public void OnAFeatureBranch_SwitchInAnotherCheckout_KeepsThisOnesSwitch()
    {
        var output = _fixture.Run(_hook, HookFixture.Bash("git switch main && git -C .. switch -c fix && git commit -m Fix"), cwd: _feature);

        HookFixture.DenyReason(output).ShouldNotBeNull().ShouldStartWith("Do not commit on main");
    }

    private static HookFixture Branches(HookFixture fixture)
    {
        HookFixture.Git(fixture.Root, "branch", "-f", "other");
        HookFixture.Git(fixture.Root, "update-ref", "refs/remotes/origin/remote-only", "HEAD");
        return fixture;
    }
}
