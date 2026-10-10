using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>Parts stacked on one joint (#1044): a Wheel plus one motor or brake, placed, selected, tabbed, deleted and copied.</summary>
public sealed class BuildStackedPartsTests
{
    private const int _servoId = 6;
    private const int _wheelId = 7;
    private static readonly CreatureElementSelection _middleJoint = new(CreatureElementKind.Node, 2);
    private static readonly CreatureElementSelection _servo = new(CreatureElementKind.Servo, _servoId);
    private static readonly CreatureElementSelection _wheel = new(CreatureElementKind.Wheel, _wheelId);

    [Theory]
    [InlineData(BuildPart.Servo, BuildPart.Wheel)]
    [InlineData(BuildPart.Wheel, BuildPart.Servo)]
    public void PlacePart_AServoAndAWheel_ShareAJoint_InEitherOrder(BuildPart first, BuildPart second)
    {
        var build = TwoBeams();

        build.PlacePart(first, _middleJoint).ShouldNotBeNull();
        build.PlacePart(second, _middleJoint).ShouldNotBeNull();

        build.Servos.Single().NodeId.ShouldBe(2);
        build.Wheels.Single().NodeId.ShouldBe(2);
        build.PlacementNote.ShouldBeNull();
        build.NodeRadius(2).ShouldBe(WheelDef.DefaultRadius);
        build.JointPartsAt(2).Select(part => part.Kind).ShouldBe([CreatureElementKind.Wheel, CreatureElementKind.Servo]);
    }

    [Fact]
    public void PlacePart_ASecondServoOrWheel_OnAStack_IsRefused_OnePerSlot()
    {
        var (build, _) = Stacked();

        build.PlacePart(BuildPart.Servo, _middleJoint).ShouldBeNull();
        build.PlacementNote!.Text.ShouldBe(UiText.Plain("One motor or brake per joint"));
        build.PlacePart(BuildPart.Wheel, _middleJoint).ShouldBeNull();
        build.PlacementNote!.Text.ShouldBe(UiText.Plain("One wheel per joint"));
        build.Servos.Count.ShouldBe(1);
        build.Wheels.Count.ShouldBe(1);
    }

    [Fact]
    public void Tap_OnAStack_SelectsTheOutermostPart_AndASecondTapClearsIt()
    {
        var (build, gestures) = Stacked();

        Tap(gestures, new Vector2D(100, 0));
        build.Selection.Parts.ShouldBe([_wheel]);

        Tap(gestures, new Vector2D(100, 0));
        build.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void Tap_OnAStackWhoseInnerPartIsSelected_ClearsTheJointsParts()
    {
        var (build, gestures) = Stacked();
        build.ReplaceSelection(PartSet.Of(_servo));

        Tap(gestures, new Vector2D(100, 0));

        build.SelectedPartCount.ShouldBe(0);
    }

    [Fact]
    public void Drag_OfAnUnselectedStack_SelectsTheOutermostPart_AndMovesTheJoint()
    {
        var (build, gestures) = Stacked();

        Drag(gestures, new Vector2D(100, 0), new Vector2D(100, 40));

        build.Nodes.Single(node => node.Id == 2).Position.ShouldBe(new Vector2D(100, 40));
        build.Selection.Parts.ShouldBe([_wheel]);
    }

    [Fact]
    public void Drag_OfAStackWithASelectedPart_MovesTheSelection()
    {
        var (build, gestures) = Stacked();
        build.ReplaceSelection(PartSet.Of(_servo));

        Drag(gestures, new Vector2D(100, 0), new Vector2D(100, 40));

        build.Nodes.Single(node => node.Id == 2).Position.ShouldBe(new Vector2D(100, 40));
        build.Selection.Parts.ShouldBe([_servo]);
    }

    [Fact]
    public void Box_OverAStack_SelectsEveryPartInPlaceOfTheJoint()
    {
        var (build, gestures) = Stacked();

        Drag(gestures, new Vector2D(70, -70), new Vector2D(130, 70));

        build.Selection.Nodes.ShouldBeEmpty();
        build.Selection.Servos.ShouldBe([_servoId]);
        build.Selection.Wheels.ShouldBe([_wheelId]);
    }

    [Fact]
    public void Delete_OfTheServoAlone_KeepsTheJointAndWheel_AndUndoBringsItBackSelected()
    {
        var (build, _) = Stacked();
        build.ReplaceSelection(PartSet.Of(_servo));

        build.DeleteSelectedParts();

        build.Servos.ShouldBeEmpty();
        build.Wheels.Single().Id.ShouldBe(_wheelId);
        build.Nodes.Select(node => node.Id).ShouldBe([1, 2, 3]);
        build.Undo();
        build.Servos.Single().Id.ShouldBe(_servoId);
        build.Selection.Parts.ShouldBe([_servo]);
    }

    [Fact]
    public void Delete_OfBothPartsAndEveryLink_TakesTheJoint()
    {
        var (build, _) = Stacked();
        build.ReplaceSelection(PartSet.None with
        {
            Beams = new HashSet<int> { 4, 5 },
            Servos = new HashSet<int> { _servoId },
            Wheels = new HashSet<int> { _wheelId },
        });

        build.DeleteSelectedParts();

        build.Nodes.Select(node => node.Id).ShouldBe([1, 3]);
        build.Servos.ShouldBeEmpty();
        build.Wheels.ShouldBeEmpty();
    }

    [Fact]
    public void Delete_OfEveryLinkButOnePart_KeepsTheJointForTheOther()
    {
        var (build, _) = Stacked();
        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 4, 5 }, Servos = new HashSet<int> { _servoId } });

        build.DeleteSelectedParts();

        build.Nodes.Select(node => node.Id).ShouldContain(2);
        build.Wheels.Single().NodeId.ShouldBe(2);
    }

    // A big Wheel's joint near the corner would only fit going back up and left; the copied
    // joint carries the Servo alone, so it fits one step down and right.
    [Fact]
    public void Copy_OfTheServoAndItsLinks_LeavesTheWheelBehind_AndSizesTheCopiedJointForTheServo()
    {
        var corner = BuildViewModel.BuildArea.Max;
        var step = BuildViewModel.BuildGridStep;
        var stack = new Vector2D(corner.X - WheelDef.MaxRadius, corner.Y - WheelDef.MaxRadius);
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(stack.X - 100, stack.Y)), new NodeDef(2, stack), new NodeDef(3, new Vector2D(stack.X, stack.Y - 100))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
            [],
            [new ServoDef(_servoId, 2, 4, 5)],
            [],
            [],
            [new WheelDef(_wheelId, 2, radius: WheelDef.MaxRadius)],
            nextPartId: 8));
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 1, 3 }, Beams = new HashSet<int> { 4, 5 }, Servos = new HashSet<int> { _servoId } });

        build.CopySelectedParts();

        build.Wheels.Count.ShouldBe(1);
        var copy = build.Servos.Single(servo => servo.Id != _servoId);
        build.Nodes.Single(node => node.Id == copy.NodeId).Position.ShouldBe(new Vector2D(stack.X + step, stack.Y + step));
        build.NodeRadius(copy.NodeId).ShouldBe(ServoDef.JointRadius);
    }

    [Fact]
    public void Locked_TheWheelOfAStackCanGo_ButNotItsServo()
    {
        var (build, _) = Stacked(locked: true);

        build.ReplaceSelection(PartSet.Of(_servo));
        build.DeleteLockedReason.ShouldNotBeNull();
        build.ReplaceSelection(PartSet.Of(_wheel));
        build.DeleteLockedReason.ShouldBeNull();
        build.DeleteSelectedParts();

        build.Wheels.ShouldBeEmpty();
        build.Servos.Single().NodeId.ShouldBe(2);
    }

    [Fact]
    public void OnThisJoint_ListsTheStackOutsideIn_AndMarksTheSelectedPart()
    {
        var (build, _) = Stacked();
        build.ReplaceSelection(PartSet.Of(_servo));

        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.OnThisJoint!.ShouldBe([new JointPartTab(PartSettingsKind.Wheel, _wheel), new JointPartTab(PartSettingsKind.Servo, _servo)]);
        part.OnThisJointIndex.ShouldBe(1);
    }

    // A tab tap replaces the selection with its part (BuildHost); the strip stays and its mark moves.
    [Fact]
    public void OnThisJoint_TabbingToTheWheel_ShowsTheWheelWithTheSameStrip()
    {
        var (build, _) = Stacked();
        build.ReplaceSelection(PartSet.Of(_servo));
        var servoPanel = new BuildPresentationViewModel(build).SinglePart!;

        build.ReplaceSelection(PartSet.Of(servoPanel.OnThisJoint![0].Part));

        var part = new BuildPresentationViewModel(build).SinglePart!;
        part.Kind.ShouldBe(PartSettingsKind.Wheel);
        part.OnThisJoint.ShouldBe(servoPanel.OnThisJoint);
        part.OnThisJointIndex.ShouldBe(0);
        part.PanelId.ShouldNotBe(servoPanel.PanelId);
    }

    [Fact]
    public void OnThisJoint_IsHidden_ForALonePart_AndForAMultiSelection()
    {
        var build = TwoBeams();
        var wheel = build.PlacePart(BuildPart.Wheel, _middleJoint)!.Value;
        build.SelectOnly(CreatureElementKind.Wheel, wheel);
        new BuildPresentationViewModel(build).SinglePart!.OnThisJoint.ShouldBeNull();

        var (stacked, _) = Stacked();
        stacked.ReplaceSelection(PartSet.None with { Servos = new HashSet<int> { _servoId }, Wheels = new HashSet<int> { _wheelId } });
        new BuildPresentationViewModel(stacked).SinglePart.ShouldBeNull();
    }

    /// <summary>Joints 1 (0,0), 2 (100,0) and 3 (200,0); beam 4 joins 1–2 and beam 5 joins 2–3.</summary>
    private static BuildViewModel TwoBeams()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
            [],
            nextPartId: 6));
        return build;
    }

    /// <summary><see cref="TwoBeams"/> with a Servo and a Wheel on joint 2 and nothing selected; Select is active.</summary>
    private static (BuildViewModel Build, BuildGestures Gestures) Stacked(bool locked = false)
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0))],
                [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
                [],
                [new ServoDef(_servoId, 2, 4, 5)],
                [],
                [],
                [new WheelDef(_wheelId, 2)],
                nextPartId: 8),
            locked: locked);
        build.ActiveTool = BuildTool.Select;
        return (build, new BuildGestures(build));
    }

    private static void Tap(BuildGestures gestures, Vector2D position)
    {
        gestures.Press(position);
        gestures.Release(position);
    }

    private static void Drag(BuildGestures gestures, Vector2D from, Vector2D to)
    {
        gestures.Press(from);
        gestures.Drag(to);
        gestures.Release(to);
    }
}
