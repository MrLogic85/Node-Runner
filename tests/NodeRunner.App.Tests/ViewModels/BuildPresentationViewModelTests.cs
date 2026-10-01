using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildPresentationViewModelTests
{
    [Theory]
    [InlineData(BuildTool.Move, "Drag a joint to move it. Tap a part to select it.")]
    [InlineData(BuildTool.Beam, "Drag from one joint to another to join them with a beam.")]
    [InlineData(BuildTool.Joint, "Tap empty space to add a joint, or tap a beam to split it.")]
    [InlineData(BuildTool.Select, "Tap parts to select them. Drag selected parts to move them together.")]
    public void ToolHint_ReturnsUserFacingHintForTool(BuildTool tool, string expected)
    {
        BuildPresentationViewModel.ToolHint(tool).ShouldBe(expected);
    }

    [Fact]
    public void EditMode_LocksTopologyToolsAndShowsRebuildAction()
    {
        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(
            Guid.NewGuid(),
            "Worm",
            PairCreature(),
            new TrainingStateDef([2, 1], [0.1, -0.2, 0.3], 3, "Tanh")));
        var presentation = new BuildPresentationViewModel(build);

        presentation.LockTopologyTools.ShouldBeTrue();
        presentation.InspectorRole.ShouldBe("Tool: Move");
        presentation.InspectorValues.ShouldBe("Drag an existing node to reposition it. Training is kept.");
        presentation.IsLocked.ShouldBeTrue();
        presentation.ShowRebuildAction.ShouldBeTrue();
    }

    [Fact]
    public void SelectedBeam_ShowsLengthEndpointsAndFixedStructureFacts()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(3, 4), 18)],
            [new BeamDef(101, 1, 2)],
            []));
        build.SelectBeam(101);
        var presentation = new BuildPresentationViewModel(build);

        presentation.SinglePartTitle.ShouldBe("Beam 1");
        presentation.SinglePartPrimaryLabel.ShouldBe("Length");
        presentation.SinglePartPrimaryValue.ShouldBe("5.0 units");
        presentation.SinglePartConnectionsLabel.ShouldBe("Between");
        presentation.SinglePartConnectionsValue.ShouldBe("Node 1 ↔ Node 2");
        presentation.SinglePartFacts.ShouldBe("Rigid connection");
    }

    [Fact]
    public void SelectedNode_ShowsPositionRadiusAndConnectedBeams()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0), 18),
                new NodeDef(2, new Vector2D(20, 5), 12),
                new NodeDef(3, new Vector2D(40, 0), 18),
            ],
            [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
            []));
        build.ToggleSelectedNode(2);
        var presentation = new BuildPresentationViewModel(build);

        presentation.SelectedPartCount.ShouldBe(1);
        presentation.SinglePartTitle.ShouldBe("Node 2");
        presentation.SinglePartPrimaryValue.ShouldBe("20, 5");
        presentation.SinglePartConnectionsValue.ShouldBe("Beam 1 · Beam 2");
        presentation.SinglePartFacts.ShouldBe("Radius 12.0 · 2 attached Beam(s)");
    }

    [Fact]
    public void MultiSelection_SummarizesSelectedNodes()
    {
        var build = new BuildViewModel();
        build.Load(PairCreature());
        build.ToggleSelectedNode(1);
        build.ToggleSelectedNode(2);
        var presentation = new BuildPresentationViewModel(build);

        presentation.SelectedPartCount.ShouldBe(2);
        presentation.MultiSelectionTitle.ShouldBe("2 selected");
        presentation.MultiSelectionCounts.ShouldBe("Nodes · 2");
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
        buildPanel.InputSummary.ShouldBe("0 sensors placed; fix anatomy to count inputs.");
        buildPanel.MotorRelationSummary.ShouldBe("Fix anatomy to count motor relations.");
    }

    [Fact]
    public void BuildPanel_WhenAnatomyIsValid_SummarizesInputsAndMotorRelations()
    {
        var build = new BuildViewModel();
        build.Load(WormCreature(sensorCount: 1));
        var presentation = new BuildPresentationViewModel(build);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeTrue();
        buildPanel.ReadinessText.ShouldBe("Ready to train");
        buildPanel.InputSummary.ShouldBe("1 sensor: 2 inputs; 3 motor relations: 6 inputs; 8 inputs total");
        buildPanel.InputCount.ShouldBe(8);
        buildPanel.MotorRelationSummary.ShouldBe("3 motor relations can twist");
    }

    [Fact]
    public void BuildPanel_WithAccelerometerAndLosSensor_CountsInputsPerSensorKind()
    {
        var build = new BuildViewModel();
        build.Load(WormCreature(sensorCount: 2));
        var presentation = new BuildPresentationViewModel(build);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeTrue();
        buildPanel.InputSummary.ShouldBe("2 sensors: 5 inputs; 3 motor relations: 6 inputs; 11 inputs total");
        buildPanel.InputCount.ShouldBe(11);
    }

    [Fact]
    public void BuildPanel_WhenValidAnatomyHasNoMotorRelations_DisablesTrainingWithFlexibleJointReason()
    {
        var build = new BuildViewModel();
        build.Load(PairCreature());
        var presentation = new BuildPresentationViewModel(build);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeFalse();
        buildPanel.ReadinessText.ShouldBe("Add a two-beam node");
        buildPanel.InputSummary.ShouldBe("1 sensor: 2 inputs; 0 motor relations: 0 inputs; 2 inputs total");
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

    private static CreatureDef PairCreature() => new(
        [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(56, 0), 18)],
        [new BeamDef(101, 1, 2)],
        [new SensorDef(201, 101, SensorKind.Accelerometer)]);

    private static CreatureDef WormCreature(int sensorCount)
    {
        var sensors = sensorCount == 1
            ? new[] { new SensorDef(201, 101, SensorKind.Accelerometer) }
            : [new SensorDef(201, 101, SensorKind.Accelerometer), new SensorDef(202, 102, SensorKind.LineOfSight)];
        return new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0), 18),
                new NodeDef(2, new Vector2D(56, 0), 18),
                new NodeDef(3, new Vector2D(112, 0), 18),
                new NodeDef(4, new Vector2D(168, 0), 18),
                new NodeDef(5, new Vector2D(224, 0), 18),
            ],
            [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3), new BeamDef(103, 3, 4), new BeamDef(104, 4, 5)],
            sensors);
    }
}
