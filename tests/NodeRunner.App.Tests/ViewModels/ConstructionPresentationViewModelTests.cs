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
    public void EditMode_LocksTopologyToolsAndShowsRebuildAction()
    {
        var construction = new ConstructionViewModel();
        construction.Load(
            new CreatureDef(
                [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(20, 0), 18)],
                [new BeamDef(0, 1)],
                []),
            moveOnly: true);
        var presentation = new ConstructionPresentationViewModel(construction);

        presentation.PlaceToolText.ShouldBe("Move");
        presentation.LockTopologyTools.ShouldBeTrue();
        presentation.LockedTopologyToolsText.ShouldBe("Beam, Core, Delete locked: Move only · training kept");
        presentation.BeamToolText.ShouldBe("Beam · locked");
        presentation.CoreToolText.ShouldBe("Core · locked");
        presentation.CoreToolTooltip.ShouldBe("Move only · training kept");
        presentation.DeleteToolText.ShouldBe("Delete · locked");
        presentation.InspectorRole.ShouldBe("Tool: Move");
        presentation.InspectorValues.ShouldBe("Drag an existing node to reposition it. Training is kept.");
        presentation.ShowCompleteAction.ShouldBeFalse();
        presentation.ShowRebuildAction.ShouldBeTrue();
        presentation.RebuildActionText.ShouldBe("Rebuild body");
        presentation.RebuildConfirmationTitle.ShouldBe("Rebuild body?");
        presentation.RebuildConfirmationBody.ShouldBe("Rebuild creates a new body and a new brain. The original Creation and its training stay unchanged.");
    }

    [Fact]
    public void BuildMode_ShowsTopologyToolsAndCompleteAction()
    {
        var construction = new ConstructionViewModel();
        var presentation = new ConstructionPresentationViewModel(construction);

        presentation.PlaceToolText.ShouldBe("Place");
        presentation.LockTopologyTools.ShouldBeFalse();
        presentation.BeamToolText.ShouldBe("Beam");
        presentation.DeleteToolText.ShouldBe("Delete");
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

    [Fact]
    public void BuildPanel_WhenAnatomyIsEmpty_DisablesTrainingWithBeginnerReason()
    {
        var construction = new ConstructionViewModel();
        var presentation = new ConstructionPresentationViewModel(construction);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeFalse();
        buildPanel.CanCompleteCreation.ShouldBeFalse();
        buildPanel.DisabledReason.ShouldBe("Add nodes and beams before training a new creature.");
        buildPanel.ValidationLine.ShouldBe("Not ready: Add nodes and beams before training a new creature.");
    }

    [Fact]
    public void BuildPanel_WhenAnatomyIsInvalid_UsesFirstBuilderValidationError()
    {
        var construction = new ConstructionViewModel();
        construction.PlaceNode(new Vector2D(0, 0), 18);
        var presentation = new ConstructionPresentationViewModel(construction);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeFalse();
        buildPanel.CanCompleteCreation.ShouldBeFalse();
        buildPanel.DisabledReason.ShouldBe("Node 0 has no beams attached. Connect it with a beam or remove it.");
        buildPanel.InputSummary.ShouldBe("0 cores placed; fix anatomy to count inputs.");
        buildPanel.MotorRelationSummary.ShouldBe("Fix anatomy to count motor relations.");
        buildPanel.ValidationLine.ShouldBe("Not ready: Node 0 has no beams attached. Connect it with a beam or remove it.");
    }

    [Fact]
    public void BuildPanel_WhenAnatomyIsValid_SummarizesInputsAndMotorRelations()
    {
        var construction = new ConstructionViewModel();
        construction.Load(
            new CreatureDef(
                [
                    new NodeDef(new Vector2D(0, 0), 18),
                    new NodeDef(new Vector2D(56, 0), 18),
                    new NodeDef(new Vector2D(112, 0), 18),
                    new NodeDef(new Vector2D(168, 0), 18),
                    new NodeDef(new Vector2D(224, 0), 18),
                ],
                [new BeamDef(0, 1), new BeamDef(1, 2), new BeamDef(2, 3), new BeamDef(3, 4)],
                [new CoreDef(0)]));
        var presentation = new ConstructionPresentationViewModel(construction);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeTrue();
        buildPanel.CanCompleteCreation.ShouldBeTrue();
        buildPanel.DisabledReason.ShouldBeNull();
        buildPanel.InputSummary.ShouldBe("1 core: 6 sensors; 3 motor relations: 6 sensors; 12 inputs total");
        buildPanel.MotorRelationSummary.ShouldBe("3 motor relations can twist");
        buildPanel.ValidationLine.ShouldBe("Ready: 12 inputs -> 3 outputs");
    }

    [Fact]
    public void BuildPanel_WhenValidAnatomyHasNoMotorRelations_DisablesTrainingWithFlexibleJointReason()
    {
        var construction = new ConstructionViewModel();
        construction.Load(
            new CreatureDef(
                [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(56, 0), 18)],
                [new BeamDef(0, 1)],
                [new CoreDef(0)]));
        var presentation = new ConstructionPresentationViewModel(construction);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeFalse();
        buildPanel.CanCompleteCreation.ShouldBeTrue();
        buildPanel.DisabledReason.ShouldBe("Add a two-beam node. Closed triangles cannot twist.");
        buildPanel.InputSummary.ShouldBe("1 core: 6 sensors; 0 motor relations: 0 sensors; 6 inputs total");
        buildPanel.MotorRelationSummary.ShouldBe("0 motor relations can twist");
        buildPanel.ValidationLine.ShouldBe("Not ready: Add a two-beam node. Closed triangles cannot twist.");
    }

    [Fact]
    public void PresentationChanged_WhenAnatomyChanges_RaisesForLiveBuildScreenRefresh()
    {
        var construction = new ConstructionViewModel();
        var presentation = new ConstructionPresentationViewModel(construction);
        var raiseCount = 0;
        presentation.PresentationChanged += (_, _) => raiseCount++;

        construction.PlaceNode(new Vector2D(0, 0), 18);

        raiseCount.ShouldBe(1);
    }
}
