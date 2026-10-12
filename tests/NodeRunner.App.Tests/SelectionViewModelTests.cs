using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests;

public sealed class SelectionViewModelTests
{
    private const int _stackedJoint = 1;
    private const int _servoId = 10;
    private const int _wheelId = 11;
    private const int _loneWheelId = 12;
    private const int _touchId = 13;
    private const int _loneTouchId = 14;
    private const int _loneTouchJoint = 4;
    private const int _rubberId = 15;
    private const int _brakeId = 16;

    // Joint 1 holds a Servo, a Wheel, a Rubber and a Touch sensor, joint 3 a Wheel alone, joint 4 a Touch sensor alone, and joint 2 nothing.
    private static readonly CreatureDef _creature = Creature(servos: [new ServoDef(_servoId, _stackedJoint, 5, 7)], brakes: []);

    // The same, with a Brake in the Servo's place: a joint holds one motor or brake (#1041).
    private static readonly CreatureDef _brakedCreature = Creature(servos: [], brakes: [new BrakeDef(_brakeId, _stackedJoint, 5, 7)]);

    private static readonly CreatureElementSelection[] _stackTopDown =
        [new(CreatureElementKind.Servo, _servoId), new(CreatureElementKind.Wheel, _wheelId), new(CreatureElementKind.Rubber, _rubberId), new(CreatureElementKind.TouchSensor, _touchId)];

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
    [InlineData(CreatureElementKind.Rubber, _rubberId)]
    [InlineData(CreatureElementKind.TouchSensor, _touchId)]
    [InlineData(CreatureElementKind.Node, _stackedJoint)]
    public void Select_OnAStackedJoint_SelectsEveryPartTopDown(CreatureElementKind kind, int id)
    {
        var viewModel = new SelectionViewModel();

        viewModel.Select(_creature, new CreatureElementSelection(kind, id));

        viewModel.SelectedParts.ShouldBe(_stackTopDown);
    }

    [Theory]
    [InlineData(CreatureElementKind.Brake, _brakeId)]
    [InlineData(CreatureElementKind.Wheel, _wheelId)]
    [InlineData(CreatureElementKind.Node, _stackedJoint)]
    public void Select_OnAStackedJointWithABrake_SelectsEveryPartTopDown_TheBrakeFirst(CreatureElementKind kind, int id)
    {
        var viewModel = new SelectionViewModel();

        viewModel.Select(_brakedCreature, new CreatureElementSelection(kind, id));

        viewModel.SelectedParts.ShouldBe([new(CreatureElementKind.Brake, _brakeId), .. _stackTopDown.Skip(1)]);
    }

    [Theory]
    [InlineData(CreatureElementKind.Wheel, _loneWheelId)]
    [InlineData(CreatureElementKind.TouchSensor, _loneTouchId)]
    [InlineData(CreatureElementKind.Node, 2)]
    [InlineData(CreatureElementKind.Spring, 8)]
    public void Select_OnASinglePart_SelectsItAlone(CreatureElementKind kind, int id)
    {
        var viewModel = new SelectionViewModel();

        viewModel.Select(_creature, new CreatureElementSelection(kind, id));

        viewModel.SelectedParts.ShouldBe([new CreatureElementSelection(kind, id)]);
    }

    [Fact]
    public void Select_OnAJointWithATouchSensorAlone_SelectsTheSensor()
    {
        var viewModel = new SelectionViewModel();

        viewModel.Select(_creature, new CreatureElementSelection(CreatureElementKind.Node, _loneTouchJoint));

        viewModel.SelectedParts.ShouldBe([new CreatureElementSelection(CreatureElementKind.TouchSensor, _loneTouchId)]);
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

    private static CreatureDef Creature(ServoDef[] servos, BrakeDef[] brakes) => new(
        [new NodeDef(_stackedJoint, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0)), new NodeDef(3, new Vector2D(4, 0)), new NodeDef(4, new Vector2D(0, 2))],
        [new BeamDef(5, 1, 2), new BeamDef(6, 2, 3), new BeamDef(7, 1, 4)],
        [],
        servos,
        [],
        [new SpringDef(8, 2, 4)],
        [new WheelDef(_wheelId, _stackedJoint), new WheelDef(_loneWheelId, 3)],
        [new TouchSensorDef(_touchId, _stackedJoint), new TouchSensorDef(_loneTouchId, _loneTouchJoint)],
        [new RubberDef(_rubberId, _stackedJoint)],
        [],
        brakes,
        nextPartId: 17);
}
