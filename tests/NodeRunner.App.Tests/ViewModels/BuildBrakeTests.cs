using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>
/// Placing, setting up and relinking a Brake in Build (#1041), as a Servo is, and keeping it through
/// edits, deletes and undo steps (#1039). It has no brain ports, so a locked creation edits it freely.
/// </summary>
public sealed class BuildBrakeTests
{
    private static readonly CreatureElementSelection _endJoint = new(CreatureElementKind.Node, 1);
    private static readonly CreatureElementSelection _middleJoint = new(CreatureElementKind.Node, 2);

    [Fact]
    public void PlacePart_OnAJoint_TakesTheDefaultSidesAndStrength_SelectsIt_AndCanTrain()
    {
        var build = TwoBeams();

        var brakeId = build.PlacePart(BuildPart.Brake, _middleJoint).ShouldNotBeNull();

        build.Brakes.Single().ShouldBe(new BrakeDef(brakeId, 2, fixedLinkId: 4, targetLinkId: 5));
        build.Brakes.Single().Strength.ShouldBe(BrakeDef.DefaultStrength);
        build.SingleSelectedBrakeId.ShouldBe(brakeId);
        build.SelectedBrakeCount.ShouldBe(1);
        build.Selection.Brakes.ShouldBe([brakeId]);
        CreatureReadiness.CanTrain(build.Snapshot()).ShouldBeTrue();
        build.Undo();
        build.Brakes.ShouldBeEmpty();
    }

    [Fact]
    public void PlacePart_OnAOneLinkJointWithAWheel_BrakesTheWheel()
    {
        var build = TwoBeams();
        var wheel = build.PlacePart(BuildPart.Wheel, _endJoint)!.Value;

        build.PlacePart(BuildPart.Brake, _endJoint);

        build.Brakes.Single().FixedLinkId.ShouldBe(4);
        build.Brakes.Single().TargetLinkId.ShouldBe(wheel);
    }

    [Fact]
    public void CanPlacePart_OnABeam_IsRefused()
    {
        var build = TwoBeams();

        build.CanPlacePart(BuildPart.Brake, new CreatureElementSelection(CreatureElementKind.Beam, 4), out var reason).ShouldBeFalse();

        reason.ShouldBe(UiText.Plain("Brakes go on a joint"));
    }

    [Theory]
    [InlineData(BuildPart.Servo, BuildPart.Brake)]
    [InlineData(BuildPart.VelocityMotor, BuildPart.Brake)]
    [InlineData(BuildPart.Brake, BuildPart.Servo)]
    [InlineData(BuildPart.Brake, BuildPart.VelocityMotor)]
    [InlineData(BuildPart.Brake, BuildPart.Brake)]
    public void AJointHoldsOneHingePart(BuildPart first, BuildPart second)
    {
        var build = TwoBeams();
        build.PlacePart(first, _middleJoint);

        build.CanPlacePart(second, _middleJoint, out var reason).ShouldBeFalse();

        reason.ShouldBe(UiText.Plain("One Servo, Stepper, Motor, Brake or Angle sensor per joint"));
    }

    [Fact]
    public void ABrake_SitsAtTheTopOfItsJointsStack_AndATapOnItHitsItsJoint()
    {
        var build = TwoBeams();
        var wheel = build.PlacePart(BuildPart.Wheel, _endJoint)!.Value;
        var brakeId = build.PlacePart(BuildPart.Brake, _endJoint)!.Value;

        build.JointPartsAt(1).ShouldBe([new CreatureElementSelection(CreatureElementKind.Brake, brakeId), new CreatureElementSelection(CreatureElementKind.Wheel, wheel)]);
        build.ReplaceSelection(PartSet.None);
        build.DrawnPartAt(new Vector2D(0, 0), PlacingTargets.None).ShouldBe(_endJoint);
    }

    [Fact]
    public void SelectedBrake_ShowsItsNote_StrengthAndPickers()
    {
        var build = new BuildViewModel();
        build.Load(WithABrake());
        build.SelectOnly(CreatureElementKind.Brake, 6);

        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.Kind.ShouldBe(PartKind.Brake);
        part.Title.ShouldBe(UiText.Plain("Brake"));
        part.Note.ShouldBe(PartInfo.Brake);
        part.BasicSettings.Select(slider => (slider.Id, slider.Readout, slider.Locked)).ShouldBe([(PartParameterId.BrakeStrength, UiText.Format("{0} N·m", new FixedNumber(200, 0)), false)]);
        part.AdvancedSettings.ShouldBeEmpty();
        part.Pickers!.Select(picker => (picker.Label, picker.SelectedIndex, picker.Note)).ShouldBe(
        [
            (UiText.Plain("Fixed part"), 0, null),
            (UiText.Plain("Target part"), 1, null),
        ]);
        part.OnThisJoint.ShouldBeNull();
    }

    [Fact]
    public void OpeningTheSettings_LeavesASavedStrength_EvenOneOffTheSteps()
    {
        var build = new BuildViewModel();
        build.Load(WithABrake(strength: 1500000));
        build.SelectOnly(CreatureElementKind.Brake, 6);

        var slider = new BuildPresentationViewModel(build).SinglePart!.BasicSettings.Single();

        slider.Readout.ShouldBe(UiText.Format("{0} N·m", new FixedNumber(150, 0)));
        build.Brakes.Single().Strength.ShouldBe(1500000);
        build.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SettingStrength_ToLocked_ReadsLocked_AndUndoes()
    {
        var build = new BuildViewModel();
        build.Load(WithABrake());
        build.SelectOnly(CreatureElementKind.Brake, 6);

        build.SetParameter(PartParameterId.BrakeStrength, PartParameters.ValueAt(PartParameterId.BrakeStrength, 1));

        build.Brakes.Single().Locked.ShouldBeTrue();
        var slider = new BuildPresentationViewModel(build).SinglePart!.BasicSettings.Single();
        slider.Readout.ShouldBe(UiText.Plain("Locked"));
        slider.High.ShouldBe(1);
        new CreatureBuilder(build.Snapshot()).IsWithinBuildLimits().ShouldBeTrue();
        build.Undo();
        build.Brakes.Single().Strength.ShouldBe(BrakeDef.DefaultStrength);
        build.Redo();
        build.Brakes.Single().Locked.ShouldBeTrue();
    }

    // Only a Brake's Strength may be infinite, and only upward: Locked (#1041).
    [Theory]
    [InlineData(PartParameterId.BrakeStrength, double.NaN)]
    [InlineData(PartParameterId.BrakeStrength, double.NegativeInfinity)]
    [InlineData(PartParameterId.MotorStrength, double.PositiveInfinity)]
    public void SetParameter_ToAnythingButAFiniteValueOrLocked_Throws(PartParameterId parameter, double value)
    {
        var build = new BuildViewModel();
        build.Load(WithABrake());
        build.SelectOnly(CreatureElementKind.Brake, 6);

        Should.Throw<ArgumentOutOfRangeException>(() => build.SetParameter(parameter, value));

        build.Brakes.Single().Strength.ShouldBe(BrakeDef.DefaultStrength);
    }

    [Fact]
    public void RenamePart_OfABrake_NamesIt_AndIsAnUndoStep()
    {
        var build = new BuildViewModel();
        build.Load(WithABrake());

        build.RenamePart(6, "Knee", shownDefault: null);

        build.Brakes.Single().ShouldBe(new BrakeDef(6, 2, fixedLinkId: 4, targetLinkId: 5, name: "Knee"));
        build.PartDisplayName(6).ShouldBe(UiText.AsWritten("Knee"));
        build.Undo();
        build.Brakes.Single().Name.ShouldBeNull();
    }

    [Fact]
    public void TwoSelectedBrakes_OneLocked_ShowTheirSpanUpToLocked()
    {
        var build = TwoBeams();
        var first = build.PlacePart(BuildPart.Brake, _middleJoint)!.Value;
        var second = build.PlacePart(BuildPart.Brake, _endJoint)!.Value;
        build.SelectOnly(CreatureElementKind.Brake, first);
        build.SetParameter(PartParameterId.BrakeStrength, BrakeDef.LockedStrength);

        build.ReplaceSelection(PartSet.None with { Brakes = new HashSet<int> { first, second } });

        build.SelectedBrakeCount.ShouldBe(2);
        new BuildPresentationViewModel(build).Selection!.Settings.Single(slider => slider.Id == PartParameterId.BrakeStrength).Readout
            .ShouldBe(UiText.Format("{0} N·m–Locked", new FixedNumber(200, 0)));
    }

    // A link change keeps a Brake's or motor's id, so a trained Creation keeps what it learned (#1177).
    [Fact]
    public void OnATrainedCreation_ABrakesAndAMotorsPickers_HaveNoNote()
    {
        var build = new BuildViewModel();
        build.Load(WithABrake(), training: TestTraining.State(9));
        build.SelectOnly(CreatureElementKind.Brake, 6);

        new BuildPresentationViewModel(build).SinglePart!.Pickers!.Select(picker => picker.Note).ShouldBe([null, null]);

        build.Load(WithAVelocityMotor(), training: TestTraining.State(9));
        build.SelectOnly(CreatureElementKind.VelocityMotor, 6);

        new BuildPresentationViewModel(build).SinglePart!.Pickers!.Select(picker => picker.Note).ShouldBe([null, null]);
    }

    [Fact]
    public void SetHingePartLink_OnABrake_SwapsItsSides_KeepsItsId_AndUndoes()
    {
        var build = new BuildViewModel();
        build.Load(WithABrake());
        build.SelectOnly(CreatureElementKind.Brake, 6);

        build.SetHingePartLink(6, fixedRole: true, linkId: 5);

        build.Brakes.Single().ShouldBe(new BrakeDef(6, 2, fixedLinkId: 5, targetLinkId: 4));
        build.SingleSelectedBrakeId.ShouldBe(6);
        build.Undo();
        build.Brakes.Single().ShouldBe(new BrakeDef(6, 2, fixedLinkId: 4, targetLinkId: 5));
        build.SingleSelectedBrakeId.ShouldBe(6);
    }

    [Fact]
    public void ABrakesPickers_OfferItsJointsWheel_AndItsPanelTabsTheWheel()
    {
        var build = TwoBeams();
        var wheel = build.PlacePart(BuildPart.Wheel, _endJoint)!.Value;
        var brakeId = build.PlacePart(BuildPart.Brake, _endJoint)!.Value;

        var part = new BuildPresentationViewModel(build).SinglePart!;
        var pickers = part.Pickers!;
        part.OnThisJoint!.Select(tab => (tab.Kind, tab.Part)).ShouldBe([
            (PartKind.Brake, new CreatureElementSelection(CreatureElementKind.Brake, brakeId)),
            (PartKind.Wheel, new CreatureElementSelection(CreatureElementKind.Wheel, wheel)),
        ]);

        pickers[1].Options.Select(option => (option.LinkId, option.Kind)).ShouldContain((wheel, CreatureElementKind.Wheel));
        pickers[1].LinkIdAt(pickers[1].SelectedIndex!.Value).ShouldBe(wheel);
        build.SetHingePartLink(brakeId, fixedRole: true, linkId: wheel);
        build.Brakes.Single().ShouldBe(new BrakeDef(brakeId, 1, fixedLinkId: wheel, targetLinkId: 4));
    }

    [Fact]
    public void DeletingItsWheel_LeavesTheBrakeASideShort_AndSaysWhy()
    {
        var build = TwoBeams();
        var wheel = build.PlacePart(BuildPart.Wheel, _endJoint)!.Value;
        var brakeId = build.PlacePart(BuildPart.Brake, _endJoint)!.Value;
        build.SelectOnly(CreatureElementKind.Wheel, wheel);

        build.DeleteSelectedParts();

        build.Brakes.Single().ShouldBe(new BrakeDef(brakeId, 1, fixedLinkId: 4, targetLinkId: null));
        CreatureReadiness.CanTrain(build.Snapshot()).ShouldBeFalse();
        build.ShowTrainingBlockers();
        build.CanvasNotes().Single(note => note.Target.Id == brakeId).Text.ShouldBe(UiText.Plain("A Brake needs two links, or a link and a Wheel"));
        build.SelectOnly(CreatureElementKind.Brake, brakeId);
        var pickers = new BuildPresentationViewModel(build).SinglePart!.Pickers!;
        pickers[0].Note.ShouldBeNull();
        pickers[1].Note.ShouldBe(UiText.Plain("A Brake needs two links, or a link and a Wheel"));
    }

    [Fact]
    public void DeletingItsFixedLink_OnAJointWithTwoLinksLeft_AsksForAFixedLink()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0)), new NodeDef(4, new Vector2D(100, 100))],
            [new BeamDef(5, 1, 2), new BeamDef(6, 2, 3), new BeamDef(7, 2, 4)],
            [],
            nextPartId: 8));
        var brakeId = build.PlacePart(BuildPart.Brake, _middleJoint)!.Value;
        build.SelectOnly(CreatureElementKind.Beam, build.Brakes.Single().FixedLinkId!.Value);

        build.DeleteSelectedParts();
        build.ShowTrainingBlockers();

        build.CanvasNotes().Single(note => note.Target.Id == brakeId).Text.ShouldBe(UiText.Plain("Pick a Fixed part"));
        build.SelectOnly(CreatureElementKind.Brake, brakeId);
        var picker = new BuildPresentationViewModel(build).SinglePart!.Pickers![0];
        picker.SelectedIndex.ShouldBeNull();
        picker.Placeholder.ShouldBe(UiText.Plain("Pick a Fixed part"));
    }

    [Fact]
    public void DeleteSelectedParts_RemovesTheBrake_KeepsItsLinks_AndUndoBringsItBack()
    {
        var build = new BuildViewModel();
        build.Load(WithABrake(strength: BrakeDef.LockedStrength));
        build.SelectOnly(CreatureElementKind.Brake, 6);

        build.DeleteSelectedParts();

        build.Brakes.ShouldBeEmpty();
        build.Beams.Count.ShouldBe(2);
        build.Selection.Count.ShouldBe(0);
        build.Undo();
        build.Brakes.ShouldBe([new BrakeDef(6, 2, fixedLinkId: 4, targetLinkId: 5, strength: BrakeDef.LockedStrength)]);
        build.Selection.Brakes.ShouldBe([6]);
    }

    [Fact]
    public void DeletingItsJoint_TakesTheBrake_EvenOnALockedCreation_AndUndoBringsItBack()
    {
        var build = new BuildViewModel();
        build.Load(WithABrake(), locked: true);
        build.SelectOnly(CreatureElementKind.Node, 2);

        build.DeleteLockedReason.ShouldBeNull();
        build.DeleteSelectedParts();

        build.Brakes.ShouldBeEmpty();
        build.Undo();
        build.Brakes.ShouldBe([new BrakeDef(6, 2, fixedLinkId: 4, targetLinkId: 5)]);
    }

    [Fact]
    public void Copy_TakesTheBrakeOntoTheCopiedLinks_WithItsStrength()
    {
        var build = new BuildViewModel();
        build.Load(WithABrake(strength: BrakeDef.LockedStrength));
        build.ReplaceSelection(PartSet.None with
        {
            Nodes = new HashSet<int> { 1, 2, 3 },
            Beams = new HashSet<int> { 4, 5 },
            Brakes = new HashSet<int> { 6 },
        });
        build.CanCopySelection.ShouldBeTrue();

        build.CopySelectedParts();

        var copy = build.Brakes.Where(brake => brake.Id != 6).ShouldHaveSingleItem();
        var beams = build.Beams.Skip(2).Select(beam => beam.Id).ToArray();
        (copy.FixedLinkId, copy.TargetLinkId, copy.Locked).ShouldBe((beams[0], beams[1], true));
        copy.NodeId.ShouldBe(build.Nodes.Skip(3).ElementAt(1).Id);
        build.Selection.Brakes.ShouldBe([copy.Id]);
    }

    [Fact]
    public void Copy_OfABrakeAndItsLinks_BringsItsJointAlong_AndSelectsTheCopy()
    {
        var build = new BuildViewModel();
        build.Load(WithABrake());
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 1, 3 }, Beams = new HashSet<int> { 4, 5 }, Brakes = new HashSet<int> { 6 } });

        build.CopySelectedParts();

        var copy = build.Brakes.Single(brake => brake.Id != 6);
        copy.NodeId.ShouldNotBe(2);
        build.Nodes.ShouldContain(node => node.Id == copy.NodeId);
        build.Nodes.Count.ShouldBe(6);
        build.Selection.Brakes.ShouldBe([copy.Id]);
    }

    [Fact]
    public void Box_OverAStack_SelectsTheBrakeWithTheOtherParts_InPlaceOfTheJoint()
    {
        var build = WithABrakeAndARubber(out var brakes);
        build.ActiveTool = BuildTool.Select;
        var gestures = new BuildGestures(build);

        gestures.Press(new Vector2D(70, -70));
        gestures.Drag(new Vector2D(130, 70));
        gestures.Release(new Vector2D(130, 70));

        build.Selection.Nodes.ShouldBeEmpty();
        build.Selection.Brakes.ShouldBe([brakes.Single().Id]);
        build.Selection.Rubbers.ShouldBe(build.Rubbers.Select(rubber => rubber.Id));
    }

    [Fact]
    public void Select_DragTheSelectedBrake_MovesItsJoint_AndKeepsTheBrakeSelected()
    {
        var build = new BuildViewModel();
        build.Load(WithABrake());
        build.ActiveTool = BuildTool.Select;
        build.SelectOnly(CreatureElementKind.Brake, 6);
        var gestures = new BuildGestures(build);

        gestures.Press(new Vector2D(100, 0));
        gestures.Drag(new Vector2D(100, 40));
        gestures.Release(new Vector2D(100, 40));

        build.Nodes.Single(node => node.Id == 2).Position.ShouldBe(new Vector2D(100, 40));
        build.Selection.Nodes.ShouldBeEmpty();
        build.Selection.Brakes.ShouldBe([6]);
    }

    [Fact]
    public void Locked_PlacingRelinkingSettingAndDeletingABrake_AreAllowed()
    {
        var build = new BuildViewModel();
        build.Load(WithABrake(), locked: true);
        build.SelectOnly(CreatureElementKind.Brake, 6);

        new BuildPresentationViewModel(build).SinglePart!.BasicSettings.Single().Locked.ShouldBeFalse();
        build.SetHingePartLink(6, fixedRole: true, linkId: 5);
        build.SetParameter(PartParameterId.BrakeStrength, BrakeDef.LockedStrength);
        build.Brakes.Single().ShouldBe(new BrakeDef(6, 2, fixedLinkId: 5, targetLinkId: 4, strength: BrakeDef.LockedStrength));

        build.DeleteLockedReason.ShouldBeNull();
        build.DeleteSelectedParts();
        build.Brakes.ShouldBeEmpty();

        build.CanPlacePart(BuildPart.Brake, _middleJoint, out var reason).ShouldBeTrue();
        reason.ShouldBeNull();
        build.PlacePart(BuildPart.Brake, _middleJoint).ShouldNotBeNull();
        build.Brakes.ShouldHaveSingleItem();
    }

    [Fact]
    public void Locked_DeletingABrakesLinks_IsAllowed()
    {
        var build = new BuildViewModel();
        build.Load(WithABrake(), locked: true);
        build.SelectOnly(CreatureElementKind.Beam, 4);

        build.DeleteLockedReason.ShouldBeNull();
        build.DeleteSelectedParts();

        build.Brakes.Single().FixedLinkId.ShouldBeNull();
    }

    [Fact]
    public void Locked_CopyingABrake_IsAllowed()
    {
        var build = new BuildViewModel();
        build.Load(WithABrake(), locked: true);
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 1, 2, 3 }, Beams = new HashSet<int> { 4, 5 }, Brakes = new HashSet<int> { 6 } });

        build.CopyBlockers().ShouldBeEmpty();
        build.CopySelectedParts();

        build.Brakes.Count.ShouldBe(2);
    }

    [Fact]
    public void UndoAndRedo_OfAnUnrelatedEdit_KeepTheBrake()
    {
        var build = WithABrakeAndARubber(out var brakes);

        build.MoveNode(1, new Vector2D(0, 40));
        build.Undo();

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 0));
        build.Snapshot().Brakes.ShouldBe(brakes);

        build.Redo();

        build.Nodes[0].Position.ShouldBe(new Vector2D(0, 40));
        build.Snapshot().Brakes.ShouldBe(brakes);
    }

    // A joint holding a Brake is not bare, so a delete that takes its links keeps it (#973).
    [Fact]
    public void Delete_OfTheLinksOfAJointWithABrake_KeepsTheJointAndTheBrake()
    {
        var build = WithABrakeAndARubber(out _);
        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 4, 5 }, Rubbers = new HashSet<int>(build.Rubbers.Select(rubber => rubber.Id)) });

        build.DeleteSelectedParts();

        build.Nodes.ShouldContain(node => node.Id == 2);
        build.Brakes.ShouldHaveSingleItem().FixedLinkId.ShouldBeNull();
        build.Rubbers.ShouldBeEmpty();
    }

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

    // Joint 2 between beams 4 and 5 holds Brake 6.
    private static CreatureDef WithABrake(double strength = BrakeDef.DefaultStrength) => new(
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
        [new BrakeDef(6, 2, fixedLinkId: 4, targetLinkId: 5, strength: strength)],
        nextPartId: 7);

    // Joint 2 between beams 4 and 5 holds Velocity motor 6.
    private static CreatureDef WithAVelocityMotor() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0))],
        [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [new VelocityMotorDef(6, 2, fixedLinkId: 4, targetLinkId: 5)],
        [],
        nextPartId: 7);

    // Joint 2 between beams 4 and 5 holds a Locked Brake and a Rubber.
    private static BuildViewModel WithABrakeAndARubber(out IReadOnlyList<BrakeDef> brakes)
    {
        var builder = new CreatureBuilder(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0))],
            [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
            [],
            nextPartId: 6));
        var brake = builder.AddBrake(2);
        builder.SetParameter(brake, PartParameterId.BrakeStrength, BrakeDef.LockedStrength);
        builder.AddRubber(2);
        var build = new BuildViewModel();
        build.Load(builder.Build());
        brakes = [.. build.Brakes];
        return build;
    }
}
