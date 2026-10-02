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
            new TrainingStateDef([2, 1], [0.1, -0.2, 0.3], 3, "Tanh", 1, TestTraining.Run)));
        var presentation = new BuildPresentationViewModel(build);

        presentation.LockTopologyTools.ShouldBeTrue();
        presentation.InspectorRole.ShouldBe("Tool: Move");
        presentation.InspectorValues.ShouldBe("Drag an existing node to reposition it. Training is kept.");
        presentation.IsLocked.ShouldBeTrue();
        presentation.ShowRebuildAction.ShouldBeTrue();
    }

    [Fact]
    public void SelectedBeam_ShowsNameEndsAndLengthGuidance()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(3, 4), 18)],
            [new BeamDef(101, 1, 2)],
            []));
        build.SelectBeam(101);
        var presentation = new BuildPresentationViewModel(build);

        presentation.SinglePart.ShouldBe(new PartSettingsPresentation(
            101,
            PartSettingsKind.Beam,
            "Beam 1",
            "Beam 1",
            "Between",
            "Node 1 ↔ Node 2",
            "Drag its ends to change the length.",
            CanDelete: true));
    }

    [Theory]
    [InlineData(SensorKind.Accelerometer, PartSettingsKind.Accelerometer, "Accelerometer", "Feels how its beam speeds up, slows down and tilts.")]
    [InlineData(SensorKind.Camera, PartSettingsKind.Camera, "Camera", "Three rays see how near the ground is. Drag the round handle to aim it.")]
    public void SelectedSensor_ShowsNameBeamAndWhatItFeels(SensorKind kind, PartSettingsKind partKind, string name, string note)
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(3, 4), 18)],
            [new BeamDef(101, 1, 2, "Thigh")],
            [new SensorDef(7, 101, kind)]));
        build.SelectSensor(7);
        var presentation = new BuildPresentationViewModel(build);

        presentation.SinglePart.ShouldBe(new PartSettingsPresentation(7, partKind, name, name, "On", "Thigh", note, CanDelete: true));
    }

    [Fact]
    public void SelectedCamera_WhenLocked_StillOffersToAimIt()
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(3, 4), 18)],
                [new BeamDef(101, 1, 2)],
                [new SensorDef(7, 101, SensorKind.Camera)]),
            moveOnly: true);
        build.SelectSensor(7);

        new BuildPresentationViewModel(build).SinglePart!.Note.ShouldBe($"Three rays see how near the ground is. {BuildPresentationViewModel.AimNote}");
    }

    [Fact]
    public void SelectedNode_ShowsNameAndTheBeamsThatMeetThere()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0), 18),
                new NodeDef(2, new Vector2D(20, 5), 12, "Knee"),
                new NodeDef(3, new Vector2D(40, 0), 18),
            ],
            [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3, "Shin")],
            []));
        build.ToggleSelectedNode(2);
        var presentation = new BuildPresentationViewModel(build);

        presentation.SinglePart.ShouldBe(new PartSettingsPresentation(
            2,
            PartSettingsKind.Node,
            "Knee",
            "Node 2",
            "Beams",
            "Beam 1 · Shin",
            "Beams meet and turn here. Drag it to move them.",
            CanDelete: true));
    }

    [Fact]
    public void SelectedNodeWithoutBeams_SaysNoneYet()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef([new NodeDef(1, new Vector2D(0, 0), 18)], [], []));
        build.ToggleSelectedNode(1);

        new BuildPresentationViewModel(build).SinglePart!.ConnectionsValue.ShouldBe("None yet");
    }

    [Fact]
    public void LockedCreation_PartSettingsHaveNoDelete()
    {
        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(
            Guid.NewGuid(),
            "Worm",
            PairCreature(),
            new TrainingStateDef([2, 1], [0.1, -0.2, 0.3], 3, "Tanh", 1, TestTraining.Run)));
        build.ToggleSelectedNode(build.Nodes[0].Id);

        new BuildPresentationViewModel(build).SinglePart!.CanDelete.ShouldBeFalse();
    }

    [Fact]
    public void NoOrManySelected_HasNoPartSettings()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(3, 4), 18)],
            [new BeamDef(101, 1, 2)],
            []));
        var presentation = new BuildPresentationViewModel(build);

        presentation.SinglePart.ShouldBeNull();
        build.ToggleSelectedNode(1);
        build.ToggleSelectedNode(2);
        presentation.SinglePart.ShouldBeNull();
    }

    [Theory]
    [InlineData(SensorKind.Accelerometer)]
    [InlineData(SensorKind.Camera)]
    public void SensorNote_AvoidsBrainWording(SensorKind kind)
    {
        var note = BuildPresentationViewModel.SensorNote(kind).ToLowerInvariant();

        foreach (var word in new[] { "brain", "port", "neuron", "input", "layer" })
        {
            note.ShouldNotContain(word);
        }
    }

    [Fact]
    public void Selection_CountsTheJointsAndOffersDelete()
    {
        var build = new BuildViewModel();
        build.Load(PairCreature());
        build.ToggleSelectedNode(1);
        build.ToggleSelectedNode(2);

        new BuildPresentationViewModel(build).Selection.ShouldBe(new SelectionPanelPresentation(
            "2 selected",
            "Delete 2",
            "Beams on a deleted node go with it.",
            CanDelete: true));
    }

    [Fact]
    public void Selection_OnALockedCreation_HasNoDelete()
    {
        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(
            Guid.NewGuid(),
            "Worm",
            PairCreature(),
            new TrainingStateDef([2, 1], [0.1, -0.2, 0.3], 3, "Tanh", 1, TestTraining.Run)));
        build.ReplaceSelection([1, 2]);

        new BuildPresentationViewModel(build).Selection!.CanDelete.ShouldBeFalse();
    }

    [Fact]
    public void Selection_NeedsSeveralParts()
    {
        var build = new BuildViewModel();
        build.Load(PairCreature());
        var presentation = new BuildPresentationViewModel(build);

        presentation.Selection.ShouldBeNull();
        build.ToggleSelectedNode(1);
        presentation.Selection.ShouldBeNull();
    }

    [Fact]
    public void BuildPanel_WhenBeamsAreTooShort_CountsThemInReadiness()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(50, 0), 18), new NodeDef(3, new Vector2D(100, 0), 18)],
            [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
            []));
        var presentation = new BuildPresentationViewModel(build);

        presentation.BuildPanel.CanStartTraining.ShouldBeFalse();
        presentation.BuildPanel.ReadinessText.ShouldBe("2 beams too short");
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
    public void BuildPanel_WithAccelerometerAndCamera_CountsInputsPerSensorKind()
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
        [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(90, 0), 18)],
        [new BeamDef(101, 1, 2)],
        [new SensorDef(201, 101, SensorKind.Accelerometer)]);

    private static CreatureDef WormCreature(int sensorCount)
    {
        var sensors = sensorCount == 1
            ? new[] { new SensorDef(201, 101, SensorKind.Accelerometer) }
            : [new SensorDef(201, 101, SensorKind.Accelerometer), new SensorDef(202, 102, SensorKind.Camera)];
        return new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0), 18),
                new NodeDef(2, new Vector2D(90, 0), 18),
                new NodeDef(3, new Vector2D(180, 0), 18),
                new NodeDef(4, new Vector2D(270, 0), 18),
                new NodeDef(5, new Vector2D(360, 0), 18),
            ],
            [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3), new BeamDef(103, 3, 4), new BeamDef(104, 4, 5)],
            sensors);
    }
}
