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

        viewModel.Select(new CreatureElementSelection(CreatureElementKind.Muscle, 2));

        viewModel.SelectedElement.ShouldBe(new CreatureElementSelection(CreatureElementKind.Muscle, 2));
        changedProperties.ShouldBe(new[] { nameof(SelectionViewModel.SelectedElement) });
    }

    [Fact]
    public void Clear_WithSelection_ClearsSelectionAndRaisesPropertyChanged()
    {
        var viewModel = new SelectionViewModel();
        viewModel.Select(new CreatureElementSelection(CreatureElementKind.Joint, 0));
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

        viewModel.Clear();

        viewModel.SelectedElement.ShouldBeNull();
        changedProperties.ShouldBe(new[] { nameof(SelectionViewModel.SelectedElement) });
    }

}
