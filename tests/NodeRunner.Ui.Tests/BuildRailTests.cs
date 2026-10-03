using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>Build's rail and top bar (#370): play sits at the bottom of the rail in both states.</summary>
public sealed class BuildRailTests
{
    private static readonly SceneNodes.SceneNode[] _build = [.. SceneNodes.InScene("screens/BuildScreen.tscn")];

    [Fact]
    public void Rail_HoldsTheTools_ThenPlayAtTheBottom()
    {
        Children("/ButtonBarContent").ShouldBe(["PartsTool", "BeamTool", "JointTool", "SelectTool", "RailSpacer", "StartTraining"]);
        _build.Single(node => node.Name == "RailSpacer").Node.Body.ShouldContain("size_flags_vertical = 3");
        var parts = _build.Single(node => node.Name == "PartsTool").Node.Body;
        parts.ShouldContain("text = \"Parts\"");
        parts.ShouldContain($"IconId = {(int)UiIconId.Parts}");
    }

    [Fact]
    public void Play_IsThePrimaryStackedPlayIcon_WithNoLabel()
    {
        var play = _build.Single(node => node.Name == "StartTraining").Node.Body;

        play.ShouldContain($"IconId = {(int)UiIconId.Play}");
        play.ShouldContain($"Kind = {(int)UiButtonKind.Primary}");
        play.ShouldContain($"ContentLayout = {(int)UiButtonContentLayout.Stacked}");
        play.ShouldNotContain("\ntext = ");
        play.ShouldContain("tooltip_text = \"Start training\"");
    }

    [Fact]
    public void TopBar_HoldsTheNameAndThePadlock()
    {
        Children("/ToolbarContent").ShouldBe(["CreationName", "Unlock"]);
        _build.Single(node => node.Name == "Unlock").Node.Body.ShouldContain($"IconId = {(int)UiIconId.Lock}");
    }

    [Fact]
    public void ResetTraining_IsDangerLikeDelete()
    {
        var danger = $"Kind = {(int)UiMenuActionItem.MenuItemKind.Danger}";

        _build.Single(node => node.Name == "MenuResetTraining").Node.Body.ShouldContain(danger);
        _build.Single(node => node.Name == "MenuDeleteCreation").Node.Body.ShouldContain(danger);
    }

    [Fact]
    public void Overflow_PutsResetTrainingUnderCopy_AboveDelete()
    {
        Children("/ToolbarMenu").ShouldBe(["MenuStats", "MenuPowerBudget", "MenuCopyCreation", "MenuResetTraining", "MenuDeleteCreation"]);
    }

    private static string[] Children(string parent) =>
        [.. _build.Where(node => node.Parent?.EndsWith(parent, StringComparison.Ordinal) == true).Select(node => node.Name)];
}
