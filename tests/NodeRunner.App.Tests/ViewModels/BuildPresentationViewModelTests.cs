using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildPresentationViewModelTests
{
    [Theory]
    [InlineData(BuildTool.Move, "Drag a joint to move it. Tap a part to select it.")]
    [InlineData(BuildTool.Beam, "Drag from one joint to another to join them with a beam.")]
    [InlineData(BuildTool.Joint, "Tap empty space to add a joint, or tap a beam to split it.")]
    [InlineData(BuildTool.Core, "Tap a node to attach a core, tap again to remove it.")]
    public void ToolHint_ReturnsUserFacingHintForTool(BuildTool tool, string expected)
    {
        BuildPresentationViewModel.ToolHint(tool).ShouldBe(expected);
    }

    [Fact]
    public void BuildModeButtonText_WhenInactive_ShowsBuild()
    {
        var build = new BuildViewModel();
        var presentation = new BuildPresentationViewModel(build);

        presentation.BuildModeButtonText.ShouldBe("Build");
    }

    [Fact]
    public void BuildModeButtonText_WhenActive_ShowsSimulate()
    {
        var build = new BuildViewModel { IsActive = true };
        var presentation = new BuildPresentationViewModel(build);

        presentation.BuildModeButtonText.ShouldBe("Simulate");
    }

    [Fact]
    public void InspectorValues_UsesStatusMessageBeforeToolHint()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0), 18);
        build.ConnectBeam(0, 0);
        var presentation = new BuildPresentationViewModel(build);

        presentation.InspectorValues.ShouldBe("A beam must connect two different nodes.");
    }

    [Fact]
    public void EditMode_LocksTopologyToolsAndShowsRebuildAction()
    {
        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(
            Guid.NewGuid(),
            "Worm",
            new CreatureDef(
                [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(20, 0), 18)],
                [new BeamDef(0, 1)],
                []),
            new TrainingStateDef([2, 1], [0.1, -0.2, 0.3], 3, "Tanh")));
        var presentation = new BuildPresentationViewModel(build);

        presentation.LockTopologyTools.ShouldBeTrue();
        presentation.CoreToolText.ShouldBe("Core · locked");
        presentation.CoreToolTooltip.ShouldBe("Move only · training kept");
        presentation.InspectorRole.ShouldBe("Tool: Move");
        presentation.InspectorValues.ShouldBe("Drag an existing node to reposition it. Training is kept.");
        presentation.IsLocked.ShouldBeTrue();
        presentation.ShowRebuildAction.ShouldBeTrue();
        presentation.RebuildActionText.ShouldBe("Rebuild body");
        presentation.RebuildConfirmationTitle.ShouldBe("Rebuild body?");
        presentation.RebuildConfirmationBody.ShouldBe("Rebuild creates a new body and a new brain. The original Creation and its training stay unchanged.");
    }

    [Fact]
    public void EditMode_WithTraining_ShowsTrainingSummary()
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef(
                [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(20, 0), 18)],
                [new BeamDef(0, 1)],
                []),
            moveOnly: true,
            creationName: "Worm",
            training: new TrainingStateDef([2, 4, 1], Enumerable.Repeat(0.1, 17).ToArray(), 12, "Tanh", 42.25));
        var presentation = new BuildPresentationViewModel(build);

        presentation.CreationName.ShouldBe("Worm");
        presentation.TrainingSummaryTitle.ShouldBe("Trained 12 generations");
        presentation.BestDistanceText.ShouldBe("42.3 m");
        presentation.TrainingSummaryBody.ShouldBe("Generation 12. Best distance 42.3 m. Anatomy is locked so this brain stays valid.");
    }

    [Fact]
    public void BuildMode_ShowsTopologyToolsForADraft()
    {
        var build = new BuildViewModel();
        var presentation = new BuildPresentationViewModel(build);

        presentation.LockTopologyTools.ShouldBeFalse();
        presentation.IsLocked.ShouldBeFalse();
        presentation.ShowRebuildAction.ShouldBeFalse();
    }

    [Fact]
    public void SelectedBeam_ShowsLengthEndpointsAndFixedStructureFacts()
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef(
                [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(3, 4), 18)],
                [new BeamDef(0, 1)],
                []));
        build.SelectBeam(0);
        var presentation = new BuildPresentationViewModel(build);

        presentation.SinglePartTitle.ShouldBe("Beam 1");
        presentation.SinglePartPrimaryLabel.ShouldBe("Length");
        presentation.SinglePartPrimaryValue.ShouldBe("5.0 units");
        presentation.SinglePartConnectionsLabel.ShouldBe("Between");
        presentation.SinglePartConnectionsValue.ShouldBe("Node 1 ↔ Node 2");
        presentation.SinglePartFacts.ShouldBe("Rigid connection");
    }

    [Fact]
    public void SelectedCore_ShowsActualBuiltInSensorContract()
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef(
                [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(20, 0), 18)],
                [new BeamDef(0, 1)],
                [new CoreDef(0)]));
        build.ToggleSelectedNode(0);
        var presentation = new BuildPresentationViewModel(build);

        presentation.SinglePartTitle.ShouldBe("Core · Node 1");
        presentation.SinglePartPrimaryLabel.ShouldBe("Built-in senses");
        presentation.SinglePartPrimaryValue.ShouldBe("6 inputs");
        presentation.SinglePartConnectionsValue.ShouldBe("Node 1");
        presentation.SinglePartFacts.ShouldContain("Forward-down ray");
    }

    [Fact]
    public void SelectedNode_ShowsPositionRadiusAndConnectedBeams()
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef(
                [
                    new NodeDef(new Vector2D(0, 0), 18),
                    new NodeDef(new Vector2D(20, 5), 12),
                    new NodeDef(new Vector2D(40, 0), 18),
                ],
                [new BeamDef(0, 1), new BeamDef(1, 2)],
                []));
        build.ToggleSelectedNode(1);
        var presentation = new BuildPresentationViewModel(build);

        presentation.SelectedPartCount.ShouldBe(1);
        presentation.SinglePartTitle.ShouldBe("Node 2");
        presentation.SinglePartPrimaryValue.ShouldBe("20, 5");
        presentation.SinglePartConnectionsValue.ShouldBe("Beam 1 · Beam 2");
        presentation.SinglePartFacts.ShouldBe("Radius 12.0 · 2 attached Beam(s)");
    }

    [Fact]
    public void MultiSelection_WithCore_SummarizesSelectedNodesAndCore()
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef(
                [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(20, 0), 18)],
                [new BeamDef(0, 1)],
                [new CoreDef(0)]));
        build.ToggleSelectedNode(0);
        build.ToggleSelectedNode(1);
        var presentation = new BuildPresentationViewModel(build);

        presentation.SelectedPartCount.ShouldBe(2);
        presentation.MultiSelectionTitle.ShouldBe("2 selected");
        presentation.MultiSelectionCounts.ShouldBe("Nodes · 2    Core · 1");
    }

    [Fact]
    public void BrainShape_WhenNotChosen_IsTheFixedDefault()
    {
        var build = new BuildViewModel();
        var a = build.PlaceNode(new Vector2D(0, 0), 18);
        var b = build.PlaceNode(new Vector2D(20, 0), 18);
        var c = build.PlaceNode(new Vector2D(40, 0), 18);
        build.ConnectBeam(a, b);
        build.ConnectBeam(b, c);
        build.ToggleCoreOnNode(a);
        var presentation = new BuildPresentationViewModel(build);

        presentation.BrainShape.ShouldBe(BrainShapeDef.Default);
    }

    [Fact]
    public void BrainShape_WhenCustomized_UsesExplicitShape()
    {
        var build = new BuildViewModel();
        build.SetBrainShape(new BrainShapeDef(2, 9));
        var presentation = new BuildPresentationViewModel(build);

        presentation.BrainShape.ShouldBe(new BrainShapeDef(2, 9));
    }

    [Fact]
    public void CoreToolText_WhenExtraCoreLocked_ShowsFitnessUnlockHint()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0), 18);
        build.ToggleCoreOnNode(0);
        var presentation = new BuildPresentationViewModel(build);

        presentation.CoreToolText.ShouldBe("Core 1/1 (50 fitness)");
        presentation.CoreToolTooltip.ShouldBe("Attach or remove a core. Train to unlock a second core slot.");
    }

    [Fact]
    public void CoreToolText_WhenExtraCoreUnlocked_ShowsUnlockedHint()
    {
        var build = new BuildViewModel();
        build.SetMaxCores(2);
        var presentation = new BuildPresentationViewModel(build);

        presentation.CoreToolText.ShouldBe("Core 0/2 (unlocked)");
        presentation.CoreToolTooltip.ShouldBe("Attach or remove a core. Extra core slot unlocked.");
    }

    [Fact]
    public void BuildPanel_WhenAnatomyIsEmpty_DisablesTrainingWithBeginnerReason()
    {
        var build = new BuildViewModel();
        var presentation = new BuildPresentationViewModel(build);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeFalse();
        buildPanel.ReadinessText.ShouldBe("Add nodes + beams");
    }

    [Fact]
    public void BuildPanel_WhenANodeIsUnconnected_SaysSoInReadiness()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0), 18);
        var presentation = new BuildPresentationViewModel(build);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeFalse();
        buildPanel.ReadinessText.ShouldBe("1 node not connected");
        buildPanel.InputSummary.ShouldBe("0 cores placed; fix anatomy to count inputs.");
        buildPanel.MotorRelationSummary.ShouldBe("Fix anatomy to count motor relations.");
    }

    [Fact]
    public void BuildPanel_WhenSeveralNodesAreUnconnected_CountsThemInReadiness()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0), 18);
        build.PlaceNode(new Vector2D(80, 0), 18);
        var presentation = new BuildPresentationViewModel(build);

        presentation.BuildPanel.ReadinessText.ShouldBe("2 nodes not connected");
    }

    [Fact]
    public void BuildPanel_WhenAnatomyIsValid_SummarizesInputsAndMotorRelations()
    {
        var build = new BuildViewModel();
        build.Load(
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
        var presentation = new BuildPresentationViewModel(build);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeTrue();
        buildPanel.ReadinessText.ShouldBe("Ready to train");
        buildPanel.InputSummary.ShouldBe("1 core: 6 sensors; 3 motor relations: 6 sensors; 12 inputs total");
        buildPanel.MotorRelationSummary.ShouldBe("3 motor relations can twist");
    }

    [Fact]
    public void BuildPanel_WhenValidAnatomyHasNoMotorRelations_DisablesTrainingWithFlexibleJointReason()
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef(
                [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(56, 0), 18)],
                [new BeamDef(0, 1)],
                [new CoreDef(0)]));
        var presentation = new BuildPresentationViewModel(build);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeFalse();
        buildPanel.ReadinessText.ShouldBe("Add a two-beam node");
        buildPanel.InputSummary.ShouldBe("1 core: 6 sensors; 0 motor relations: 0 sensors; 6 inputs total");
        buildPanel.MotorRelationSummary.ShouldBe("0 motor relations can twist");
    }

    [Fact]
    public void PresentationChanged_WhenAnatomyChanges_RaisesForLiveBuildScreenRefresh()
    {
        var build = new BuildViewModel();
        var presentation = new BuildPresentationViewModel(build);
        var raiseCount = 0;
        presentation.PresentationChanged += (_, _) => raiseCount++;

        build.PlaceNode(new Vector2D(0, 0), 18);

        raiseCount.ShouldBe(1);
    }
}
