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
    public void PlaceNode_AddsNodeAndRaisesNodesChanged()
    {
        var viewModel = new ConstructionViewModel();
        var raised = false;
        viewModel.NodesChanged += (_, _) => raised = true;

        var index = viewModel.PlaceNode(new Vector2D(3, 4), 18);

        index.ShouldBe(0);
        viewModel.Nodes.Count.ShouldBe(1);
        viewModel.Nodes[0].Position.ShouldBe(new Vector2D(3, 4));
        raised.ShouldBeTrue();
    }

    [Fact]
    public void MoveNode_UpdatesPositionAndRaisesNodesChanged()
    {
        var viewModel = new ConstructionViewModel();
        var index = viewModel.PlaceNode(new Vector2D(0, 0), 18);
        var raised = false;
        viewModel.NodesChanged += (_, _) => raised = true;

        viewModel.MoveNode(index, new Vector2D(10, 20));

        viewModel.Nodes[index].Position.ShouldBe(new Vector2D(10, 20));
        raised.ShouldBeTrue();
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
}
