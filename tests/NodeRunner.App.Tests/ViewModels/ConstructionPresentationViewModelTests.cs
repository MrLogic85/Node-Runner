using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class ConstructionPresentationViewModelTests
{
    [Theory]
    [InlineData(ConstructionTool.Place, "Tap empty space to place a node. Drag a node to move it.")]
    [InlineData(ConstructionTool.Beam, "Tap a node, then another node, to connect them with a beam.")]
    [InlineData(ConstructionTool.Core, "Tap a node to attach a core, tap again to remove it.")]
    [InlineData(ConstructionTool.Delete, "Tap a node or beam to delete it.")]
    public void ToolHint_ReturnsUserFacingHintForTool(ConstructionTool tool, string expected)
    {
        ConstructionPresentationViewModel.ToolHint(tool).ShouldBe(expected);
    }

    [Fact]
    public void BuildModeButtonText_WhenInactive_ShowsBuild()
    {
        var construction = new ConstructionViewModel();
        var presentation = new ConstructionPresentationViewModel(construction);

        presentation.BuildModeButtonText.ShouldBe("Build");
    }

    [Fact]
    public void BuildModeButtonText_WhenActive_ShowsSimulate()
    {
        var construction = new ConstructionViewModel { IsActive = true };
        var presentation = new ConstructionPresentationViewModel(construction);

        presentation.BuildModeButtonText.ShouldBe("Simulate");
    }

    [Fact]
    public void InspectorValues_UsesStatusMessageBeforeToolHint()
    {
        var construction = new ConstructionViewModel();
        construction.SelectNodeForBeam(0);
        var presentation = new ConstructionPresentationViewModel(construction);

        presentation.InspectorValues.ShouldBe("Node 0 selected. Tap another node to connect.");
    }

    [Fact]
    public void EditMode_HidesTopologyToolsAndShowsRebuildAction()
    {
        var construction = new ConstructionViewModel();
        construction.Load(
            new CreatureDef(
                [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(20, 0), 18)],
                [new BeamDef(0, 1)],
                []),
            moveOnly: true);
        var presentation = new ConstructionPresentationViewModel(construction);

        presentation.ShowConstructionTools.ShouldBeFalse();
        presentation.ShowCompleteAction.ShouldBeFalse();
        presentation.ShowRebuildAction.ShouldBeTrue();
    }

    [Fact]
    public void BuildMode_ShowsTopologyToolsAndCompleteAction()
    {
        var construction = new ConstructionViewModel();
        var presentation = new ConstructionPresentationViewModel(construction);

        presentation.ShowConstructionTools.ShouldBeTrue();
        presentation.ShowCompleteAction.ShouldBeTrue();
        presentation.ShowRebuildAction.ShouldBeFalse();
    }

    [Fact]
    public void CoreToolText_WhenExtraCoreLocked_ShowsFitnessUnlockHint()
    {
        var construction = new ConstructionViewModel();
        construction.PlaceNode(new Vector2D(0, 0), 18);
        construction.ToggleCoreOnNode(0);
        var presentation = new ConstructionPresentationViewModel(construction);

        presentation.CoreToolText.ShouldBe("Core 1/1 (50 fitness)");
        presentation.CoreToolTooltip.ShouldBe("Attach or remove a core. Train to unlock a second core slot.");
    }

    [Fact]
    public void CoreToolText_WhenExtraCoreUnlocked_ShowsUnlockedHint()
    {
        var construction = new ConstructionViewModel();
        construction.SetMaxCores(2);
        var presentation = new ConstructionPresentationViewModel(construction);

        presentation.CoreToolText.ShouldBe("Core 0/2 (unlocked)");
        presentation.CoreToolTooltip.ShouldBe("Attach or remove a core. Extra core slot unlocked.");
    }
}
