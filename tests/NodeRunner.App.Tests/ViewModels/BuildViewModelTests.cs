using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildViewModelTests
{
    private static readonly CanvasRect _area = BuildViewModel.BuildArea;

    [Fact]
    public void PlaceNode_OutsideTheBuildArea_LandsWithItsRingJustInside()
    {
        var viewModel = new BuildViewModel();

        viewModel.PlaceNode(new Vector2D(_area.Max.X + 500, _area.Min.Y - 500));

        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(_area.Max.X - NodeDef.PlainJointRadius, _area.Min.Y + NodeDef.PlainJointRadius));
    }

    [Fact]
    public void MoveNode_PastTheBuildArea_StopsAtTheEdge()
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0));

        viewModel.MoveNode(1, new Vector2D(_area.Min.X - 500, 40));

        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(_area.Min.X + NodeDef.PlainJointRadius, 40));
    }

    [Fact]
    public void TranslateSelection_PastTheBuildArea_StopsTheWholeGroupAndKeepsItsShape()
    {
        var viewModel = SelectedPair();

        viewModel.TranslateSelection(viewModel.SnapshotSelection(), new Vector2D(_area.Max.X, 30));

        viewModel.Nodes[1].Position.ShouldBe(new Vector2D(_area.Max.X - NodeDef.PlainJointRadius, 30));
        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(_area.Max.X - NodeDef.PlainJointRadius - 100, 30));
    }

    [Fact]
    public void TranslateSelection_WithFractionalCoordinates_StillReachesTheEdge()
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(0.1, 0));
        viewModel.PlaceNode(new Vector2D(100.1, 0));
        viewModel.ReplaceSelection([1, 2]);

        viewModel.TranslateSelection(viewModel.SnapshotSelection(), new Vector2D(1500.3, 0));

        viewModel.Nodes[1].Position.X.ShouldBe(_area.Max.X - NodeDef.PlainJointRadius);
        viewModel.Nodes[0].Position.X.ShouldBe(_area.Max.X - NodeDef.PlainJointRadius - 100, 1e-9);
    }

    [Fact]
    public void ToggleSelected_AddsAndRemovesAnyPart_KeepingTheRest()
    {
        var build = new BuildViewModel();
        build.Load(Carrier());

        build.ToggleSelected(new(CreatureElementKind.Node, 1));
        build.ToggleSelected(new(CreatureElementKind.Beam, 101));
        build.ToggleSelected(new(CreatureElementKind.Piston, 301));
        build.ToggleSelected(new(CreatureElementKind.Sensor, 201));
        build.SelectedPartCount.ShouldBe(4);

        build.ToggleSelected(new(CreatureElementKind.Beam, 101));
        build.Selection.Beams.ShouldBeEmpty();
        build.Selection.Nodes.ShouldBe([1]);
        build.SelectedPartCount.ShouldBe(3);
    }

    [Fact]
    public void DeleteSelectedParts_DeletesEachSelectedPart_EvenOnesADeletedBeamOrJointTakes()
    {
        var build = new BuildViewModel();
        build.Load(Carrier());
        build.ReplaceSelection(new PartSet(
            new HashSet<int> { 3 },
            new HashSet<int> { 101, 102 },
            new HashSet<int> { 201 },
            new HashSet<int>(),
            new HashSet<int> { 301 },
            new HashSet<int>()));

        build.DeleteSelectedParts();

        build.Nodes.Select(node => node.Id).ShouldBe([1, 2]);
        build.Beams.ShouldBeEmpty();
        build.Sensors.ShouldBeEmpty();
        build.Pistons.ShouldBeEmpty();
        build.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void SetParameter_ChangesOnlyThatSetting_OnEverySelectedPiston()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(0, 90)), new NodeDef(4, new Vector2D(90, 90))],
            [new BeamDef(101, 1, 2), new BeamDef(102, 3, 4)],
            [],
            [new PistonDef(301, 1, 3, stroke: 0.2), new PistonDef(302, 2, 4, stroke: 0.4), new PistonDef(303, 1, 4)]));
        build.ReplaceSelection(PartSet.None with { Pistons = new HashSet<int> { 301, 302 } });

        build.SetParameter(PartParameterId.Strength, 20000);
        build.SetParameter(PartParameterId.MaxSpeed, 100);

        build.Pistons.Select(piston => (piston.Strength, piston.Stroke, piston.MaxSpeed)).ShouldBe([
            (20000, 0.2, 100),
            (20000, 0.4, 100),
            (PistonDef.DefaultStrength, PistonDef.DefaultStroke, PistonDef.DefaultMaxSpeed)]);

        build.SetParameter(PartParameterId.Stroke, 0.5);
        build.Pistons.Select(piston => piston.Stroke).ShouldBe([0.5, 0.5, PistonDef.DefaultStroke]);
    }

    [Fact]
    public void SetParameter_OnALockedCreation_ChangesOnce_AndNotAgainForTheSameValue()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(0, 90))],
            [],
            [],
            [new PistonDef(301, 1, 2), new PistonDef(302, 1, 3, stroke: 0.4)]), locked: true);
        build.ReplaceSelection(PartSet.None with { Pistons = new HashSet<int> { 301, 302 } });
        var changes = 0;
        build.AnatomyChanged += (_, _) => changes++;

        build.SetParameter(PartParameterId.Stroke, 0.2);
        build.SetParameter(PartParameterId.Stroke, 0.2);

        build.Pistons.Select(piston => piston.Stroke).ShouldBe([0.2, 0.2]);
        changes.ShouldBe(1);
    }

    [Theory]
    [InlineData(new[] { 301 }, "Strength,Stroke,StartPosition,MaxSpeed,RiseTime")]
    [InlineData(new[] { 301, 302 }, "Strength,Stroke,StartPosition,MaxSpeed,RiseTime")]
    [InlineData(new[] { 301, 1 }, "")]
    [InlineData(new[] { 301, 101 }, "")]
    [InlineData(new[] { 201 }, "Aim")]
    [InlineData(new[] { 201, 202 }, "")]
    [InlineData(new[] { 201, 301 }, "")]
    [InlineData(new[] { 401 }, "Stiffness,Damping,Stroke,CoilLength")]
    [InlineData(new[] { 401, 402 }, "Stiffness,Damping,Stroke,CoilLength")]
    [InlineData(new[] { 301, 401 }, "Stroke")]
    [InlineData(new int[0], "")]
    public void EditableParameters_AreOnePartsOwn_OrThoseEverySelectedPartHasAndCanShare(int[] parts, string editable)
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 0))],
            [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
            [new SensorDef(201, 101, SensorKind.Camera), new SensorDef(202, 102, SensorKind.Camera)],
            [new PistonDef(301, 1, 2), new PistonDef(302, 2, 3)],
            [new SpringDef(401, 1, 3), new SpringDef(402, 2, 3)],
            nextPartId: 403));
        build.ReplaceSelection(new PartSet(
            parts.Where(id => id < 100).ToHashSet(),
            parts.Where(id => id is > 100 and < 200).ToHashSet(),
            parts.Where(id => id is > 200 and < 300).ToHashSet(),
            new HashSet<int>(),
            parts.Where(id => id is > 300 and < 400).ToHashSet(),
            parts.Where(id => id > 400).ToHashSet()));

        string.Join(',', build.EditableParameters).ShouldBe(editable);
    }

    [Fact]
    public void SetParameter_ThatNotEverySelectedPartHas_Throws()
    {
        var build = new BuildViewModel();
        build.Load(Carrier());
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 1 }, Pistons = new HashSet<int> { 301 } });

        Should.Throw<InvalidOperationException>(() => build.SetParameter(PartParameterId.Strength, 20000));
    }

    [Theory]
    [InlineData(BuildTool.Parts)]
    [InlineData(BuildTool.Joint)]
    [InlineData(BuildTool.Select)]
    public void PickedLink_StaysAcrossToolSwitches(BuildTool other)
    {
        var build = new BuildViewModel { ActiveTool = BuildTool.Beam };
        build.PickLink(BuildLink.Spring);

        build.ActiveTool = other;
        build.ActiveTool = BuildTool.Beam;

        build.PickedLink.ShouldBe(BuildLink.Spring);
    }

    [Fact]
    public void Load_PicksBeamAgain()
    {
        var build = new BuildViewModel { ActiveTool = BuildTool.Beam };
        build.PickLink(BuildLink.Piston);

        build.Load(new CreatureBuilder().Build());

        build.PickedLink.ShouldBe(BuildLink.Beam);
    }

    [Fact]
    public void PickedLink_SurvivesSelectingAndClearingAPart()
    {
        var build = new BuildViewModel { ActiveTool = BuildTool.Beam };
        build.PlaceNode(new Vector2D(0, 0));
        build.PickLink(BuildLink.Piston);

        build.ReplaceSelection([1]);
        build.ClearSelection();

        build.PickedLink.ShouldBe(BuildLink.Piston);
    }

    [Fact]
    public void PickLink_LockedFutureRowsDoNothing()
    {
        var build = new BuildViewModel { ActiveTool = BuildTool.Beam };

        build.PickLink(BuildLink.Wing);

        build.PickedLink.ShouldBe(BuildLink.Beam);
    }

    private static CreatureDef Carrier() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 0))],
        [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
        [new SensorDef(201, 101, SensorKind.Accelerometer)],
        [new PistonDef(301, 1, 3)]);

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
        viewModel.PlaceNode(new Vector2D(_area.Max.X - 18, -100));
        viewModel.PlaceNode(new Vector2D(_area.Max.X - 18, 100));
        viewModel.ReplaceSelection([1, 2]);
        var start = viewModel.SnapshotSelection();

        viewModel.RotateSelection(start, Math.PI / 4).ShouldBeFalse();

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
        viewModel.PlaceNode(new Vector2D(10, 10));
        viewModel.PlaceNode(new Vector2D(10, 10));
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

        viewModel.RotateSelection(viewModel.SnapshotSelection(), Math.PI).ShouldBeTrue();

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
        viewModel.PlaceNode(new Vector2D(0, 0));
        viewModel.PlaceNode(new Vector2D(100, 0));
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

        var id = viewModel.PlaceNode(new Vector2D(3, 4));

        id.ShouldBe(1);
        viewModel.Nodes.Count.ShouldBe(1);
        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(3, 4));
        raised.ShouldBeTrue();
    }

    [Fact]
    public void MoveNode_UpdatesPositionAndRaisesAnatomyChanged()
    {
        var viewModel = new BuildViewModel();
        var id = viewModel.PlaceNode(new Vector2D(0, 0));
        var raised = false;
        viewModel.AnatomyChanged += (_, _) => raised = true;

        viewModel.MoveNode(id, new Vector2D(10, 20));

        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(10, 20));
        raised.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(1, true)]
    public void LoadCreation_IsLockedExactlyWhenTrained(int? generation, bool locked)
    {
        var training = generation is { } trained ? TestTraining.State(trained, 1, TestTraining.Run) : null;
        var viewModel = new BuildViewModel();

        viewModel.LoadCreation(new CreationDef(Guid.NewGuid(), "Worm", TwoNodeCreature(), training));

        viewModel.IsLocked.ShouldBe(locked);
    }

    [Fact]
    public void LoadLocked_MovesNodes_AndAddsJoints()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(20, 0))],
            [new BeamDef(101, 1, 2)],
            []);
        var viewModel = new BuildViewModel();

        viewModel.Load(creature, locked: true);
        viewModel.MoveNode(1, new Vector2D(5, 5));

        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(5, 5));
        viewModel.IsLocked.ShouldBeTrue();
        viewModel.PlaceNode(new Vector2D(30, 0));

        viewModel.Nodes.Count.ShouldBe(3);
        viewModel.Beams.Count.ShouldBe(1);
    }

    [Fact]
    public void Unlock_AllowsTopologyChanges()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(20, 0))],
            [new BeamDef(101, 1, 2)],
            []);
        var viewModel = new BuildViewModel();
        viewModel.Load(creature, locked: true);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Unlock();
        viewModel.PlaceNode(new Vector2D(30, 0));

        viewModel.IsLocked.ShouldBeFalse();
        changed.ShouldContain(nameof(BuildViewModel.IsLocked));
        viewModel.Nodes.Count.ShouldBe(3);
    }

    [Fact]
    public void Load_ResetsActiveToolToJoint()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(20, 0))],
            [new BeamDef(101, 1, 2)],
            []);
        var viewModel = new BuildViewModel { ActiveTool = BuildTool.Beam };

        viewModel.Load(creature);

        viewModel.ActiveTool.ShouldBe(BuildTool.Joint);
    }

    [Fact]
    public void LoadLocked_OpensOnTheJointTool()
    {
        var viewModel = new BuildViewModel { ActiveTool = BuildTool.Select };

        viewModel.Load(new CreatureDef([new NodeDef(1, new Vector2D(0, 0))], [], []), locked: true);

        viewModel.ActiveTool.ShouldBe(BuildTool.Joint);
    }

    [Fact]
    public void LoadLocked_WithSavedCreationMetadata_ExposesNameAndTrainingGeneration()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(20, 0))],
            [new BeamDef(101, 1, 2)],
            []);
        var training = TestTraining.State(9, 1, TestTraining.Run);
        var viewModel = new BuildViewModel();

        viewModel.Load(creature, locked: true, creationName: "Worm", training: training);

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
    public void Load_WithoutName_HasNoName()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(new CreatureDef([], [], []), creationName: "Hopper");

        viewModel.Load(new CreatureDef([], [], []));

        viewModel.CreationName.ShouldBeEmpty();
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
        viewModel.PlaceNode(new Vector2D(0, 0));
        var closeIndex = viewModel.PlaceNode(new Vector2D(5, 0));
        viewModel.PlaceNode(new Vector2D(100, 100));

        var found = viewModel.TryFindNodeNear(new Vector2D(6, 0), 10, out var nodeIndex);

        found.ShouldBeTrue();
        nodeIndex.ShouldBe(closeIndex);
    }

    [Fact]
    public void TryFindNodeNear_BeyondDistance_ReturnsFalse()
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0));

        var found = viewModel.TryFindNodeNear(new Vector2D(100, 100), 10, out var nodeIndex);

        found.ShouldBeFalse();
        nodeIndex.ShouldBe(-1);
    }

    [Fact]
    public void ActiveTool_DefaultsToJoint()
    {
        var viewModel = new BuildViewModel();

        viewModel.ActiveTool.ShouldBe(BuildTool.Joint);
    }


    [Fact]
    public void ConnectLink_Beam_DifferentNodes_CreatesBeam()
    {
        var viewModel = new BuildViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0));
        var b = viewModel.PlaceNode(new Vector2D(10, 0));
        var raised = false;
        viewModel.AnatomyChanged += (_, _) => raised = true;

        viewModel.ConnectLink(BuildLink.Beam, a, b);

        viewModel.Beams.Count.ShouldBe(1);
        viewModel.Beams[0].NodeA.ShouldBe(a);
        viewModel.Beams[0].NodeB.ShouldBe(b);
        raised.ShouldBeTrue();
    }

    [Fact]
    public void ConnectLink_Beam_SameNode_CreatesNoBeam()
    {
        var viewModel = new BuildViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0));

        viewModel.ConnectLink(BuildLink.Beam, a, a).ShouldBeNull();

        viewModel.Beams.Count.ShouldBe(0);
    }

    [Fact]
    public void ConnectLink_Beam_DuplicateBeam_RefusesInsteadOfThrowing()
    {
        var viewModel = new BuildViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0));
        var b = viewModel.PlaceNode(new Vector2D(10, 0));
        viewModel.ConnectLink(BuildLink.Beam, a, b);

        viewModel.ConnectLink(BuildLink.Beam, a, b).ShouldBeNull();

        viewModel.Beams.Count.ShouldBe(1);
    }

    [Fact]
    public void ConnectLink_Beam_WhenLocked_CreatesTheBeam()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(
            new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(20, 0))], [], []),
            locked: true);

        viewModel.CanConnectLink(BuildLink.Beam, 1, 2, out _).ShouldBeTrue();
        viewModel.ConnectLink(BuildLink.Beam, 1, 2).ShouldNotBeNull();

        viewModel.Beams.ShouldHaveSingleItem();
    }


    private static BuildViewModel LockedPair()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(
            new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(20, 0))], [new BeamDef(101, 1, 2)], []),
            locked: true);
        return viewModel;
    }




    [Fact]
    public void TryFindBeamNear_WithinDistance_ReturnsBeam()
    {
        var viewModel = new BuildViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0));
        var b = viewModel.PlaceNode(new Vector2D(10, 0));
        viewModel.ConnectLink(BuildLink.Beam, a, b);

        var found = viewModel.TryFindBeamNear(new Vector2D(5, 0), 2, out var beamId);

        found.ShouldBeTrue();
        beamId.ShouldBe(viewModel.Beams[0].Id);
    }

    [Fact]
    public void TryFindBeamNear_BeyondDistance_ReturnsFalse()
    {
        var viewModel = new BuildViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0));
        var b = viewModel.PlaceNode(new Vector2D(10, 0));
        viewModel.ConnectLink(BuildLink.Beam, a, b);

        var found = viewModel.TryFindBeamNear(new Vector2D(5, 50), 2, out var beamIndex);

        found.ShouldBeFalse();
        beamIndex.ShouldBe(-1);
    }

    [Fact]
    public void SelectBeam_ReplacesNodeSelectionAndExposesBeamAsSelectedPart()
    {
        var viewModel = new BuildViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0));
        var b = viewModel.PlaceNode(new Vector2D(10, 0));
        viewModel.ConnectLink(BuildLink.Beam, a, b);
        viewModel.ToggleSelected(new(CreatureElementKind.Node, a));
        var raisedFor = new List<string?>();
        var anatomyChanged = false;
        viewModel.PropertyChanged += (_, args) => raisedFor.Add(args.PropertyName);
        viewModel.AnatomyChanged += (_, _) => anatomyChanged = true;

        viewModel.SelectOnly(CreatureElementKind.Beam, viewModel.Beams[0].Id);

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
        var a = viewModel.PlaceNode(new Vector2D(0, 0));
        var b = viewModel.PlaceNode(new Vector2D(10, 0));
        viewModel.ConnectLink(BuildLink.Beam, a, b);
        viewModel.SelectOnly(CreatureElementKind.Beam, viewModel.Beams[0].Id);
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
        var a = viewModel.PlaceNode(new Vector2D(0, 0));
        var b = viewModel.PlaceNode(new Vector2D(10, 0));
        viewModel.ConnectLink(BuildLink.Beam, a, b);
        viewModel.SelectOnly(CreatureElementKind.Beam, viewModel.Beams[0].Id);

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

        viewModel.SelectOnly(CreatureElementKind.Sensor, 5);

        viewModel.SelectedPartCount.ShouldBe(1);
        viewModel.SingleSelectedSensorId.ShouldBe(5);

        viewModel.DeleteSelectedParts();

        viewModel.Sensors.Select(sensor => sensor.Id).ShouldBe([4]);
        viewModel.Beams.Count.ShouldBe(2);
        viewModel.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void SelectingABeam_DropsTheSelectedSensor()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(SensorCreature());
        viewModel.SelectOnly(CreatureElementKind.Sensor, 4);

        viewModel.SelectOnly(CreatureElementKind.Beam, 3);
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
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(6, new Vector2D(100, 100))],
        [new BeamDef(3, 1, 2), new BeamDef(7, 2, 6)],
        [new SensorDef(4, 3, SensorKind.Accelerometer), new SensorDef(5, 7, SensorKind.Camera)]);

    [Fact]
    public void DeleteSelectedParts_OnALockedCreation_KeepsABeamWithASensor()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(10, 0))],
                [new BeamDef(101, 1, 2)],
                [new SensorDef(201, 101, SensorKind.Accelerometer)]),
            locked: true);
        viewModel.SelectOnly(CreatureElementKind.Beam, viewModel.Beams[0].Id);

        viewModel.DeleteSelectedParts();

        viewModel.Beams.Count.ShouldBe(1);
        viewModel.Sensors.Count.ShouldBe(1);
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
        viewModel.PlaceNode(new Vector2D(0, 0));

        var canLeave = viewModel.TryLeave(out var creature, out var errors);

        canLeave.ShouldBeFalse();
        creature.ShouldBeNull();
        errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void Snapshot_WithUnconnectedNode_ReturnsTheDrawingForSaving()
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0));

        var creature = viewModel.Snapshot();

        creature.Nodes.Count.ShouldBe(1);
        creature.Beams.ShouldBeEmpty();
    }

    [Fact]
    public void TryGetTrainableCreature_WithUnconnectedNode_Refuses()
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0));

        viewModel.TryGetTrainableCreature(out var creature).ShouldBeFalse();

        creature.ShouldBeNull();
    }

    [Fact]
    public void TryGetTrainableCreature_WithNothingToDrive_ReturnsTheCreature()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(200, 0))], [new BeamDef(3, 1, 2)], []));

        viewModel.TryGetTrainableCreature(out var creature).ShouldBeTrue();

        creature.ShouldNotBeNull();
    }

    [Fact]
    public void TryGetTrainableCreature_WithAPiston_ReturnsTheCreature()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 20))],
            [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
            [],
            [new PistonDef(301, 1, 3)]));

        viewModel.TryGetTrainableCreature(out var creature).ShouldBeTrue();

        creature.ShouldNotBeNull();
        creature.Beams.Count.ShouldBe(2);
    }

    [Fact]
    public void TryLeave_WithValidCreature_ReturnsTrue()
    {
        var viewModel = new BuildViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0));
        var b = viewModel.PlaceNode(new Vector2D(90, 0));
        viewModel.ConnectLink(BuildLink.Beam, a, b);

        var canLeave = viewModel.TryLeave(out var creature, out var errors);

        canLeave.ShouldBeTrue();
        creature.ShouldNotBeNull();
        creature.Nodes.Count.ShouldBe(2);
        errors.ShouldBeEmpty();
    }

    private static CreatureDef TwoNodeCreature() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(20, 0))],
        [new BeamDef(3, 1, 2)],
        []);

    [Fact]
    public void CanvasNotes_NameEachBeamTooShortToTrain()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(50, 0)), new NodeDef(3, new Vector2D(150, 0)), new NodeDef(4, new Vector2D(150, 0))],
            [new BeamDef(5, 1, 2), new BeamDef(6, 2, 3), new BeamDef(7, 3, 4)],
            []));

        viewModel.CanvasNotes().ShouldBe(
        [
            new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Beam, 5), UiText.Plain("Too short")),
        ]);
    }

    [Fact]
    public void ShowTrainingBlockers_NotesEachLooseJoint_UntilItIsJoined()
    {
        var viewModel = new BuildViewModel();
        var first = viewModel.PlaceNode(new Vector2D(0, 0));
        var second = viewModel.PlaceNode(new Vector2D(200, 0));
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        viewModel.CanvasNotes().ShouldBeEmpty();

        viewModel.ShowTrainingBlockers();

        changed.ShouldContain(nameof(BuildViewModel.CanvasNotes));
        viewModel.CanvasNotes().ShouldBe(
        [
            new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Node, first), UiText.Plain("Not connected")),
            new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Node, second), UiText.Plain("Not connected")),
        ]);

        viewModel.ConnectLink(BuildLink.Beam, first, second).ShouldNotBeNull();

        viewModel.CanvasNotes().ShouldBeEmpty();
    }

    [Fact]
    public void ShowTrainingBlockers_NotesEveryPieceAtItsJointNearestAnother_UntilTheyAreJoined()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(TwoPieces());
        viewModel.CanvasNotes().ShouldBeEmpty();

        viewModel.ShowTrainingBlockers();

        viewModel.CanvasNotes().ShouldBe([NotConnected(2), NotConnected(4)]);

        viewModel.ConnectLink(BuildLink.Beam, 2, 4).ShouldNotBeNull();

        viewModel.CanvasNotes().ShouldBeEmpty();
    }

    [Fact]
    public void ShowTrainingBlockers_LeavesAPieceSplitOffAfterTheyWereJoinedForTheNextTap()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(TwoPieces());
        viewModel.ShowTrainingBlockers();
        viewModel.ConnectLink(BuildLink.Beam, 2, 4).ShouldNotBeNull();

        var a = viewModel.PlaceNode(new Vector2D(600, 0));
        var b = viewModel.PlaceNode(new Vector2D(800, 0));
        viewModel.ConnectLink(BuildLink.Beam, a, b).ShouldNotBeNull();

        viewModel.CanvasNotes().ShouldBeEmpty();
        viewModel.ShowTrainingBlockers();
        viewModel.CanvasNotes().ShouldBe([NotConnected(2), NotConnected(a)]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShowTrainingBlockers_ForgetsThePiecesOnceJoined_SoUndoOrDeletingTheLinkWaitsForTheNextTap(bool undo)
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(TwoPieces());
        viewModel.ShowTrainingBlockers();

        var spring = viewModel.ConnectLink(BuildLink.Spring, 2, 4).ShouldNotBeNull();
        if (undo)
        {
            viewModel.Undo();
        }
        else
        {
            viewModel.SelectOnly(CreatureElementKind.Spring, spring);
            viewModel.DeleteSelectedParts();
        }

        viewModel.Pieces().Count.ShouldBe(2);
        viewModel.CanvasNotes().ShouldBeEmpty();
    }

    [Fact]
    public void Load_ForgetsThePiecesTheLastTapPointedAt()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(TwoPieces());
        viewModel.ShowTrainingBlockers();

        viewModel.Load(TwoPieces());

        viewModel.CanvasNotes().ShouldBeEmpty();
    }

    // Joints 1–2 and 3–4: joint 2 and joint 4 are the two pieces' nearest joints.
    private static CreatureDef TwoPieces() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(200, 100)), new NodeDef(3, new Vector2D(0, 300)), new NodeDef(4, new Vector2D(200, 300))],
        [new BeamDef(5, 1, 2), new BeamDef(6, 4, 3)],
        []);

    private static CanvasNote NotConnected(int nodeId) =>
        new(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Node, nodeId), UiText.Plain("Not connected"));

    [Fact]
    public void ShowTrainingBlockers_LeavesAJointLoosenedLaterForTheNextTap()
    {
        var viewModel = new BuildViewModel();
        viewModel.Load(new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(200, 0))], [new BeamDef(3, 1, 2)], []));
        viewModel.ShowTrainingBlockers();

        var loose = viewModel.PlaceNode(new Vector2D(400, 0));

        viewModel.CanvasNotes().ShouldBeEmpty();
        viewModel.ShowTrainingBlockers();
        viewModel.CanvasNotes().ShouldBe(
            [new CanvasNote(CanvasNoteKind.Danger, new CreatureElementSelection(CreatureElementKind.Node, loose), UiText.Plain("Not connected"))]);
    }

    [Fact]
    public void Load_ForgetsTheShownBlockers()
    {
        var viewModel = new BuildViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0));
        viewModel.ShowTrainingBlockers();

        viewModel.Load(new CreatureDef([new NodeDef(1, new Vector2D(0, 0))], [], []));

        viewModel.CanvasNotes().ShouldBeEmpty();
    }

    [Fact]
    public void SetAim_TurnsTheCameraAndRedrawsOnlyOnAChange()
    {
        var build = new BuildViewModel();
        build.Load(CameraPair(), locked: false);
        build.SelectOnly(CreatureElementKind.Sensor, 4);
        var changes = 0;
        build.AnatomyChanged += (_, _) => changes++;

        build.SetParameter(PartParameterId.Aim, 1);
        build.SetParameter(PartParameterId.Aim, 1);

        build.Sensors[0].Aim.ShouldBe(1);
        changes.ShouldBe(1);
    }

    [Fact]
    public void SetAim_WhenLocked_TurnsTheCamera()
    {
        var build = new BuildViewModel();
        build.Load(CameraPair(), locked: true);
        build.SelectOnly(CreatureElementKind.Sensor, 4);

        build.SetParameter(PartParameterId.Aim, 1);

        build.Sensors[0].Aim.ShouldBe(1);
    }

    private static CreatureDef CameraPair() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0))],
        [new BeamDef(3, 1, 2)],
        [new SensorDef(4, 3, SensorKind.Camera)]);
}
