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
    public void EditMode_LocksTopologyTools()
    {
        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(
            Guid.NewGuid(),
            "Worm",
            PairCreature(),
            TestTraining.State(3, 1, TestTraining.Run)));
        var presentation = new BuildPresentationViewModel(build);

        presentation.LockTopologyTools.ShouldBeTrue();
        presentation.InspectorRole.ShouldBe("Tool: Move");
        presentation.InspectorValues.ShouldBe("Drag an existing node to reposition it. Training is kept.");
        presentation.IsLocked.ShouldBeTrue();
        presentation.IsTrained.ShouldBeTrue();
    }

    [Fact]
    public void Unlock_OpensTheBodyForEditing_AndKeepsTheTraining()
    {
        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(Guid.NewGuid(), "Worm", PairCreature(), TestTraining.State(3)));
        var presentation = new BuildPresentationViewModel(build);
        var changes = 0;
        presentation.PresentationChanged += (_, _) => changes++;

        build.Unlock();

        presentation.IsLocked.ShouldBeFalse();
        presentation.LockTopologyTools.ShouldBeFalse();
        presentation.IsTrained.ShouldBeTrue();
        build.TrainingGeneration.ShouldBe(3);
        changes.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void LatestDistanceText_ShowsTheLatestGenerationNotTheBestEver()
    {
        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(
            Guid.NewGuid(),
            "Worm",
            PairCreature(),
            TestTraining.State(3, bestDistance: 400, new TrainingRunDef(250, 1, 0, MapIds.Flat, frontDistance: 270))));

        new BuildPresentationViewModel(build).LatestDistanceText.ShouldBe("Latest distance 2.7 m");
    }

    [Theory]
    [InlineData(1, "Trained 1 generation")]
    [InlineData(12, "Trained 12 generations")]
    public void TrainingSummaryTitle_CountsGenerations(int generation, string expected)
    {
        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(Guid.NewGuid(), "Worm", PairCreature(), TestTraining.State(generation)));

        new BuildPresentationViewModel(build).TrainingSummaryTitle.ShouldBe(expected);
    }

    [Fact]
    public void ResetTrainingWarning_NamesTheTrainingThatIsLost()
    {
        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(Guid.NewGuid(), "Worm", PairCreature(), TestTraining.State(12)));

        new BuildPresentationViewModel(build).ResetTrainingWarning.ShouldBe(
            "Worm forgets its 12 generations of training and keeps its body. Copy it first to keep the trained one.");
    }

    [Fact]
    public void SelectedBeam_ShowsNameEndsAndLengthGuidance()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(3, 4))],
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
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(3, 4))],
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
                [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(3, 4))],
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
                new NodeDef(1, new Vector2D(0, 0)),
                new NodeDef(2, new Vector2D(20, 5), "Knee"),
                new NodeDef(3, new Vector2D(40, 0)),
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
        build.Load(new CreatureDef([new NodeDef(1, new Vector2D(0, 0))], [], []));
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
            TestTraining.State(3, 1, TestTraining.Run)));
        build.ToggleSelectedNode(build.Nodes[0].Id);

        new BuildPresentationViewModel(build).SinglePart!.CanDelete.ShouldBeFalse();
    }

    [Fact]
    public void NoOrManySelected_HasNoPartSettings()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(3, 4))],
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
            TestTraining.State(3, 1, TestTraining.Run)));
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
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(50, 0)), new NodeDef(3, new Vector2D(100, 0))],
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
        build.PlaceNode(new Vector2D(0, 0));
        var presentation = new BuildPresentationViewModel(build);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeFalse();
        buildPanel.ReadinessText.ShouldBe("1 node not connected");
    }

    [Fact]
    public void BuildPanel_WithAPiston_IsReadyToTrain()
    {
        var build = new BuildViewModel();
        build.Load(PistonCreature());
        var presentation = new BuildPresentationViewModel(build);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeTrue();
        buildPanel.ReadinessText.ShouldBe("Ready to train");
    }

    [Fact]
    public void BuildPanel_WithoutAPiston_AsksForOne()
    {
        var build = new BuildViewModel();
        build.Load(PairCreature());
        var presentation = new BuildPresentationViewModel(build);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeFalse();
        buildPanel.ReadinessText.ShouldBe("Add a piston");
    }

    [Fact]
    public void PresentationChanged_WhenAnatomyChanges_RaisesForLiveBuildScreenRefresh()
    {
        var build = new BuildViewModel();
        var presentation = new BuildPresentationViewModel(build);
        var raiseCount = 0;
        presentation.PresentationChanged += (_, _) => raiseCount++;

        build.PlaceNode(new Vector2D(0, 0));

        raiseCount.ShouldBe(1);
    }

    private static CreatureDef PairCreature() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0))],
        [new BeamDef(101, 1, 2)],
        [new SensorDef(201, 101, SensorKind.Accelerometer)]);

    private static CreatureDef PistonCreature() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 0))],
        [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
        [new SensorDef(201, 101, SensorKind.Accelerometer)],
        [new PistonDef(301, 1, 3)]);
}
