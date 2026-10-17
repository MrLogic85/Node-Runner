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

    // The same, with a Brake in the Servo's place: a joint holds one hinge part.
    private static readonly CreatureDef _brakedCreature = Creature(servos: [], brakes: [new BrakeDef(_brakeId, _stackedJoint, 5, 7)]);

    private static readonly CreatureElementSelection[] _stackTopDown =
        [new(CreatureElementKind.Servo, _servoId), new(CreatureElementKind.Wheel, _wheelId), new(CreatureElementKind.Rubber, _rubberId), new(CreatureElementKind.TouchSensor, _touchId)];

    public static TheoryData<CreatureDef, CreatureElementSelection, CreatureElementSelection[]> Taps => new()
    {
        { _creature, new(CreatureElementKind.Wheel, _wheelId), _stackTopDown },
        { _creature, new(CreatureElementKind.Node, _stackedJoint), _stackTopDown },
        { _brakedCreature, new(CreatureElementKind.Brake, _brakeId), [new(CreatureElementKind.Brake, _brakeId), .. _stackTopDown.Skip(1)] },
        { _creature, new(CreatureElementKind.Node, _loneTouchJoint), [new(CreatureElementKind.TouchSensor, _loneTouchId)] },
        { _creature, new(CreatureElementKind.Node, 2), [new(CreatureElementKind.Node, 2)] },
        { _creature, new(CreatureElementKind.Spring, 8), [new(CreatureElementKind.Spring, 8)] },
        { _creature, new(CreatureElementKind.Servo, 99), [new(CreatureElementKind.Servo, 99)] },
    };

    [Theory]
    [MemberData(nameof(Taps))]
    public void Select_ATappedJointOrJointPart_SelectsItsWholeStackTopDown_AnyOtherPartAlone(CreatureDef creature, CreatureElementSelection tapped, CreatureElementSelection[] expected)
    {
        var viewModel = new SelectionViewModel();

        viewModel.Select(creature, tapped);

        viewModel.SelectedParts.ShouldBe(expected);
    }

    [Fact]
    public void SelectedParts_IsRaisedOnlyWhenTheSelectionChanges()
    {
        var viewModel = new SelectionViewModel();
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

        viewModel.Select(_creature, new CreatureElementSelection(CreatureElementKind.Beam, 6));
        viewModel.Select(_creature, new CreatureElementSelection(CreatureElementKind.Servo, _servoId));
        viewModel.Select(_creature, new CreatureElementSelection(CreatureElementKind.Wheel, _wheelId));
        viewModel.Clear();

        viewModel.SelectedParts.ShouldBeEmpty();
        changedProperties.ShouldBe([nameof(SelectionViewModel.SelectedParts), nameof(SelectionViewModel.SelectedParts), nameof(SelectionViewModel.SelectedParts)]);
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
