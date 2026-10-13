using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.ML;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>
/// The Angle sensor in Build (#1206): placed on a joint from the tray in its hinge-part slot,
/// set up with the Motor's pickers, copied, deleted and stacked as the other joint parts are, and
/// kept by a locked Creation as a part with brain ports.
/// </summary>
public sealed class BuildAngleSensorTests
{
    private const int _angleSensorId = 6;
    private static readonly CreatureElementSelection _endJoint = new(CreatureElementKind.Node, 1);
    private static readonly CreatureElementSelection _middleJoint = new(CreatureElementKind.Node, 2);
    private static readonly CreatureElementSelection _angleSensor = new(CreatureElementKind.AngleSensor, _angleSensorId);

    [Fact]
    public void TheSensorsTab_ListsAnAvailableAngleSensorRow_ThatALockedCreationLocks()
    {
        PartTray.Create(BuildPart.AngleSensor).Groups[1].Rows.Single(row => row.Part == BuildPart.AngleSensor)
            .ShouldBe(new PartTrayRow(BuildPart.AngleSensor, UiText.Plain("Angle sensor"), PartTrayRowState.Selected, Version: null));
        PartTray.Create(picked: null, creationLocked: true).Groups[1].Rows.Single(row => row.Part == BuildPart.AngleSensor)
            .State.ShouldBe(PartTrayRowState.CreationLocked);
        PartTray.CanPick(BuildPart.AngleSensor, creationLocked: true).ShouldBeFalse();
    }

    [Fact]
    public void PlacePart_OnAJoint_TakesTheDefaultSides_SelectsIt_AndCanTrain()
    {
        var build = TwoBeams();

        var id = build.PlacePart(BuildPart.AngleSensor, _middleJoint).ShouldNotBeNull();

        build.AngleSensors.ShouldBe([new AngleSensorDef(id, 2, fixedLinkId: 4, targetLinkId: 5)]);
        build.Selection.Parts.ShouldBe([new CreatureElementSelection(CreatureElementKind.AngleSensor, id)]);
        build.PlacementNote.ShouldBeNull();
        CreatureReadiness.CanTrain(build.Snapshot()).ShouldBeTrue();
        build.Undo();
        build.AngleSensors.ShouldBeEmpty();
        build.Redo();
        build.AngleSensors.ShouldHaveSingleItem().Id.ShouldBe(id);
    }

    [Fact]
    public void Tap_WithTheAngleSensorPicked_PlacesItInTheJointsPlacingRing_AndKeepsItPicked()
    {
        var build = TwoBeams();
        build.ActiveTool = BuildTool.Parts;
        build.PickPart(BuildPart.AngleSensor);
        var gestures = new BuildGestures(build);
        var targets = build.PlacingTargetsOf(BuildPart.AngleSensor, movingSensorId: null);

        Tap(gestures, new Vector2D(100, SelectionMarks.JointHalo(targets.JointRingOf(2)) - 1));

        build.AngleSensors.ShouldHaveSingleItem().NodeId.ShouldBe(2);
        build.PickedPart.ShouldBe(BuildPart.AngleSensor);
    }

    // A ring marks a joint at its JointHalo; past an arc it sits on the sensor's halo, 28 on a bare joint.
    [Fact]
    public void PlacingAnAngleSensor_RingsEachJointThatTakesIt_OutsideTheArcItWouldDraw()
    {
        var build = TwoBeams();

        var targets = build.PlacingTargetsOf(BuildPart.AngleSensor, movingSensorId: null);

        SelectionMarks.JointHalo(targets.JointRingOf(2)).ShouldBe(28);
        SelectionMarks.JointHalo(build.PlacingTargetsOf(BuildPart.Brake, movingSensorId: null).JointRingOf(2)).ShouldBe(NodeDef.HubRadius + SelectionMarks.Gap);
    }

    [Theory]
    [InlineData(BuildPart.AngleSensor)]
    [InlineData(BuildPart.Servo)]
    [InlineData(BuildPart.Brake)]
    public void ARefusedRing_OnAnAngleSensorsJoint_SitsOnItsHalo(BuildPart placing)
    {
        var build = new BuildViewModel();
        build.Load(WithAnAngleSensor());

        var targets = build.PlacingTargetsOf(placing, movingSensorId: null);

        targets.RefusedJoints.ShouldContain(2);
        SelectionMarks.JointHalo(targets.RefusedRingOf(2)).ShouldBe(28);
    }

    // A placed Rubber or Touch sensor moves the arc out, so the ring that previews it moves with it.
    [Theory]
    [InlineData(BuildPart.Rubber, 31)]
    [InlineData(BuildPart.TouchSensor, 33)]
    [InlineData(BuildPart.Wheel, 43)]
    public void ATakingRing_OnAnAngleSensorsJoint_ClearsTheArcThePartWouldLeave(BuildPart placing, double ring)
    {
        var build = new BuildViewModel();
        build.Load(WithAnAngleSensor());

        var targets = build.PlacingTargetsOf(placing, movingSensorId: null);

        targets.Joints.ShouldContain(2);
        SelectionMarks.JointHalo(targets.JointRingOf(2)).ShouldBe(ring);
        SelectionMarks.JointHalo(targets.JointRingOf(1)).ShouldBe(SelectionMarks.JointHalo(PartTray.PlacingRingRadius(placing, NodeDef.PlainJointRadius)));
    }

    [Fact]
    public void ALinksRing_OnAnAngleSensorsJoint_SitsOnItsHalo_AndElsewhereOnTheJointsOwn()
    {
        var build = new BuildViewModel();
        build.Load(WithAnAngleSensor());

        SelectionMarks.JointHalo(build.RingRadiusAt(2, build.NodeRadius(2))).ShouldBe(28);
        build.RingRadiusAt(1, build.NodeRadius(1)).ShouldBe(NodeDef.PlainJointRadius);
    }

    [Theory]
    [InlineData(BuildPart.Wheel)]
    [InlineData(BuildPart.Rubber)]
    [InlineData(BuildPart.TouchSensor)]
    public void PlacePart_OnAJointWithAWheelRubberOrTouchSensor_GoesOnTopOfItsStack(BuildPart first)
    {
        var build = TwoBeams();
        var other = build.PlacePart(first, _middleJoint)!.Value;

        var id = build.PlacePart(BuildPart.AngleSensor, _middleJoint).ShouldNotBeNull();

        build.JointPartsAt(2).Select(part => part.Id).ShouldBe([id, other]);
        build.PlacingTargetsOf(first, movingSensorId: null).RefusedJoints.ShouldContain(2);
    }

    [Theory]
    [InlineData(BuildPart.Servo, BuildPart.AngleSensor)]
    [InlineData(BuildPart.VelocityMotor, BuildPart.AngleSensor)]
    [InlineData(BuildPart.Brake, BuildPart.AngleSensor)]
    [InlineData(BuildPart.AngleSensor, BuildPart.AngleSensor)]
    [InlineData(BuildPart.AngleSensor, BuildPart.Servo)]
    [InlineData(BuildPart.AngleSensor, BuildPart.VelocityMotor)]
    [InlineData(BuildPart.AngleSensor, BuildPart.Brake)]
    public void AJointHoldsOneServoMotorBrakeOrAngleSensor_AndARefusedDropSaysSo(BuildPart first, BuildPart second)
    {
        var build = TwoBeams();
        build.PlacePart(first, _middleJoint);

        build.PlacePart(second, _middleJoint).ShouldBeNull();

        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, _middleJoint, UiText.Plain("One Servo, Motor, Brake or Angle sensor per joint")));
        build.HingeParts.ShouldHaveSingleItem();
        build.PlacingTargetsOf(second, movingSensorId: null).RefusedJoints.ShouldContain(2);
    }

    [Theory]
    [InlineData(CreatureElementKind.Beam, 4)]
    [InlineData(CreatureElementKind.Piston, 7)]
    [InlineData(CreatureElementKind.Sensor, 8)]
    public void PlacePart_OnABeamLinkOrSensor_IsRefused_WithANote(CreatureElementKind kind, int id)
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
            [new SensorDef(8, 5, SensorKind.Accelerometer)],
            [new PistonDef(7, 1, 3)],
            nextPartId: 9));
        var target = new CreatureElementSelection(kind, id);

        build.PlacePart(BuildPart.AngleSensor, target).ShouldBeNull();

        build.PlacementNote.ShouldBe(new CanvasNote(CanvasNoteKind.Danger, target, UiText.Plain("Angle sensors go on a joint")));
        build.AngleSensors.ShouldBeEmpty();
    }

    [Fact]
    public void PlacePart_OnAOneLinkJoint_LeavesItsTargetMissing_AndReadinessBlocksTraining()
    {
        var build = TwoBeams();

        var id = build.PlacePart(BuildPart.AngleSensor, _endJoint)!.Value;

        build.AngleSensors.ShouldBe([new AngleSensorDef(id, 1, fixedLinkId: 4)]);
        CreatureReadiness.CanTrain(build.Snapshot()).ShouldBeFalse();
        var target = new BuildPresentationViewModel(build).SinglePart!.Pickers![1];
        target.SelectedIndex.ShouldBeNull();
        target.Note.ShouldBe(UiText.Plain("An Angle sensor needs two links, or a link and a Wheel"));
        build.ShowTrainingBlockers();
        build.CanvasNotes().Single(note => note.Target == new CreatureElementSelection(CreatureElementKind.AngleSensor, id)).Text
            .ShouldBe(UiText.Plain("An Angle sensor needs two links, or a link and a Wheel"));
    }

    [Fact]
    public void PlacePart_OnALinkAndWheelJoint_TakesTheWheelAsItsTarget()
    {
        var build = TwoBeams();
        var wheel = build.PlacePart(BuildPart.Wheel, _endJoint)!.Value;

        build.PlacePart(BuildPart.AngleSensor, _endJoint);

        build.AngleSensors.Single().ShouldBe(new AngleSensorDef(_angleSensorId + 1, 1, fixedLinkId: 4, targetLinkId: wheel));
    }

    [Fact]
    public void Tap_OnAStack_StepsDownFromTheAngleSensor_ThroughTheWheelRubberAndTouchSensor()
    {
        var build = TwoBeams();
        var wheel = build.PlacePart(BuildPart.Wheel, _middleJoint)!.Value;
        var rubber = build.PlacePart(BuildPart.Rubber, _middleJoint)!.Value;
        var touch = build.PlacePart(BuildPart.TouchSensor, _middleJoint)!.Value;
        var angleSensor = build.PlacePart(BuildPart.AngleSensor, _middleJoint)!.Value;
        build.ReplaceSelection(PartSet.None);
        build.ActiveTool = BuildTool.Select;
        var gestures = new BuildGestures(build);

        List<IReadOnlyList<CreatureElementSelection>> taps = [];
        for (var tap = 0; tap < 5; tap++)
        {
            Tap(gestures, new Vector2D(100, 0));
            taps.Add([.. build.Selection.Parts]);
        }

        taps.ShouldBe([
            [new CreatureElementSelection(CreatureElementKind.AngleSensor, angleSensor)],
            [new CreatureElementSelection(CreatureElementKind.Wheel, wheel)],
            [new CreatureElementSelection(CreatureElementKind.Rubber, rubber)],
            [new CreatureElementSelection(CreatureElementKind.TouchSensor, touch)],
            [],
        ]);
    }

    [Fact]
    public void Box_OverItsJoint_SelectsTheAngleSensorInPlaceOfTheJoint()
    {
        var build = new BuildViewModel();
        build.Load(WithAnAngleSensor());
        build.ActiveTool = BuildTool.Select;
        var gestures = new BuildGestures(build);

        gestures.Press(new Vector2D(70, -70));
        gestures.Drag(new Vector2D(130, 70));
        gestures.Release(new Vector2D(130, 70));

        build.Selection.Nodes.ShouldBeEmpty();
        build.Selection.AngleSensors.ShouldBe([_angleSensorId]);
    }

    [Fact]
    public void ItsPanel_ShowsItsNameNotePickersAndDelete_AndNoSliders()
    {
        var build = new BuildViewModel();
        build.Load(WithAnAngleSensor());
        build.SelectOnly(CreatureElementKind.AngleSensor, _angleSensorId);

        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.Kind.ShouldBe(PartKind.AngleSensor);
        part.Title.ShouldBe(UiText.Plain("Angle sensor"));
        part.Name.ShouldBe(UiText.Format("Angle {0}", 1));
        part.Note.ShouldBe(UiText.Plain("Tells the brain the angle between its two sides, and how fast it turns."));
        part.CanDelete.ShouldBeTrue();
        part.Settings.ShouldBeEmpty();
        part.Pickers!.Select(picker => (picker.Label, picker.SelectedIndex, picker.Note)).ShouldBe(
        [
            (UiText.Plain("Fixed part"), 0, null),
            (UiText.Plain("Target part"), 1, null),
        ]);
        part.OnThisJoint.ShouldBeNull();
    }

    [Fact]
    public void APicker_SwapsItsSides_KeepsItsId_AndUndoes()
    {
        var build = new BuildViewModel();
        build.Load(WithAnAngleSensor());
        build.SelectOnly(CreatureElementKind.AngleSensor, _angleSensorId);

        build.SetHingePartLink(_angleSensorId, fixedRole: true, linkId: 5);

        build.AngleSensors.Single().ShouldBe(new AngleSensorDef(_angleSensorId, 2, fixedLinkId: 5, targetLinkId: 4));
        new BuildPresentationViewModel(build).SinglePart!.Pickers!.Select(picker => picker.SelectedIndex).ShouldBe([1, 0]);
        build.Undo();
        build.AngleSensors.Single().ShouldBe(new AngleSensorDef(_angleSensorId, 2, fixedLinkId: 4, targetLinkId: 5));
    }

    [Fact]
    public void OnThisJoint_ListsItWithTheJointsOtherParts_AndItsPickersOfferTheWheel()
    {
        var build = TwoBeams();
        var wheel = build.PlacePart(BuildPart.Wheel, _endJoint)!.Value;
        var angleSensor = build.PlacePart(BuildPart.AngleSensor, _endJoint)!.Value;

        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.OnThisJoint!.Select(tab => (tab.Kind, tab.Part)).ShouldBe([
            (PartKind.AngleSensor, new CreatureElementSelection(CreatureElementKind.AngleSensor, angleSensor)),
            (PartKind.Wheel, new CreatureElementSelection(CreatureElementKind.Wheel, wheel)),
        ]);
        part.Pickers![1].LinkIdAt(part.Pickers[1].SelectedIndex!.Value).ShouldBe(wheel);
    }

    [Fact]
    public void RenamePart_NamesIt_AndIsAnUndoStep()
    {
        var build = new BuildViewModel();
        build.Load(WithAnAngleSensor());

        build.RenamePart(_angleSensorId, "Knee", shownDefault: null);

        build.PartDisplayName(_angleSensorId).ShouldBe(UiText.AsWritten("Knee"));
        build.Undo();
        build.AngleSensors.Single().Name.ShouldBeNull();
    }

    [Fact]
    public void Delete_RemovesIt_KeepsItsJointAndLinks_AndUndoBringsItBackSelected()
    {
        var build = new BuildViewModel();
        build.Load(WithAnAngleSensor());
        build.SelectOnly(CreatureElementKind.AngleSensor, _angleSensorId);

        build.DeleteSelectedParts();

        build.AngleSensors.ShouldBeEmpty();
        build.Nodes.Count.ShouldBe(3);
        build.Beams.Count.ShouldBe(2);
        build.Undo();
        build.AngleSensors.ShouldBe([new AngleSensorDef(_angleSensorId, 2, fixedLinkId: 4, targetLinkId: 5)]);
        build.Selection.AngleSensors.ShouldBe([_angleSensorId]);
    }

    [Fact]
    public void Copy_WithBothSides_TakesItOntoTheCopiedLinks_AndSelectsTheCopy()
    {
        var build = new BuildViewModel();
        build.Load(WithAnAngleSensor());
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 1, 3 }, Beams = new HashSet<int> { 4, 5 }, AngleSensors = new HashSet<int> { _angleSensorId } });

        build.CopySelectedParts();

        var copy = build.AngleSensors.Single(angleSensor => angleSensor.Id != _angleSensorId);
        var beams = build.Beams.Skip(2).Select(beam => beam.Id).ToArray();
        (copy.FixedLinkId, copy.TargetLinkId).ShouldBe((beams[0], beams[1]));
        copy.NodeId.ShouldBe(build.Nodes.Skip(3).ElementAt(1).Id);
        copy.Name.ShouldBeNull();
        build.Selection.Parts.ShouldBe([
            new CreatureElementSelection(CreatureElementKind.Node, build.Nodes[3].Id),
            new CreatureElementSelection(CreatureElementKind.Node, build.Nodes[5].Id),
            new CreatureElementSelection(CreatureElementKind.Beam, beams[0]),
            new CreatureElementSelection(CreatureElementKind.Beam, beams[1]),
            new CreatureElementSelection(CreatureElementKind.AngleSensor, copy.Id),
        ], ignoreOrder: true);
    }

    [Fact]
    public void Copy_WithOneSideLeftOut_LeavesThatSideMissing()
    {
        var build = new BuildViewModel();
        build.Load(WithAnAngleSensor());
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 1 }, Beams = new HashSet<int> { 4 }, AngleSensors = new HashSet<int> { _angleSensorId } });

        build.CopySelectedParts();

        var copy = build.AngleSensors.Single(angleSensor => angleSensor.Id != _angleSensorId);
        copy.FixedLinkId.ShouldBe(build.Beams.Last().Id);
        copy.TargetLinkId.ShouldBeNull();
        CreatureReadiness.CanTrain(build.Snapshot()).ShouldBeFalse();
    }

    [Fact]
    public void Copy_AlongsideAnotherSelectedPartOnItsJoint_CopiesBoth_ButNotAnUnselectedOne()
    {
        var build = TwoBeams();
        var rubber = build.PlacePart(BuildPart.Rubber, _middleJoint)!.Value;
        build.PlacePart(BuildPart.TouchSensor, _middleJoint);
        var angleSensor = build.PlacePart(BuildPart.AngleSensor, _middleJoint)!.Value;
        build.ReplaceSelection(PartSet.None with
        {
            Nodes = new HashSet<int> { 1, 3 },
            Beams = new HashSet<int> { 4, 5 },
            AngleSensors = new HashSet<int> { angleSensor },
            Rubbers = new HashSet<int> { rubber },
        });

        build.CopySelectedParts();

        var copy = build.AngleSensors.Single(part => part.Id != angleSensor);
        build.Rubbers.Select(part => part.NodeId).ShouldBe([2, copy.NodeId]);
        build.TouchSensors.Select(part => part.NodeId).ShouldBe([2]);
        build.Selection.AngleSensors.ShouldBe([copy.Id]);
        build.Selection.Rubbers.ShouldBe([build.Rubbers[1].Id]);
    }

    [Fact]
    public void Locked_PlacingIt_IsRefused_WithoutANote()
    {
        var build = new BuildViewModel();
        build.Load(TwoBeams().Snapshot(), locked: true);

        build.CanPlacePart(BuildPart.AngleSensor, _middleJoint, out var reason).ShouldBeFalse();
        reason.ShouldBe(BuildViewModel.LockedReason);
        build.PlacePart(BuildPart.AngleSensor, _middleJoint).ShouldBeNull();

        build.AngleSensors.ShouldBeEmpty();
        build.PlacementNote.ShouldBeNull();
    }

    [Fact]
    public void Locked_CopyingIt_IsBlocked_WithANoteOnIt()
    {
        var build = new BuildViewModel();
        build.Load(WithAnAngleSensor(), locked: true);
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 1, 3 }, Beams = new HashSet<int> { 4, 5 }, AngleSensors = new HashSet<int> { _angleSensorId } });

        build.CopyBlockers().ShouldBe([new CanvasNote(CanvasNoteKind.Danger, _angleSensor, UiText.Plain("Locked: would change the model"))]);
        build.CopySelectedParts();

        build.AngleSensors.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(CreatureElementKind.AngleSensor, _angleSensorId)]
    [InlineData(CreatureElementKind.Node, 2)]
    [InlineData(CreatureElementKind.Beam, 4)]
    public void Locked_DeletingIt_ItsJoint_OrASide_IsRefused_WithANoteOnIt(CreatureElementKind kind, int id)
    {
        var build = new BuildViewModel();
        build.Load(WithAnAngleSensor(), locked: true);
        build.SelectOnly(kind, id);

        build.DeleteLockedReason.ShouldBe(BuildViewModel.LockedReason);
        build.DeleteSelectedParts();

        build.AngleSensors.ShouldBe([new AngleSensorDef(_angleSensorId, 2, fixedLinkId: 4, targetLinkId: 5)]);
        build.CanvasNotes().ShouldContain(new CanvasNote(CanvasNoteKind.Danger, _angleSensor, UiText.Plain("Locked: would change the model")));
    }

    // Its ports do not depend on its sides, which keep its id, so a locked Creation renames and relinks it as a Motor (#1179).
    [Fact]
    public void Locked_RenamingAndRelinkingIt_AreAllowed()
    {
        var build = new BuildViewModel();
        build.Load(WithAnAngleSensor(), locked: true);
        build.SelectOnly(CreatureElementKind.AngleSensor, _angleSensorId);

        new BuildPresentationViewModel(build).SinglePart!.CanDelete.ShouldBeFalse();
        build.RenamePart(_angleSensorId, "Knee", shownDefault: null);
        build.SetHingePartLink(_angleSensorId, fixedRole: false, linkId: 4);

        build.AngleSensors.Single().ShouldBe(new AngleSensorDef(_angleSensorId, 2, fixedLinkId: 5, targetLinkId: 4, name: "Knee"));
        build.IsLocked.ShouldBeTrue();
    }

    [Fact]
    public void ItsTrainingCallout_ShowsItsThreeInputs_AsLiveBars()
    {
        var build = TwoBeams();
        var id = build.PlacePart(BuildPart.AngleSensor, _middleJoint)!.Value;
        var creature = build.Snapshot();
        var layout = BrainPorts.Of(creature);
        int[] layers = [layout.Inputs.Count, layout.Outputs.Count];
        var brain = NeuralNetwork.FromGenome(layers, new double[NeuralNetwork.GenomeLength(layers)], Activation.Tanh);
        var values = BrainPortValues.Sample(layout, brain, [0.5, -0.5, 0.25]);

        var callout = PartCalloutPresentation.For(creature, new CreatureElementSelection(CreatureElementKind.AngleSensor, id), values);

        callout.Kind.ShouldBe(PartKind.AngleSensor);
        callout.Inputs.Select(port => (port.Label, port.Range, port.Value)).ShouldBe([
            (UiText.Plain("angle sin"), PortRange.MinusOneToOne, 0.5),
            (UiText.Plain("angle cos"), PortRange.MinusOneToOne, -0.5),
            (UiText.Plain("speed"), PortRange.MinusOneToOne, 0.25),
        ]);
        callout.Outputs.ShouldBeEmpty();
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

    // Joint 2 between beams 4 and 5 holds Angle sensor 6.
    private static CreatureDef WithAnAngleSensor() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0))],
        [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [new AngleSensorDef(_angleSensorId, 2, fixedLinkId: 4, targetLinkId: 5)],
        nextPartId: 7);

    private static void Tap(BuildGestures gestures, Vector2D position)
    {
        gestures.Press(position);
        gestures.Release(position);
    }
}
