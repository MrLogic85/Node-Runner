using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class ConstructionViewModelTests
{
    private static readonly CanvasRect _area = ConstructionViewModel.BuildArea;

    [Fact]
    public void PlaceNode_OutsideTheBuildArea_LandsWithItsDiscJustInside()
    {
        var viewModel = new ConstructionViewModel();

        viewModel.PlaceNode(new Vector2D(_area.Max.X + 500, _area.Min.Y - 500), 18);

        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(_area.Max.X - 18, _area.Min.Y + 18));
    }

    [Fact]
    public void MoveNode_PastTheBuildArea_StopsAtTheEdge()
    {
        var viewModel = new ConstructionViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0), 18);

        viewModel.MoveNode(0, new Vector2D(_area.Min.X - 500, 40));

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
        var viewModel = new ConstructionViewModel();
        viewModel.PlaceNode(new Vector2D(0.1, 0), 14);
        viewModel.PlaceNode(new Vector2D(100.1, 0), 14);
        viewModel.ReplaceSelection([0, 1]);

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
        var viewModel = new ConstructionViewModel();
        viewModel.PlaceNode(new Vector2D(_area.Max.X - 18, -100), 18);
        viewModel.PlaceNode(new Vector2D(_area.Max.X - 18, 100), 18);
        viewModel.ReplaceSelection([0, 1]);
        var start = viewModel.SnapshotSelection();

        viewModel.RotateSelection(start, Math.PI / 4);

        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(_area.Max.X - 18, -100));
        viewModel.Nodes[1].Position.ShouldBe(new Vector2D(_area.Max.X - 18, 100));
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(0.5, 0.5)]
    [InlineData(100, ConstructionViewModel.MaxSelectionScale)]
    [InlineData(-3, ConstructionViewModel.MinSelectionScale)]
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
        var viewModel = new ConstructionViewModel();
        viewModel.PlaceNode(new Vector2D(10, 10), 18);
        viewModel.PlaceNode(new Vector2D(10, 10), 18);
        viewModel.ReplaceSelection([0, 1]);

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
        viewModel.ReplaceSelection([0, 1]);

        viewModel.ScaleSelection(viewModel.SnapshotSelection(), 2);

        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(-10, 0));
        viewModel.Nodes[1].Position.ShouldBe(new Vector2D(30, 0));
    }

    [Fact]
    public void RotateSelection_WhenLocked_Turns()
    {
        var viewModel = LockedPair();
        viewModel.ReplaceSelection([0, 1]);

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

    private static ConstructionViewModel SelectedPair()
    {
        var viewModel = new ConstructionViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0), 18);
        viewModel.PlaceNode(new Vector2D(100, 0), 18);
        viewModel.ReplaceSelection([0, 1]);
        return viewModel;
    }

    [Fact]
    public void IsActive_DefaultsToFalse()
    {
        var viewModel = new ConstructionViewModel();

        viewModel.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void IsActive_WhenChanged_RaisesPropertyChanged()
    {
        var viewModel = new ConstructionViewModel();
        var raisedFor = new List<string?>();
        viewModel.PropertyChanged += (_, args) => raisedFor.Add(args.PropertyName);

        viewModel.IsActive = true;

        raisedFor.ShouldContain(nameof(ConstructionViewModel.IsActive));
    }

    [Fact]
    public void IsActive_WhenSetToSameValue_DoesNotRaisePropertyChanged()
    {
        var viewModel = new ConstructionViewModel();
        var raiseCount = 0;
        viewModel.PropertyChanged += (_, _) => raiseCount++;

        viewModel.IsActive = false;

        raiseCount.ShouldBe(0);
    }

    [Fact]
    public void PlaceNode_AddsNodeAndRaisesAnatomyChanged()
    {
        var viewModel = new ConstructionViewModel();
        var raised = false;
        viewModel.AnatomyChanged += (_, _) => raised = true;

        var index = viewModel.PlaceNode(new Vector2D(3, 4), 18);

        index.ShouldBe(0);
        viewModel.Nodes.Count.ShouldBe(1);
        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(3, 4));
        raised.ShouldBeTrue();
    }

    [Fact]
    public void MoveNode_UpdatesPositionAndRaisesAnatomyChanged()
    {
        var viewModel = new ConstructionViewModel();
        var index = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var raised = false;
        viewModel.AnatomyChanged += (_, _) => raised = true;

        viewModel.MoveNode(index, new Vector2D(10, 20));

        viewModel.Nodes[index].Position.ShouldBe(new Vector2D(10, 20));
        raised.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void LoadCreation_IsMoveOnlyExactlyWhenLocked(int? generation, bool moveOnly)
    {
        var training = generation is { } trained ? new TrainingStateDef([2, 1], [0.1, -0.2, 0.3], trained, "Tanh") : null;
        var viewModel = new ConstructionViewModel();

        viewModel.LoadCreation(new CreationDef(Guid.NewGuid(), "Worm", TwoNodeCreature(), training));

        viewModel.IsMoveOnly.ShouldBe(moveOnly);
    }

    [Fact]
    public void LoadMoveOnly_AllowsMovingExistingNodesButRejectsTopologyChanges()
    {
        var creature = new CreatureDef(
            [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(20, 0), 18)],
            [new BeamDef(0, 1)],
            []);
        var viewModel = new ConstructionViewModel();

        viewModel.Load(creature, moveOnly: true);
        viewModel.MoveNode(0, new Vector2D(5, 5));

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
            [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(20, 0), 18)],
            [new BeamDef(0, 1)],
            []);
        var viewModel = new ConstructionViewModel { ActiveTool = ConstructionTool.Beam };

        viewModel.Load(creature);

        viewModel.ActiveTool.ShouldBe(ConstructionTool.Move);
    }

    [Fact]
    public void Load_WithBrainShape_PreservesSavedBrainShape()
    {
        var creature = new CreatureDef(
            [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(20, 0), 18)],
            [new BeamDef(0, 1)],
            []);
        var shape = new BrainShapeDef(3, 12);
        var viewModel = new ConstructionViewModel();

        viewModel.Load(creature, moveOnly: true, brainShape: shape);

        viewModel.BrainShape.ShouldBe(shape);
    }

    [Fact]
    public void LoadMoveOnly_WithSavedCreationMetadata_ExposesNameAndTrainingGeneration()
    {
        var creature = new CreatureDef(
            [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(20, 0), 18)],
            [new BeamDef(0, 1)],
            []);
        var training = new TrainingStateDef([2, 4, 1], Enumerable.Repeat(0.1, 17).ToArray(), 9, "Tanh");
        var viewModel = new ConstructionViewModel();

        viewModel.Load(creature, moveOnly: true, creationName: "Worm", training: training);

        viewModel.CreationName.ShouldBe("Worm");
        viewModel.TrainingGeneration.ShouldBe(9);
    }

    [Fact]
    public void SetCreationName_ChangesTheName()
    {
        var viewModel = new ConstructionViewModel();

        viewModel.SetCreationName("Hopper");

        viewModel.CreationName.ShouldBe("Hopper");
    }

    [Fact]
    public void Load_WithoutName_IsUntitled()
    {
        var viewModel = new ConstructionViewModel();

        viewModel.Load(new CreatureDef([], [], []));

        viewModel.CreationName.ShouldBe(NewCreationWorkflow.UntitledName);
    }

    [Fact]
    public void TryFindNodeNear_WithNoNodes_ReturnsFalse()
    {
        var viewModel = new ConstructionViewModel();

        var found = viewModel.TryFindNodeNear(new Vector2D(0, 0), 10, out var nodeIndex);

        found.ShouldBeFalse();
        nodeIndex.ShouldBe(-1);
    }

    [Fact]
    public void TryFindNodeNear_WithinDistance_ReturnsClosestNode()
    {
        var viewModel = new ConstructionViewModel();
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
        var viewModel = new ConstructionViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0), 18);

        var found = viewModel.TryFindNodeNear(new Vector2D(100, 100), 10, out var nodeIndex);

        found.ShouldBeFalse();
        nodeIndex.ShouldBe(-1);
    }

    [Fact]
    public void ActiveTool_DefaultsToMove()
    {
        var viewModel = new ConstructionViewModel();

        viewModel.ActiveTool.ShouldBe(ConstructionTool.Move);
    }

    [Fact]
    public void ActiveTool_WhenChanged_ClearsStatus()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        viewModel.ConnectBeam(a, a);

        viewModel.ActiveTool = ConstructionTool.Core;

        viewModel.StatusMessage.ShouldBeNull();
    }

    [Fact]
    public void ConnectBeam_DifferentNodes_CreatesBeam()
    {
        var viewModel = new ConstructionViewModel();
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
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);

        viewModel.ConnectBeam(a, a).ShouldBeFalse();

        viewModel.Beams.Count.ShouldBe(0);
    }

    [Fact]
    public void ConnectBeam_DuplicateBeam_SurfacesErrorInsteadOfThrowing()
    {
        var viewModel = new ConstructionViewModel();
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
        var viewModel = new ConstructionViewModel();
        viewModel.Load(
            new CreatureDef([new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(20, 0), 18)], [], []),
            moveOnly: true);

        viewModel.CanConnect(0, 1).ShouldBeFalse();
        viewModel.ConnectBeam(0, 1).ShouldBeFalse();

        viewModel.Beams.ShouldBeEmpty();
        viewModel.StatusMessage.ShouldBe("Edit mode only allows moving existing nodes.");
    }

    [Fact]
    public void SplitBeam_ReplacesBeamWithTwoThroughNewNodeInOneChange()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(100, 0), 18);
        var c = viewModel.PlaceNode(new Vector2D(100, 100), 18);
        viewModel.ConnectBeam(a, b);
        viewModel.ConnectBeam(b, c);
        viewModel.ToggleCoreOnNode(c);
        viewModel.SelectBeam(0);
        var changes = 0;
        viewModel.AnatomyChanged += (_, _) => changes++;

        var joint = viewModel.SplitBeam(0, new Vector2D(30, 12), 18);

        joint.ShouldBe(3);
        viewModel.Nodes[3].Position.ShouldBe(new Vector2D(30, 0));
        viewModel.Beams.ShouldBe([new BeamDef(b, c), new BeamDef(a, 3), new BeamDef(3, b)]);
        viewModel.Cores.ShouldBe([new CoreDef(c)]);
        viewModel.SelectedPartCount.ShouldBe(0);
        changes.ShouldBe(1);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(110, 0)]
    public void SplitBeam_AtAnEnd_ChangesNothing(double x, double y)
    {
        var viewModel = new ConstructionViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0), 18);
        viewModel.PlaceNode(new Vector2D(100, 0), 18);
        viewModel.ConnectBeam(0, 1);

        viewModel.SplitBeam(0, new Vector2D(x, y), 18).ShouldBeNull();

        viewModel.Nodes.Count.ShouldBe(2);
        viewModel.Beams.Count.ShouldBe(1);
    }

    [Fact]
    public void SplitBeam_WhenLocked_ChangesNothing()
    {
        var viewModel = LockedPair();

        viewModel.SplitBeam(0, new Vector2D(10, 0), 18).ShouldBeNull();

        viewModel.Nodes.Count.ShouldBe(2);
        viewModel.Beams.Count.ShouldBe(1);
    }

    private static ConstructionViewModel LockedPair()
    {
        var viewModel = new ConstructionViewModel();
        viewModel.Load(
            new CreatureDef([new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(20, 0), 18)], [new BeamDef(0, 1)], []),
            moveOnly: true);
        return viewModel;
    }

    [Fact]
    public void ToggleCoreOnNode_AddsCoreAndRaisesAnatomyChanged()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var raised = false;
        viewModel.AnatomyChanged += (_, _) => raised = true;

        viewModel.ToggleCoreOnNode(a);

        viewModel.Cores.Count.ShouldBe(1);
        viewModel.Cores[0].NodeIndex.ShouldBe(a);
        raised.ShouldBeTrue();
    }

    [Fact]
    public void ToggleCoreOnNode_CalledTwice_RemovesCore()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);

        viewModel.ToggleCoreOnNode(a);
        viewModel.ToggleCoreOnNode(a);

        viewModel.Cores.Count.ShouldBe(0);
    }

    [Fact]
    public void ToggleCoreOnNode_RespectsUnlockedCoreLimit()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(20, 0), 18);
        var c = viewModel.PlaceNode(new Vector2D(40, 0), 18);
        viewModel.SetMaxCores(2);

        viewModel.ToggleCoreOnNode(a);
        viewModel.ToggleCoreOnNode(b);
        viewModel.ToggleCoreOnNode(c);

        viewModel.Cores.Count.ShouldBe(2);
        viewModel.StatusMessage.ShouldBe("Core limit reached (2). Train to unlock another core slot.");
    }

    [Fact]
    public void TryFindBeamNear_WithinDistance_ReturnsBeam()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        viewModel.ConnectBeam(a, b);

        var found = viewModel.TryFindBeamNear(new Vector2D(5, 0), 2, out var beamIndex);

        found.ShouldBeTrue();
        beamIndex.ShouldBe(0);
    }

    [Fact]
    public void TryFindBeamNear_BeyondDistance_ReturnsFalse()
    {
        var viewModel = new ConstructionViewModel();
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
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        viewModel.ConnectBeam(a, b);
        viewModel.ToggleSelectedNode(a);
        var raisedFor = new List<string?>();
        var anatomyChanged = false;
        viewModel.PropertyChanged += (_, args) => raisedFor.Add(args.PropertyName);
        viewModel.AnatomyChanged += (_, _) => anatomyChanged = true;

        viewModel.SelectBeam(0);

        viewModel.SelectedNodeCount.ShouldBe(0);
        viewModel.SelectedBeamCount.ShouldBe(1);
        viewModel.SelectedPartCount.ShouldBe(1);
        viewModel.SingleSelectedBeamIndex.ShouldBe(0);
        raisedFor.ShouldContain(nameof(ConstructionViewModel.SelectedBeamCount));
        raisedFor.ShouldContain(nameof(ConstructionViewModel.SelectedPartCount));
        raisedFor.ShouldContain(nameof(ConstructionViewModel.SingleSelectedBeamIndex));
        anatomyChanged.ShouldBeTrue();
    }

    [Fact]
    public void ClearSelection_WithOnlyBeamSelected_ClearsBeam()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        viewModel.ConnectBeam(a, b);
        viewModel.SelectBeam(0);
        var raisedFor = new List<string?>();
        var anatomyChanged = false;
        viewModel.PropertyChanged += (_, args) => raisedFor.Add(args.PropertyName);
        viewModel.AnatomyChanged += (_, _) => anatomyChanged = true;

        viewModel.ClearSelection();

        viewModel.SelectedPartCount.ShouldBe(0);
        viewModel.SingleSelectedBeamIndex.ShouldBeNull();
        raisedFor.ShouldContain(nameof(ConstructionViewModel.SelectedBeamCount));
        raisedFor.ShouldContain(nameof(ConstructionViewModel.SelectedPartCount));
        raisedFor.ShouldContain(nameof(ConstructionViewModel.SingleSelectedBeamIndex));
        anatomyChanged.ShouldBeTrue();
    }

    [Fact]
    public void DeleteSelectedParts_WithBeamSelected_RemovesBeamButKeepsNodes()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        viewModel.ConnectBeam(a, b);
        viewModel.SelectBeam(0);

        viewModel.DeleteSelectedParts();

        viewModel.Beams.ShouldBeEmpty();
        viewModel.Nodes.Count.ShouldBe(2);
        viewModel.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void DeleteSelectedParts_InMoveOnlyMode_PreservesAnatomy()
    {
        var viewModel = new ConstructionViewModel();
        viewModel.Load(
            new CreatureDef(
                [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(10, 0), 18)],
                [new BeamDef(0, 1)],
                []),
            moveOnly: true);
        viewModel.SelectBeam(0);

        viewModel.DeleteSelectedParts();

        viewModel.Beams.Count.ShouldBe(1);
        viewModel.StatusMessage.ShouldBe("Edit mode can only move selected parts.");
    }

    [Fact]
    public void DeleteSelectedParts_WithNodeSelected_CascadesAttachedPartsAndClearsSelection()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        var c = viewModel.PlaceNode(new Vector2D(20, 0), 18);
        viewModel.ConnectBeam(a, b);
        viewModel.ConnectBeam(b, c);
        viewModel.ToggleCoreOnNode(b);
        viewModel.ToggleSelectedNode(b);
        var anatomyChanged = false;
        viewModel.AnatomyChanged += (_, _) => anatomyChanged = true;

        viewModel.DeleteSelectedParts();

        viewModel.Nodes.Count.ShouldBe(2);
        viewModel.Beams.ShouldBeEmpty();
        viewModel.Cores.ShouldBeEmpty();
        viewModel.SelectedPartCount.ShouldBe(0);
        anatomyChanged.ShouldBeTrue();
    }

    [Fact]
    public void DeleteSelectedParts_WithMultipleNodes_DeletesDescendingAndReindexesSurvivors()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        var c = viewModel.PlaceNode(new Vector2D(20, 0), 18);
        viewModel.ConnectBeam(a, b);
        viewModel.ConnectBeam(b, c);
        viewModel.ToggleCoreOnNode(b);
        viewModel.ToggleSelectedNode(a);
        viewModel.ToggleSelectedNode(c);

        viewModel.DeleteSelectedParts();

        viewModel.Nodes.Count.ShouldBe(1);
        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(10, 0));
        viewModel.Beams.ShouldBeEmpty();
        viewModel.Cores.Count.ShouldBe(1);
        viewModel.Cores[0].NodeIndex.ShouldBe(0);
        viewModel.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void TryLeave_WithNoNodesPlaced_ReturnsTrue()
    {
        var viewModel = new ConstructionViewModel();

        var canLeave = viewModel.TryLeave(out var creature, out var errors);

        canLeave.ShouldBeTrue();
        creature.ShouldBeNull();
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void TryLeave_WithUnconnectedNode_ReturnsFalseWithErrors()
    {
        var viewModel = new ConstructionViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0), 18);

        var canLeave = viewModel.TryLeave(out var creature, out var errors);

        canLeave.ShouldBeFalse();
        creature.ShouldBeNull();
        errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void Snapshot_WithUnconnectedNode_ReturnsTheDrawingForSaving()
    {
        var viewModel = new ConstructionViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0), 18);

        var creature = viewModel.Snapshot();

        creature.Nodes.Count.ShouldBe(1);
        creature.Beams.ShouldBeEmpty();
    }

    [Fact]
    public void TryGetTrainableCreature_WithUnconnectedNode_RefusesAndSaysWhy()
    {
        var viewModel = new ConstructionViewModel();
        viewModel.PlaceNode(new Vector2D(0, 0), 18);

        viewModel.TryGetTrainableCreature(out var creature).ShouldBeFalse();

        creature.ShouldBeNull();
        viewModel.StatusMessage.ShouldNotBeNull();
        viewModel.StatusMessage.ShouldContain("no beams attached");
    }

    [Fact]
    public void TryGetTrainableCreature_WithNothingToDrive_Refuses()
    {
        var viewModel = new ConstructionViewModel();
        viewModel.Load(TwoNodeCreature());

        viewModel.TryGetTrainableCreature(out var creature).ShouldBeFalse();

        creature.ShouldBeNull();
    }

    [Fact]
    public void TryGetTrainableCreature_WithAMotorRelation_ReturnsTheCreature()
    {
        var viewModel = new ConstructionViewModel();
        viewModel.Load(new CreatureDef(
            [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(20, 0), 18), new NodeDef(new Vector2D(40, 10), 18)],
            [new BeamDef(0, 1), new BeamDef(1, 2)],
            []));

        viewModel.TryGetTrainableCreature(out var creature).ShouldBeTrue();

        creature.ShouldNotBeNull();
        creature.Beams.Count.ShouldBe(2);
    }

    [Fact]
    public void TryLeave_WithValidCreature_ReturnsTrue()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
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
        var viewModel = new ConstructionViewModel();

        viewModel.SetBlockedLeaveMessage(["Add at least one node before running the creature."]);

        viewModel.StatusMessage.ShouldNotBeNullOrEmpty();
        viewModel.StatusMessage!.ShouldContain("Add at least one node");
    }

    private static CreatureDef TwoNodeCreature() => new(
        [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(20, 0), 18)],
        [new BeamDef(0, 1)],
        []);
}
