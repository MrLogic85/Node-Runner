using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests;

public sealed class SelectionViewModelTests
{
    private const int _stackedJoint = 1;
    private const int _servoId = 10;
    private const int _wheelId = 11;
    private const int _loneWheelId = 12;

    // Joint 1 holds a Servo and a Wheel, joint 3 a Wheel alone, and joint 2 nothing.
    private static readonly CreatureDef _creature = new(
        [new NodeDef(_stackedJoint, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0)), new NodeDef(3, new Vector2D(4, 0)), new NodeDef(4, new Vector2D(0, 2))],
        [new BeamDef(5, 1, 2), new BeamDef(6, 2, 3), new BeamDef(7, 1, 4)],
        [],
        [new ServoDef(_servoId, _stackedJoint, 5, 7)],
        [],
        [new SpringDef(8, 2, 4)],
        [new WheelDef(_wheelId, _stackedJoint), new WheelDef(_loneWheelId, 3)],
        nextPartId: 13);

    private static readonly CreatureElementSelection[] _stackTopDown =
        [new(CreatureElementKind.Servo, _servoId), new(CreatureElementKind.Wheel, _wheelId)];

    [Fact]
    public void Select_WithNewElement_SetsSelectionAndRaisesPropertyChanged()
    {
        var viewModel = new SelectionViewModel();
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

        viewModel.Select(_creature, new CreatureElementSelection(CreatureElementKind.Beam, 6));

        viewModel.SelectedParts.ShouldBe([new CreatureElementSelection(CreatureElementKind.Beam, 6)]);
        changedProperties.ShouldBe(new[] { nameof(SelectionViewModel.SelectedParts) });
    }

    [Theory]
    [InlineData(CreatureElementKind.Servo, _servoId)]
    [InlineData(CreatureElementKind.Wheel, _wheelId)]
    [InlineData(CreatureElementKind.Node, _stackedJoint)]
    public void Select_OnAStackedJoint_SelectsEveryPartTopDown(CreatureElementKind kind, int id)
    {
        var viewModel = new SelectionViewModel();

        viewModel.Select(_creature, new CreatureElementSelection(kind, id));

        viewModel.SelectedParts.ShouldBe(_stackTopDown);
    }

    [Theory]
    [InlineData(CreatureElementKind.Wheel, _loneWheelId)]
    [InlineData(CreatureElementKind.Node, 2)]
    [InlineData(CreatureElementKind.Spring, 8)]
    public void Select_OnASinglePart_SelectsItAlone(CreatureElementKind kind, int id)
    {
        var viewModel = new SelectionViewModel();

        viewModel.Select(_creature, new CreatureElementSelection(kind, id));

        viewModel.SelectedParts.ShouldBe([new CreatureElementSelection(kind, id)]);
    }

    [Fact]
    public void Select_APartTheCreatureLacks_SelectsTheTappedPartAlone()
    {
        var viewModel = new SelectionViewModel();
        var tapped = new CreatureElementSelection(CreatureElementKind.Servo, 99);

        viewModel.Select(_creature, tapped);

        viewModel.SelectedParts.ShouldBe([tapped]);
    }

    [Fact]
    public void Select_AnotherPartOfTheSelectedStack_RaisesNothing()
    {
        var viewModel = new SelectionViewModel();
        viewModel.Select(_creature, new CreatureElementSelection(CreatureElementKind.Servo, _servoId));
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

        viewModel.Select(_creature, new CreatureElementSelection(CreatureElementKind.Wheel, _wheelId));

        viewModel.SelectedParts.ShouldBe(_stackTopDown);
        changedProperties.ShouldBeEmpty();
    }

    [Fact]
    public void Clear_WithSelection_ClearsSelectionAndRaisesPropertyChanged()
    {
        var viewModel = new SelectionViewModel();
        viewModel.Select(_creature, new CreatureElementSelection(CreatureElementKind.Node, 2));
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

        viewModel.Clear();

        viewModel.SelectedParts.ShouldBeEmpty();
        changedProperties.ShouldBe(new[] { nameof(SelectionViewModel.SelectedParts) });
    }
}
