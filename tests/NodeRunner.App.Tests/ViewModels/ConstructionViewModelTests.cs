using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class ConstructionViewModelTests
{
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
    public void LoadMoveOnly_ResetsActiveToolToPlaceForMoving()
    {
        var creature = new CreatureDef(
            [new NodeDef(new Vector2D(0, 0), 18), new NodeDef(new Vector2D(20, 0), 18)],
            [new BeamDef(0, 1)],
            []);
        var viewModel = new ConstructionViewModel { ActiveTool = ConstructionTool.Beam };

        viewModel.Load(creature, moveOnly: true);

        viewModel.ActiveTool.ShouldBe(ConstructionTool.Place);
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
    public void ActiveTool_DefaultsToPlace()
    {
        var viewModel = new ConstructionViewModel();

        viewModel.ActiveTool.ShouldBe(ConstructionTool.Place);
    }

    [Fact]
    public void ActiveTool_WhenChanged_ClearsPendingBeamAndStatus()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        viewModel.SelectNodeForBeam(a);

        viewModel.ActiveTool = ConstructionTool.Core;

        viewModel.PendingBeamStartNode.ShouldBeNull();
        viewModel.StatusMessage.ShouldBeNull();
    }

    [Fact]
    public void SelectNodeForBeam_FirstCall_SetsPendingStartNode()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);

        viewModel.SelectNodeForBeam(a);

        viewModel.PendingBeamStartNode.ShouldBe(a);
    }

    [Fact]
    public void SelectNodeForBeam_SecondCallOnDifferentNode_CreatesBeam()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        var raised = false;
        viewModel.AnatomyChanged += (_, _) => raised = true;

        viewModel.SelectNodeForBeam(a);
        viewModel.SelectNodeForBeam(b);

        viewModel.Beams.Count.ShouldBe(1);
        viewModel.Beams[0].NodeA.ShouldBe(a);
        viewModel.Beams[0].NodeB.ShouldBe(b);
        viewModel.PendingBeamStartNode.ShouldBeNull();
        raised.ShouldBeTrue();
    }

    [Fact]
    public void SelectNodeForBeam_SameNodeTwice_ClearsSelectionWithoutCreatingBeam()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);

        viewModel.SelectNodeForBeam(a);
        viewModel.SelectNodeForBeam(a);

        viewModel.Beams.Count.ShouldBe(0);
        viewModel.PendingBeamStartNode.ShouldBeNull();
    }

    [Fact]
    public void SelectNodeForBeam_DuplicateBeam_SurfacesErrorInsteadOfThrowing()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        viewModel.SelectNodeForBeam(a);
        viewModel.SelectNodeForBeam(b);

        viewModel.SelectNodeForBeam(a);
        viewModel.SelectNodeForBeam(b);

        viewModel.Beams.Count.ShouldBe(1);
        viewModel.StatusMessage.ShouldNotBeNullOrEmpty();
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
    public void DeleteNode_RemovesNodeAndCascadesToBeamsAndCores()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        viewModel.SelectNodeForBeam(a);
        viewModel.SelectNodeForBeam(b);
        viewModel.ToggleCoreOnNode(a);
        var raised = false;
        viewModel.AnatomyChanged += (_, _) => raised = true;

        viewModel.DeleteNode(a);

        viewModel.Nodes.Count.ShouldBe(1);
        viewModel.Beams.Count.ShouldBe(0);
        viewModel.Cores.Count.ShouldBe(0);
        raised.ShouldBeTrue();
    }

    [Fact]
    public void DeleteBeam_RemovesBeamButKeepsNodes()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        viewModel.SelectNodeForBeam(a);
        viewModel.SelectNodeForBeam(b);
        var raised = false;
        viewModel.AnatomyChanged += (_, _) => raised = true;

        viewModel.DeleteBeam(0);

        viewModel.Beams.Count.ShouldBe(0);
        viewModel.Nodes.Count.ShouldBe(2);
        raised.ShouldBeTrue();
    }

    [Fact]
    public void TryFindBeamNear_WithinDistance_ReturnsBeam()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        viewModel.SelectNodeForBeam(a);
        viewModel.SelectNodeForBeam(b);

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
        viewModel.SelectNodeForBeam(a);
        viewModel.SelectNodeForBeam(b);

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
        viewModel.SelectNodeForBeam(a);
        viewModel.SelectNodeForBeam(b);
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
        viewModel.SelectNodeForBeam(a);
        viewModel.SelectNodeForBeam(b);
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
        viewModel.SelectNodeForBeam(a);
        viewModel.SelectNodeForBeam(b);
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
        viewModel.SelectNodeForBeam(a);
        viewModel.SelectNodeForBeam(b);
        viewModel.SelectNodeForBeam(b);
        viewModel.SelectNodeForBeam(c);
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
        viewModel.SelectNodeForBeam(a);
        viewModel.SelectNodeForBeam(b);
        viewModel.SelectNodeForBeam(b);
        viewModel.SelectNodeForBeam(c);
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
    public void TryLeave_WithValidCreature_ReturnsTrue()
    {
        var viewModel = new ConstructionViewModel();
        var a = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var b = viewModel.PlaceNode(new Vector2D(10, 0), 18);
        viewModel.SelectNodeForBeam(a);
        viewModel.SelectNodeForBeam(b);

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
}
