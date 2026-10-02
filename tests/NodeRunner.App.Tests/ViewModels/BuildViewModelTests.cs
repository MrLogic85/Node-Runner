using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildViewModelTests
{
    private static readonly CanvasRect _area = BuildViewModel.BuildArea;

    [Fact]
    public void PlaceNode_OutsideTheBuildArea_LandsWithItsDiscJustInside()
    {
        var viewModel = new BuildViewModel();

        viewModel.PlaceNode(new Vector2D(_area.Max.X + 500, _area.Min.Y - 500), 18);

        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(_area.Max.X - 18, _area.Min.Y + 18));
    }

    [Fact]
    public void MoveNode_PastTheBuildArea_StopsAtTheEdge()
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0), 18);

        viewModel.MoveNode(1, new Vector2D(_area.Min.X - 500, 40));

        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(_area.Min.X + 18, 40));
    }

    [Fact]
    public void TranslateSelection_PastTheBuildArea_StopsTheWholeGroupAndKeepsItsShape()
    {
        var viewModel = SelectedPair();

        viewModel.TranslateSelection(viewModel.SnapshotSelection(), new Vector2D(_area.Max.X, 30));

        viewModel.Nodes[1].Position.ShouldBe(new Vector2D(_area.Max.X - 18, 30));
        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(_area.Max.X - 118, 30));
    }

    [Fact]
    public void TranslateSelection_WithFractionalCoordinates_StillReachesTheEdge()
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(0.1, 0), 14);
        viewModel.PlaceNode(new Vector2D(100.1, 0), 14);
        viewModel.ReplaceSelection([1, 2]);

        viewModel.TranslateSelection(viewModel.SnapshotSelection(), new Vector2D(1500.3, 0));

        viewModel.Nodes[1].Position.X.ShouldBe(_area.Max.X - 14);
        viewModel.Nodes[0].Position.X.ShouldBe(_area.Max.X - 114, 1e-9);
    }

    [Fact]
    public void SnapshotSelection_PivotsOnTheCentreOfTheJointsBounds()
    {
        var viewModel = SelectedPair();

        viewModel.SnapshotSelection().Pivot.ShouldBe(new Vector2D(50, 0));
    }

    [Theory]
    [InlineData(Math.PI / 2)]
    [InlineData(-Math.PI / 2)]
    [InlineData(3 * Math.PI / 2)]
    public void RotateSelection_TurnsAboutThePivotAndKeepsDistances(double radians)
    {
        var viewModel = SelectedPair();
        var start = viewModel.SnapshotSelection();

        viewModel.RotateSelection(start, radians);

        var turned = new Vector2D(50, -50 * Math.Sin(radians));
        viewModel.Nodes[0].Position.X.ShouldBe(turned.X, 1e-9);
        viewModel.Nodes[0].Position.Y.ShouldBe(turned.Y, 1e-9);
        viewModel.Nodes[1].Position.Y.ShouldBe(-turned.Y, 1e-9);
    }

    [Fact]
    public void RotateSelection_FromTheSnapshot_DoesNotDrift()
    {
        var viewModel = SelectedPair();
        var start = viewModel.SnapshotSelection();

        for (var i = 0; i < 1000; i++)
        {
            viewModel.RotateSelection(start, i * 0.37);
        }

        viewModel.RotateSelection(start, 0);

        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        viewModel.Nodes[1].Position.ShouldBe(new Vector2D(100, 0));
    }

    [Fact]
    public void RotateSelection_ThatWouldLeaveTheBuildArea_IsIgnored()
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(_area.Max.X - 18, -100), 18);
        viewModel.PlaceNode(new Vector2D(_area.Max.X - 18, 100), 18);
        viewModel.ReplaceSelection([1, 2]);
        var start = viewModel.SnapshotSelection();

        viewModel.RotateSelection(start, Math.PI / 4);

        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(_area.Max.X - 18, -100));
        viewModel.Nodes[1].Position.ShouldBe(new Vector2D(_area.Max.X - 18, 100));
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(0.5, 0.5)]
    [InlineData(100, BuildViewModel.MaxSelectionScale)]
    [InlineData(-3, BuildViewModel.MinSelectionScale)]
    public void ScaleSelection_SpreadsFromThePivotWithinTheClamps(double factor, double applied)
    {
        var viewModel = SelectedPair();

        viewModel.ScaleSelection(viewModel.SnapshotSelection(), factor);

        viewModel.Nodes[0].Position.X.ShouldBe(50 - (50 * applied), 1e-9);
        viewModel.Nodes[1].Position.X.ShouldBe(50 + (50 * applied), 1e-9);
    }

    [Fact]
    public void ScaleSelection_OfCoincidentJoints_LeavesThemInPlace()
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(10, 10), 18);
        viewModel.PlaceNode(new Vector2D(10, 10), 18);
        viewModel.ReplaceSelection([1, 2]);

        viewModel.ScaleSelection(viewModel.SnapshotSelection(), 3);

        viewModel.Nodes.ShouldAllBe(node => node.Position == new Vector2D(10, 10));
    }

    [Fact]
    public void ScaleSelection_WithANonFiniteFactor_Throws()
    {
        var viewModel = SelectedPair();

        Should.Throw<ArgumentOutOfRangeException>(() => viewModel.ScaleSelection(viewModel.SnapshotSelection(), double.NaN));
    }

    [Fact]
    public void ScaleSelection_WhenLocked_StillSpreadsTheJoints()
    {
        var viewModel = LockedPair();
        viewModel.ReplaceSelection([1, 2]);

        viewModel.ScaleSelection(viewModel.SnapshotSelection(), 2);

        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(-10, 0));
        viewModel.Nodes[1].Position.ShouldBe(new Vector2D(30, 0));
    }

    [Fact]
    public void RotateSelection_WhenLocked_Turns()
    {
        var viewModel = LockedPair();
        viewModel.ReplaceSelection([1, 2]);

        viewModel.RotateSelection(viewModel.SnapshotSelection(), Math.PI);

        viewModel.Nodes[0].Position.X.ShouldBe(20, 1e-9);
    }

    [Fact]
    public void RestoreSelection_PutsTheJointsBack()
    {
        var viewModel = SelectedPair();
        var start = viewModel.SnapshotSelection();
        viewModel.ScaleSelection(start, 2);

        viewModel.RestoreSelection(start);

        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        viewModel.Nodes[1].Position.ShouldBe(new Vector2D(100, 0));
    }

    private static BuildViewModel SelectedPair()
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0), 18);
        viewModel.PlaceNode(new Vector2D(100, 0), 18);
        viewModel.ReplaceSelection([1, 2]);
        return viewModel;
    }

    [Fact]
    public void IsActive_DefaultsToFalse()
    {
        var viewModel = new BuildViewModel();

        viewModel.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void IsActive_WhenChanged_RaisesPropertyChanged()
    {
        var viewModel = new BuildViewModel();
        var raisedFor = new List<string?>();
        viewModel.PropertyChanged += (_, args) => raisedFor.Add(args.PropertyName);

        viewModel.IsActive = true;

        raisedFor.ShouldContain(nameof(BuildViewModel.IsActive));
    }

    [Fact]
    public void IsActive_WhenSetToSameValue_DoesNotRaisePropertyChanged()
    {
        var viewModel = new BuildViewModel();
        var raiseCount = 0;
        viewModel.PropertyChanged += (_, _) => raiseCount++;

        viewModel.IsActive = false;

        raiseCount.ShouldBe(0);
    }

    [Fact]
    public void PlaceNode_AddsNodeAndRaisesAnatomyChanged()
    {
        var viewModel = new BuildViewModel();
        var raised = false;
        viewModel.AnatomyChanged += (_, _) => raised = true;

        var id = viewModel.PlaceNode(new Vector2D(3, 4), 18);

        id.ShouldBe(1);
        viewModel.Nodes.Count.ShouldBe(1);
        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(3, 4));
        raised.ShouldBeTrue();
    }

    [Fact]
    public void MoveNode_UpdatesPositionAndRaisesAnatomyChanged()
    {
        var viewModel = new BuildViewModel();
        var id = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var raised = false;
        viewModel.AnatomyChanged += (_, _) => raised = true;

        viewModel.MoveNode(id, new Vector2D(10, 20));

        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(10, 20));
        raised.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void LoadCreation_IsMoveOnlyExactlyWhenLocked(int? generation, bool moveOnly)
    {
        var training = generation is { } trained ? TestTraining.State(trained, 1, TestTraining.Run) : null;
        var viewModel = new BuildViewModel();

        viewModel.LoadCreation(new CreationDef(Guid.NewGuid(), "Worm", TwoNodeCreature(), training));

        viewModel.IsMoveOnly.ShouldBe(moveOnly);
    }

    [Fact]
    public void LoadMoveOnly_AllowsMovingExistingNodesButRejectsTopologyChanges()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(20, 0), 18)],
            [new BeamDef(101, 1, 2)],
            []);
        var viewModel = new BuildViewModel();

        viewModel.Load(creature, moveOnly: true);
        viewModel.MoveNode(1, new Vector2D(5, 5));

        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(5, 5));
        viewModel.IsMoveOnly.ShouldBeTrue();
        viewModel.Beams.Count.ShouldBe(1);
        Action action = () => viewModel.PlaceNode(new Vector2D(30, 0), 18);

        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Load_ResetsActiveToolToMove()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(20, 0), 18)],
            [new BeamDef(101, 1, 2)],
            []);
        var viewModel = new BuildViewModel { ActiveTool = BuildTool.Beam };

        viewModel.Load(creature);

        viewModel.ActiveTool.ShouldBe(BuildTool.Move);
    }

    [Fact]
    public void LoadMoveOnly_WithSavedCreationMetadata_ExposesNameAndTrainingGeneration()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(20, 0), 18)],
            [new BeamDef(101, 1, 2)],
            []);
        var training = TestTraining.State(9, 1, TestTraining.Run);
        var viewModel = new BuildViewModel();

        viewModel.Load(creature, moveOnly: true, creationName: "Worm", training: training);

        viewModel.CreationName.ShouldBe("Worm");
        viewModel.TrainingGeneration.ShouldBe(9);
    }

    [Fact]
    public void SetCreationName_ChangesTheName()
    {
        var viewModel = new BuildViewModel();

        viewModel.SetCreationName("Hopper");

        viewModel.CreationName.ShouldBe("Hopper");
    }

    [Fact]
    public void Load_WithoutName_IsUntitled()
    {
        var viewModel = new BuildViewModel();

        viewModel.Load(new CreatureDef([], [], []));

        viewModel.CreationName.ShouldBe(NewCreationWorkflow.UntitledName);
    }

    [Fact]
    public void TryFindNodeNear_WithNoNodes_ReturnsFalse()
    {
        var viewModel = new BuildViewModel();

        var found = viewModel.TryFindNodeNear(new Vector2D(0, 0), 10, out var nodeIndex);

        found.ShouldBeFalse();
        nodeIndex.ShouldBe(-1);
    }

    [Fact]
    public void TryFindNodeNear_WithinDistance_ReturnsClosestNode()
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var closeIndex = viewModel.PlaceNode(new Vector2D(5, 0), 18);
        viewModel.PlaceNode(new Vector2D(100, 100), 18);

        var found = viewModel.TryFindNodeNear(new Vector2D(6, 0), 10, out var nodeIndex);

        found.ShouldBeTrue();
        nodeIndex.ShouldBe(closeIndex);
    }

    [Fact]
    public void TryFindNodeNear_BeyondDistance_ReturnsFalse()
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0), 18);

        var found = viewModel.TryFindNodeNear(new Vector2D(100, 100), 10, out var nodeIndex);

        found.ShouldBeFalse();
        nodeIndex.ShouldBe(-1);
    }

    [Fact]
    public void ActiveTool_DefaultsToMove()
    {
        var viewModel = new BuildViewModel();

        viewModel.ActiveTool.ShouldBe(BuildTool.Move);
    }


    [Fact]
    public void ConnectBeam_DifferentNodes_CreatesBeam()
    {
        var viewModel = new BuildViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        var raised = false;
        viewModel.AnatomyChanged += (_, _) => raised = true;

        viewModel.ConnectBeam(a, b);

        viewModel.Beams.Count.ShouldBe(1);
        viewModel.Beams[0].NodeA.ShouldBe(a);
        viewModel.Beams[0].NodeB.ShouldBe(b);
        raised.ShouldBeTrue();
    }

    [Fact]
    public void ConnectBeam_SameNode_CreatesNoBeam()
    {
        var viewModel = new BuildViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);

        viewModel.ConnectBeam(a, a).ShouldBeFalse();

        viewModel.Beams.Count.ShouldBe(0);
    }

    [Fact]
    public void ConnectBeam_DuplicateBeam_SurfacesErrorInsteadOfThrowing()
    {
        var viewModel = new BuildViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        viewModel.ConnectBeam(a, b);

        viewModel.ConnectBeam(a, b);

        viewModel.Beams.Count.ShouldBe(1);
        viewModel.StatusMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ConnectBeam_WhenLocked_CreatesNoBeam()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(
            new CreatureDef([new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(20, 0), 18)], [], []),
            moveOnly: true);

        viewModel.CanConnect(1, 2).ShouldBeFalse();
        viewModel.ConnectBeam(1, 2).ShouldBeFalse();

        viewModel.Beams.ShouldBeEmpty();
        viewModel.StatusMessage.ShouldBe("Edit mode only allows moving existing nodes.");
    }


    [Theory]
    [InlineData(-10, 0)]
    [InlineData(110, 0)]
    public void SplitBeam_AtAnEnd_ChangesNothing(double x, double y)
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0), 18);
        viewModel.PlaceNode(new Vector2D(100, 0), 18);
        viewModel.ConnectBeam(1, 2);

        viewModel.SplitBeam(viewModel.Beams[0].Id, new Vector2D(x, y), 18).ShouldBeNull();

        viewModel.Nodes.Count.ShouldBe(2);
        viewModel.Beams.Count.ShouldBe(1);
    }

    [Fact]
    public void SplitBeam_WhenLocked_ChangesNothing()
    {
        var viewModel = LockedPair();

        viewModel.SplitBeam(viewModel.Beams[0].Id, new Vector2D(10, 0), 18).ShouldBeNull();

        viewModel.Nodes.Count.ShouldBe(2);
        viewModel.Beams.Count.ShouldBe(1);
    }

    private static BuildViewModel LockedPair()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(
            new CreatureDef([new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(20, 0), 18)], [new BeamDef(101, 1, 2)], []),
            moveOnly: true);
        return viewModel;
    }




    [Fact]
    public void TryFindBeamNear_WithinDistance_ReturnsBeam()
    {
        var viewModel = new BuildViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        viewModel.ConnectBeam(a, b);

        var found = viewModel.TryFindBeamNear(new Vector2D(5, 0), 2, out var beamId);

        found.ShouldBeTrue();
        beamId.ShouldBe(viewModel.Beams[0].Id);
    }

    [Fact]
    public void TryFindBeamNear_BeyondDistance_ReturnsFalse()
    {
        var viewModel = new BuildViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        viewModel.ConnectBeam(a, b);

        var found = viewModel.TryFindBeamNear(new Vector2D(5, 50), 2, out var beamIndex);

        found.ShouldBeFalse();
        beamIndex.ShouldBe(-1);
    }

    [Fact]
    public void SelectBeam_ReplacesNodeSelectionAndExposesBeamAsSelectedPart()
    {
        var viewModel = new BuildViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        viewModel.ConnectBeam(a, b);
        viewModel.ToggleSelectedNode(a);
        var raisedFor = new List<string?>();
        var anatomyChanged = false;
        viewModel.PropertyChanged += (_, args) => raisedFor.Add(args.PropertyName);
        viewModel.AnatomyChanged += (_, _) => anatomyChanged = true;

        viewModel.SelectBeam(viewModel.Beams[0].Id);

        viewModel.SelectedNodeCount.ShouldBe(0);
        viewModel.SelectedBeamCount.ShouldBe(1);
        viewModel.SelectedPartCount.ShouldBe(1);
        viewModel.SingleSelectedBeamId.ShouldBe(viewModel.Beams[0].Id);
        raisedFor.ShouldContain(nameof(BuildViewModel.SelectedBeamCount));
        raisedFor.ShouldContain(nameof(BuildViewModel.SelectedPartCount));
        raisedFor.ShouldContain(nameof(BuildViewModel.SingleSelectedBeamId));
        anatomyChanged.ShouldBeTrue();
    }

    [Fact]
    public void ClearSelection_WithOnlyBeamSelected_ClearsBeam()
    {
        var viewModel = new BuildViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        viewModel.ConnectBeam(a, b);
        viewModel.SelectBeam(viewModel.Beams[0].Id);
        var raisedFor = new List<string?>();
        var anatomyChanged = false;
        viewModel.PropertyChanged += (_, args) => raisedFor.Add(args.PropertyName);
        viewModel.AnatomyChanged += (_, _) => anatomyChanged = true;

        viewModel.ClearSelection();

        viewModel.SelectedPartCount.ShouldBe(0);
        viewModel.SingleSelectedBeamId.ShouldBeNull();
        raisedFor.ShouldContain(nameof(BuildViewModel.SelectedBeamCount));
        raisedFor.ShouldContain(nameof(BuildViewModel.SelectedPartCount));
        raisedFor.ShouldContain(nameof(BuildViewModel.SingleSelectedBeamId));
        anatomyChanged.ShouldBeTrue();
    }

    [Fact]
    public void DeleteSelectedParts_WithBeamSelected_RemovesBeamButKeepsNodes()
    {
        var viewModel = new BuildViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        viewModel.ConnectBeam(a, b);
        viewModel.SelectBeam(viewModel.Beams[0].Id);

        viewModel.DeleteSelectedParts();

        viewModel.Beams.ShouldBeEmpty();
        viewModel.Nodes.Count.ShouldBe(2);
        viewModel.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void SelectSensor_ReplacesAnyOtherSelection_AndDeleteRemovesOnlyTheSensor()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(SensorCreature());
        viewModel.ReplaceSelection([1]);

        viewModel.SelectSensor(5);

        viewModel.SelectedPartCount.ShouldBe(1);
        viewModel.SingleSelectedSensorId.ShouldBe(5);
        viewModel.StatusMessage.ShouldBe("Camera selected.");

        viewModel.DeleteSelectedParts();

        viewModel.Sensors.Select(sensor => sensor.Id).ShouldBe([4]);
        viewModel.Beams.Count.ShouldBe(2);
        viewModel.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void SelectingAJointOrBeam_DropsTheSelectedSensor()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(SensorCreature());
        viewModel.SelectSensor(4);

        viewModel.ToggleSelectedNode(1);
        viewModel.SingleSelectedSensorId.ShouldBeNull();
        viewModel.SelectedPartCount.ShouldBe(1);

        viewModel.SelectSensor(4);
        viewModel.SelectBeam(3);
        viewModel.SingleSelectedSensorId.ShouldBeNull();
        viewModel.SelectedPartCount.ShouldBe(1);
    }

    [Fact]
    public void TryFindSensorAt_HitsOnlyThePicture()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(SensorCreature());

        viewModel.TryFindSensorAt(new Vector2D(50, 0), out var accelerometer).ShouldBeTrue();
        accelerometer.ShouldBe(4);
        viewModel.TryFindSensorAt(new Vector2D(100, 50), out var camera).ShouldBeTrue();
        camera.ShouldBe(5);
        viewModel.TryFindSensorAt(new Vector2D(50, SensorPicture.AccelerometerSize), out _).ShouldBeFalse();
    }

    private static CreatureDef SensorCreature() => new(
        [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(100, 0), 18), new NodeDef(6, new Vector2D(100, 100), 18)],
        [new BeamDef(3, 1, 2), new BeamDef(7, 2, 6)],
        [new SensorDef(4, 3, SensorKind.Accelerometer), new SensorDef(5, 7, SensorKind.Camera)]);

    [Fact]
    public void DeleteSelectedParts_InMoveOnlyMode_PreservesAnatomy()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(10, 0), 18)],
                [new BeamDef(101, 1, 2)],
                []),
            moveOnly: true);
        viewModel.SelectBeam(viewModel.Beams[0].Id);

        viewModel.DeleteSelectedParts();

        viewModel.Beams.Count.ShouldBe(1);
        viewModel.StatusMessage.ShouldBe("Edit mode can only move selected parts.");
    }



    [Fact]
    public void TryLeave_WithNoNodesPlaced_ReturnsTrue()
    {
        var viewModel = new BuildViewModel();

        var canLeave = viewModel.TryLeave(out var creature, out var errors);

        canLeave.ShouldBeTrue();
        creature.ShouldBeNull();
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void TryLeave_WithUnconnectedNode_ReturnsFalseWithErrors()
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0), 18);

        var canLeave = viewModel.TryLeave(out var creature, out var errors);

        canLeave.ShouldBeFalse();
        creature.ShouldBeNull();
        errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void Snapshot_WithUnconnectedNode_ReturnsTheDrawingForSaving()
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0), 18);

        var creature = viewModel.Snapshot();

        creature.Nodes.Count.ShouldBe(1);
        creature.Beams.ShouldBeEmpty();
    }

    [Fact]
    public void TryGetTrainableCreature_WithUnconnectedNode_RefusesAndSaysWhy()
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0), 18);

        viewModel.TryGetTrainableCreature(out var creature).ShouldBeFalse();

        creature.ShouldBeNull();
        viewModel.StatusMessage.ShouldNotBeNull();
        viewModel.StatusMessage.ShouldContain("nothing attached");
    }

    [Fact]
    public void TryGetTrainableCreature_WithNothingToDrive_Refuses()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(TwoNodeCreature());

        viewModel.TryGetTrainableCreature(out var creature).ShouldBeFalse();

        creature.ShouldBeNull();
    }

    [Fact]
    public void TryGetTrainableCreature_WithAMotorRelation_ReturnsTheCreature()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(90, 0), 18), new NodeDef(3, new Vector2D(180, 20), 18)],
            [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
            []));

        viewModel.TryGetTrainableCreature(out var creature).ShouldBeTrue();

        creature.ShouldNotBeNull();
        creature.Beams.Count.ShouldBe(2);
    }

    [Fact]
    public void TryLeave_WithValidCreature_ReturnsTrue()
    {
        var viewModel = new BuildViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(90, 0), 18);
        viewModel.ConnectBeam(a, b);

        var canLeave = viewModel.TryLeave(out var creature, out var errors);

        canLeave.ShouldBeTrue();
        creature.ShouldNotBeNull();
        creature.Nodes.Count.ShouldBe(2);
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void SetBlockedLeaveMessage_SetsStatusMessage()
    {
        var viewModel = new BuildViewModel();

        viewModel.SetBlockedLeaveMessage(["Add at least one node before training this creation."]);

        viewModel.StatusMessage.ShouldNotBeNullOrEmpty();
        viewModel.StatusMessage!.ShouldContain("Add at least one node");
    }

    private static CreatureDef TwoNodeCreature() => new(
        [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(20, 0), 18)],
        [new BeamDef(3, 1, 2)],
        []);

    [Fact]
    public void CanvasNotes_NameEachBeamTooShortToTrain()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(50, 0), 18), new NodeDef(3, new Vector2D(150, 0), 18), new NodeDef(4, new Vector2D(150, 0), 18)],
            [new BeamDef(5, 1, 2), new BeamDef(6, 2, 3), new BeamDef(7, 3, 4)],
            []));

        viewModel.CanvasNotes().ShouldBe(
        [
            new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Beam, 5), "Too short"),
        ]);
    }

    [Fact]
    public void SetCameraAim_TurnsTheCameraAndRedrawsOnlyOnAChange()
    {
        var build = new BuildViewModel();
        build.Load(CameraPair(), moveOnly: false);
        var changes = 0;
        build.AnatomyChanged += (_, _) => changes++;

        build.SetCameraAim(4, 1);
        build.SetCameraAim(4, 1);

        build.Sensors[0].Aim.ShouldBe(1);
        changes.ShouldBe(1);
    }

    [Fact]
    public void SetCameraAim_WhenLocked_TurnsTheCamera()
    {
        var build = new BuildViewModel();
        build.Load(CameraPair(), moveOnly: true);

        build.SetCameraAim(4, 1);

        build.Sensors[0].Aim.ShouldBe(1);
    }

    private static CreatureDef CameraPair() => new(
        [new NodeDef(1, new Vector2D(0, 0), 18), new NodeDef(2, new Vector2D(100, 0), 18)],
        [new BeamDef(3, 1, 2)],
        [new SensorDef(4, 3, SensorKind.Camera)]);
}
