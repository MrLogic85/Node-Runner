using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests;

public sealed class SelectionViewModelTests
{
    [Fact]
    public void Select_WithNewElement_SetsSelectionAndRaisesPropertyChanged()
    {
        var viewModel = new SelectionViewModel();
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

        viewModel.Select(new CreatureElementSelection(CreatureElementKind.Beam, 2));

        viewModel.SelectedElement.ShouldBe(new CreatureElementSelection(CreatureElementKind.Beam, 2));
        changedProperties.ShouldBe(new[] { nameof(SelectionViewModel.SelectedElement) });
    }

    [Fact]
    public void Select_StoresPartIdNotListPosition()
    {
        var viewModel = new SelectionViewModel();

        viewModel.Select(new CreatureElementSelection(CreatureElementKind.Node, 42));

        viewModel.SelectedElement.ShouldBe(new CreatureElementSelection(CreatureElementKind.Node, 42));
    }

    [Fact]
    public void Clear_WithSelection_ClearsSelectionAndRaisesPropertyChanged()
    {
        var viewModel = new SelectionViewModel();
        viewModel.Select(new CreatureElementSelection(CreatureElementKind.Node, 1));
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

        viewModel.Clear();

        viewModel.SelectedElement.ShouldBeNull();
        changedProperties.ShouldBe(new[] { nameof(SelectionViewModel.SelectedElement) });
    }

}
