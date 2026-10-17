using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using static NodeRunner.App.Tests.ViewModels.BuildFixtures;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildPresentationViewModelTests
{
    [Theory]
    [InlineData(BuildTool.Parts, ToolPanelMode.PartsTray, "Parts")]
    [InlineData(BuildTool.Beam, ToolPanelMode.LinkList, "Links")]
    [InlineData(BuildTool.Joint, ToolPanelMode.JointHelp, "Joint")]
    [InlineData(BuildTool.Select, ToolPanelMode.SelectHelp, "Select")]
    public void ToolPanel_ShowsTheToolsHelp_UntilASelectionReplacesIt(BuildTool tool, ToolPanelMode mode, string title)
    {
        var build = Loaded(PairCreature());
        build.ActiveTool = tool;
        var presentation = new BuildPresentationViewModel(build);
        (presentation.ToolPanel.Mode, presentation.ToolPanel.Title).ShouldBe((mode, UiText.Plain(title)));
        (presentation.LinkList is not null).ShouldBe(tool == BuildTool.Beam);

        build.ToggleSelected(Joint(1));
        (presentation.ToolPanel.Mode, presentation.LinkList).ShouldBe((ToolPanelMode.None, null));
    }

    [Fact]
    public void TheLinkListAndTray_MarkThePick_AndWhatALockedCreationKeeps()
    {
        var build = new BuildViewModel { ActiveTool = BuildTool.Beam };
        var presentation = new BuildPresentationViewModel(build);
        presentation.LinkList!.Rows.Select(row => (row.Link, row.State)).ShouldBe([
            (BuildLink.Beam, LinkListRowState.Selected), (BuildLink.Piston, LinkListRowState.Rest),
            (BuildLink.Spring, LinkListRowState.Rest), (BuildLink.Wing, LinkListRowState.Locked)]);
        var changes = 0;
        presentation.PresentationChanged += (_, _) => changes++;
        build.PickLink(BuildLink.Piston);
        changes.ShouldBe(1);
        presentation.LinkList!.PickedInfo.ShouldBe(UiText.Format("{0}\n{1}", PartInfo.Piston, BuildLinkList.DrawHelp));
        presentation.LinkList.Rows.Take(2).Select(row => row.State).ShouldBe([LinkListRowState.Rest, LinkListRowState.Selected]);

        build = Loaded(PairCreature(), locked: true);
        build.ActiveTool = BuildTool.Beam;
        new BuildPresentationViewModel(build).LinkList!.Rows.Select(row => row.State).ShouldBe(
            [LinkListRowState.Selected, LinkListRowState.CreationLocked, LinkListRowState.Rest, LinkListRowState.Locked]);
        var tray = new BuildPresentationViewModel(build).Tray;
        var rows = tray.Groups.SelectMany(group => group.Rows).ToList();
        rows.Where(row => row.Part is not (BuildPart.Brake or BuildPart.Wheel or BuildPart.Rubber)).ShouldAllBe(row => !row.IsAvailable);
        rows.Where(row => row.Part is BuildPart.Brake or BuildPart.Wheel or BuildPart.Rubber).ShouldAllBe(row => row.State == PartTrayRowState.Available);
        (tray.HelpText, tray.PickedInfo).ShouldBe((UiText.Plain("Unlock to add parts the network uses."), null));
        new[] { BuildPart.Servo, BuildPart.TouchSensor, BuildPart.Pulse }.Select(part => rows.Single(row => row.Part == part).State).ShouldBe(
            [PartTrayRowState.CreationLocked, PartTrayRowState.CreationLocked, PartTrayRowState.ComingLater]);
    }

    [Fact]
    public void ATrainedCreation_OpensLockedOnTheJointTool_WarnsWhatUnlockLoses_AndKeepsDeleteFromThePorts()
    {
        foreach (var generation in new[] { 1, 2, 12 })
        {
            var trained = new BuildViewModel();
            trained.LoadCreation(new CreationDef(Guid.NewGuid(), "Worm", PairCreature(), TestTraining.State(generation)));
            new BuildPresentationViewModel(trained).ResetTrainingWarning.ShouldBe(UiText.Counted(
                "{1} forgets its {0} generation of training and keeps its body. Copy it first to keep the trained one.",
                "{1} forgets its {0} generations of training and keeps its body. Copy it first to keep the trained one.",
                generation,
                "Worm"));
        }

        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(Guid.NewGuid(), "Worm", PairCreature(), TestTraining.State(3, 1, TestTraining.Run)));
        var presentation = new BuildPresentationViewModel(build);
        (presentation.ActiveTool, presentation.ToolPanel.Mode, presentation.IsLocked, presentation.IsTrained, presentation.CanCopy).ShouldBe(
            (BuildTool.Joint, ToolPanelMode.JointHelp, true, true, true));
        build.ToggleSelected(Joint(1));
        presentation.SinglePart!.CanDelete.ShouldBeFalse();
        build.ReplaceSelection([1, 2]);
        (presentation.Selection!.Title, presentation.Selection.CanDelete, presentation.Selection.CanCopy).ShouldBe(
            (UiText.Counted("{0} selected", "{0} selected", 2), false, true));
        var changes = 0;
        presentation.PresentationChanged += (_, _) => changes++;
        build.Unlock();
        (presentation.IsLocked, presentation.IsTrained, build.TrainingGeneration).ShouldBe((false, true, 3));
        changes.ShouldBeGreaterThan(0);

        build = Loaded(PistonCreature(), locked: true);
        build.SelectOnly(CreatureElementKind.Beam, 102);
        new BuildPresentationViewModel(build).SinglePart!.CanDelete.ShouldBeTrue();
        build = Loaded(SpringCreature(), locked: true);
        build.ReplaceSelection(Set(Beam(101), new(CreatureElementKind.Spring, 401)));
        new BuildPresentationViewModel(build).Selection!.CanDelete.ShouldBeTrue();
        (new BuildPresentationViewModel(new BuildViewModel()).CanCopy, new BuildPresentationViewModel(Loaded(PairCreature())).CanCopy).ShouldBe((false, true));
    }

    [Fact]
    public void ASinglePart_ShowsItsNameWhereItSitsAndWhatItDoes_ButNoneOrSeveralShowNoPanel()
    {
        var build = Loaded(new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(20, 5), "Knee"), new NodeDef(3, new Vector2D(40, 0)),
                new BeamDef(101, 1, 2), new BeamDef(102, 2, 3, "Shin"), new SensorDef(7, 102, SensorKind.Accelerometer),
            ]));
        var presentation = new BuildPresentationViewModel(build);
        (presentation.SinglePart, presentation.Selection).ShouldBe((null, null));

        build.SelectOnly(CreatureElementKind.Beam, 101);
        var part = presentation.SinglePart!;
        part.ShouldBe(new PartSettingsPresentation(
            101, PartKind.Beam, UiText.Format("Beam {0}", 1), UiText.Format("Beam {0}", 1), UiText.Plain("Between"),
            UiText.Format("{0} ↔ {1}", UiText.Format("Joint {0}", 1), UiText.AsWritten("Knee")), PartInfo.Beam, CanDelete: true, part.Settings));
        part.Settings.ShouldBeEmpty();

        build.SelectOnly(CreatureElementKind.Sensor, 7);
        part = presentation.SinglePart!;
        part.ShouldBe(new PartSettingsPresentation(
            7, PartKind.Accelerometer, UiText.Format("Accel {0}", 1), UiText.Format("Accel {0}", 1), UiText.Plain("On"), UiText.AsWritten("Shin"),
            UiText.Plain("Measures a beam's acceleration."), CanDelete: true, part.Settings));
        (part.Settings.Count, part.AdvancedSettings.Count).ShouldBe((0, 0));

        build.SelectOnly(CreatureElementKind.Node, 2);
        part = presentation.SinglePart!;
        part.ShouldBe(new PartSettingsPresentation(
            2, PartKind.Node, UiText.AsWritten("Knee"), UiText.Format("Joint {0}", 2), null, null,
            UiText.Plain("Links meet and turn here. Drag it to move them."), CanDelete: true, part.Settings));

        build.ToggleSelected(Joint(1));
        presentation.SinglePart.ShouldBeNull();
        presentation.Selection.ShouldNotBeNull();

        // The notes speak of what a sensor feels, not of the network.
        foreach (var (kind, aimable) in new[] { (SensorKind.Accelerometer, false), (SensorKind.Camera, false), (SensorKind.Camera, true) })
        {
            var note = BuildPresentationViewModel.SensorNote(kind, aimable).Message.ToLowerInvariant();
            new[] { "network", "port", "neuron", "input", "layer" }.ShouldAllBe(word => !note.Contains(word));
        }
    }

    [Fact]
    public void ACamerasPanel_ShowsItsRaysSpreadAndRange_WithSpreadIdleForOneRay_AndRaysLockedWhenTrained()
    {
        var aim = UiText.Plain("Measures distance to the ground. Drag the round handle to aim it.");
        var build = Loaded(Cameras(3));
        build.SelectOnly(CreatureElementKind.Sensor, 201);
        var part = new BuildPresentationViewModel(build).SinglePart!;
        (part.Kind, part.Name, part.Note).ShouldBe((PartKind.Camera, UiText.Format("Camera {0}", 1), aim));
        part.Settings.Select(setting => setting.Label).ShouldBe([UiText.Plain("Rays"), UiText.Plain("Spread"), UiText.Plain("Range")]);
        part.Settings.Select(setting => setting.Readout).ShouldBe([
            UiText.Format("{0}", new FixedNumber(3, 0)), UiText.Format("{0}°", new FixedNumber(90, 0)), UiText.Format("{0} m", new FixedNumber(2.2, 1))]);
        part.Settings.ShouldAllBe(setting => !setting.Disabled && !setting.Advanced);
        part.AdvancedSettings.ShouldBeEmpty();

        build = Loaded(Cameras(1));
        build.SelectOnly(CreatureElementKind.Sensor, 201);
        var settings = new BuildPresentationViewModel(build).SinglePart!.Settings;
        settings.Select(setting => (setting.Locked, setting.NoEffect)).ShouldBe([(false, false), (false, true), (false, false)]);
        PartParameters.Of(settings[1].Id).NoEffectReason.ShouldNotBeNull();

        build = Loaded(Cameras(3), locked: true);
        build.SelectOnly(CreatureElementKind.Sensor, 201);
        part = new BuildPresentationViewModel(build).SinglePart!;
        part.Settings.Select(setting => (setting.Locked, setting.NoEffect)).ShouldBe([(true, false), (false, false), (false, false)]);
        part.Note.ShouldBe(aim);

        build = Loaded(Cameras(1, 5));
        build.ReplaceSelection(Set(new(CreatureElementKind.Sensor, 201), new(CreatureElementKind.Sensor, 202)));
        settings = new BuildPresentationViewModel(build).Selection!.Settings;
        settings.Select(setting => setting.Id).ShouldBe([PartParameterId.Rays, PartParameterId.Spread, PartParameterId.CameraRange]);
        (settings[0].Readout, settings[0].ValuesDiffer).ShouldBe((UiText.Format("{0}–{1}", new FixedNumber(1, 0), new FixedNumber(5, 0)), true));
        settings.ShouldAllBe(setting => !setting.Disabled);
    }

    [Fact]
    public void ALinksOrMotorsPanel_HasASliderPerSetting_SplitIntoBasicAndAdvanced_WhichStaysOpenUntilTheNextVisit()
    {
        var build = Loaded(TwoPistonCreature(stroke: 0.3));
        var presentation = new BuildPresentationViewModel(build);
        build.SelectOnly(CreatureElementKind.Piston, 301);
        var part = presentation.SinglePart!;
        part.Note.ShouldBe(PartInfo.Piston);
        part.Settings.Select(slider => slider.Readout).ShouldBe([
            UiText.Format("{0} N", new FixedNumber(250, 0)), UiText.Format("{0}%", new FixedNumber(30, 0)), UiText.Format("{0}%", new FixedNumber(50, 0)),
            UiText.Format("{0} m/s", new FixedNumber(2, 1)), UiText.Format("{0} s", new FixedNumber(0.2, 1))]);
        part.Settings.ShouldAllBe(slider => !slider.ValuesDiffer);
        part.BasicSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.Strength, PartParameterId.Stroke]);
        part.AdvancedSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.StartPosition, PartParameterId.MaxSpeed, PartParameterId.RiseTime]);
        presentation.AdvancedSettingsOpen.ShouldBeFalse();
        build.AdvancedSettingsOpen = true;
        build.ReplaceSelection(Set(new(CreatureElementKind.Piston, 301), new(CreatureElementKind.Piston, 302)));
        presentation.AdvancedSettingsOpen.ShouldBeTrue();
        presentation.Selection!.AdvancedSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.StartPosition, PartParameterId.MaxSpeed, PartParameterId.RiseTime]);
        build.Load(TwoPistonCreature(stroke: 0.3));
        presentation.AdvancedSettingsOpen.ShouldBeFalse();

        build = Loaded(SpringCreature());
        build.SelectOnly(CreatureElementKind.Spring, 401);
        part = new BuildPresentationViewModel(build).SinglePart!;
        (part.Kind, part.Note).ShouldBe((PartKind.Spring, PartInfo.Spring));
        part.Settings.Select(slider => slider.Readout).ShouldBe([
            UiText.Format("{0} N/m", new FixedNumber(400, 0)), UiText.Format("{0} N·s/m", new FixedNumber(10, 0)),
            UiText.Format("{0}%", new FixedNumber(100, 0)), UiText.Format("{0}%", new FixedNumber(67, 0))]);
        part.BasicSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.Stiffness, PartParameterId.Stroke]);
        part.AdvancedSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.Damping, PartParameterId.CoilLength]);

        // Servos saved on the old 15°/s steps can sit between two 5 rpm steps: 105°/s is 17.5 rpm.
        var offStep = 105 * Math.PI / 180;
        build = Loaded(TwoBeamsDef(new ServoDef(6, 2, 4, 5, maxSpeed: offStep)));
        build.SelectOnly(CreatureElementKind.Servo, 6);
        part = new BuildPresentationViewModel(build).SinglePart!;
        part.BasicSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.ServoStrength, PartParameterId.Range]);
        part.AdvancedSettings.Select(slider => slider.Id).ShouldBe(
            [PartParameterId.ServoStartPosition, PartParameterId.AngularMaxSpeed, PartParameterId.RiseTime]);
        var speed = part.AdvancedSettings.Single(slider => slider.Id == PartParameterId.AngularMaxSpeed);
        speed.Readout.ShouldBe(UiText.Format("{0} rpm", new FixedNumber(18, 0)));
        speed.High.ShouldBe(12.5 / 115, tolerance: 1e-9);
        (build.Servos.Single().MaxSpeed, build.CanUndo).ShouldBe((offStep, false));
    }

    [Fact]
    public void ASelectionPanel_SharesWhatItsPartsHave_ShowingDifferencesAsARange_AndSaysWhatADeleteTakes()
    {
        var build = Loaded(PistonCreature());
        var presentation = new BuildPresentationViewModel(build);
        build.ReplaceSelection([1, 2, 3]);
        var selection = presentation.Selection!;
        selection.Settings.ShouldBeEmpty();
        selection.ShouldBe(new SelectionPanelPresentation(
            UiText.Counted("{0} selected", "{0} selected", 3), selection.Settings, SettingsNote: null, EmptyNote: null, ShowFrameRows: true,
            UiText.Counted("Delete {0}", "Delete {0}", 3), UiText.Plain("Links on a deleted joint go with it."), CanDelete: true,
            UiText.Counted("Copy {0}", "Copy {0}", 3), CanCopy: true));

        build.ReplaceSelection(Set(Joint(1), new(CreatureElementKind.Piston, 301)));
        (presentation.Selection!.Settings.Count, presentation.Selection.ShowFrameRows, presentation.Selection.EmptyNote, presentation.Selection.CanCopy).ShouldBe(
            (0, false, UiText.Plain("These parts share no settings."), false));
        build.ReplaceSelection(Set(Beam(101), Beam(102)));
        presentation.Selection!.DeleteNote.ShouldBe(UiText.Plain("A sensor on a deleted beam goes with it."));

        build = Loaded(TwoPistonCreature(stroke: 0.3));
        build.ReplaceSelection(Set(new(CreatureElementKind.Piston, 301), new(CreatureElementKind.Piston, 302)));
        selection = new BuildPresentationViewModel(build).Selection!;
        (selection.ShowFrameRows, selection.EmptyNote, selection.DeleteNote).ShouldBe((false, null, null));
        selection.SettingsNote.ShouldBe(UiText.Plain("A slider sets one value for all of them."));
        selection.BasicSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.Strength, PartParameterId.Stroke]);
        var strength = selection.Settings[0];
        (strength.Readout, strength.ValuesDiffer).ShouldBe((UiText.Format("{0}–{1} N", new FixedNumber(100, 0), new FixedNumber(250, 0)), true));
        (strength.Low, strength.High).ShouldBe((PartParameters.Strength.Slider!.Range.Position(100), PartParameters.Strength.Slider!.Range.Position(250)));
        selection.Settings[1].ShouldBe(new ParameterSlider(PartParameterId.Stroke, UiText.Plain("Stroke"), UiText.Format("{0}%", new FixedNumber(30, 0)), 20.0 / 190, 20.0 / 190, 5.0 / 190));

        build = Loaded(TwoPistonCreature(stroke: 0.3, otherStroke: 0.3001));
        build.ReplaceSelection(Set(new(CreatureElementKind.Piston, 301), new(CreatureElementKind.Piston, 302)));
        new BuildPresentationViewModel(build).Selection!.Settings[1].ValuesDiffer.ShouldBeFalse();
    }

    [Theory]
    [InlineData("empty", "Add joints and links", null, 0, BuildReadiness.Empty)]
    [InlineData("loose joint", "{0} joint not connected", "{0} joints not connected", 1, BuildReadiness.Blocked)]
    [InlineData("two pieces", "{0} piece not connected", "{0} pieces not connected", 2, BuildReadiness.Blocked)]
    [InlineData("two short beams", "{0} beam too short", "{0} beams too short", 2, BuildReadiness.Blocked)]
    [InlineData("zero-length beam", "{0} link has zero length", "{0} links have zero length", 1, BuildReadiness.Blocked)]
    [InlineData("short spring", "{0} spring too short", "{0} springs too short", 1, BuildReadiness.Blocked)]
    [InlineData("short piston", "{0} piston too short", "{0} pistons too short", 1, BuildReadiness.Blocked)]
    [InlineData("two short pistons", "{0} piston too short", "{0} pistons too short", 2, BuildReadiness.Blocked)]
    [InlineData("servo", "{0} part missing a side", "{0} parts missing a side", 1, BuildReadiness.Blocked)]
    [InlineData("servo and motor", "{0} part missing a side", "{0} parts missing a side", 2, BuildReadiness.Blocked)]
    [InlineData("servo and brake", "{0} part missing a side", "{0} parts missing a side", 2, BuildReadiness.Blocked)]
    [InlineData("angle sensor", "{0} part missing a side", "{0} parts missing a side", 1, BuildReadiness.Blocked)]
    [InlineData("piston", "Ready to train", null, 0, BuildReadiness.Ready)]
    [InlineData("pair", "Ready to train", null, 0, BuildReadiness.Ready)]
    public void Readiness_CountsWhatKeepsTheCreatureFromTraining_InOneLine(string creature, string text, string? plural, int count, BuildReadiness state)
    {
        static NodeDef At(int id, double x, double y) => new(id, new Vector2D(x, y));
        // The chip is one line, so a motor's full sentence would not fit it (#1163); joint parts count together (#454).
        IPartDef[] parts = creature switch
        {
            "empty" => [],
            "loose joint" => [At(1, 0, 0)],
            "two pieces" => [At(1, 0, 0), At(2, 90, 0), At(3, 0, 300), At(4, 90, 300), new BeamDef(5, 1, 2), new BeamDef(6, 3, 4)],
            "two short beams" => [At(1, 0, 0), At(2, 50, 0), At(3, 100, 0), new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
            "zero-length beam" => [At(1, 0, 0), At(2, 0, 0), new BeamDef(101, 1, 2)],
            "short spring" => [At(1, 0, 0), At(2, 90, 0), At(3, 0, 20), new BeamDef(101, 1, 2), new PistonDef(301, 2, 3), new SpringDef(401, 1, 3)],
            "short piston" or "two short pistons" =>
                [At(1, 0, 0), At(2, 90, 0), At(3, 0, 50), At(4, 90, 50), new BeamDef(101, 1, 2), new BeamDef(102, 3, 4), new PistonDef(301, 1, 3),
                 creature == "short piston" ? new PistonDef(302, 1, 4) : new PistonDef(302, 2, 4)],
            "servo" => [.. TwoBeamsDef().Parts, new ServoDef(6, 2, fixedLinkId: 4)],
            "servo and motor" => [.. TwoBeamsDef().Parts, new ServoDef(6, 2, fixedLinkId: 4), new VelocityMotorDef(7, 3, fixedLinkId: 5)],
            "servo and brake" => [.. TwoBeamsDef().Parts, new ServoDef(6, 2, fixedLinkId: 4), new BrakeDef(7, 3, targetLinkId: 5)],
            "angle sensor" => [At(1, 0, 0), At(2, 100, 0), new BeamDef(4, 1, 2), new AngleSensorDef(7, 2, fixedLinkId: 4)],
            "piston" => [.. PistonCreature().Parts],
            _ => [.. PairCreature().Parts],
        };
        var build = Loaded(new CreatureDef(parts, nextPartId: 500));

        var readiness = new BuildPresentationViewModel(build).Readiness;

        var expected = plural is null ? UiText.Plain(text) : UiText.Counted(text, plural, count);
        (readiness.Text, readiness.State, readiness.CanStartTraining).ShouldBe((expected, state, state == BuildReadiness.Ready));
    }

    [Fact]
    public void Readiness_FollowsAMotorThatMakesABeamTooShort_AndALoadedAngleSensorsPanelAsksForItsSide()
    {
        // #1212: 60 units is long enough between plain joints (56) but not with a Servo on one (68).
        var build = Loaded(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(60, 0)), new NodeDef(3, new Vector2D(0, 200)), new BeamDef(5, 1, 2), new BeamDef(6, 1, 3)]));
        var presentation = new BuildPresentationViewModel(build);
        var tooShort = UiText.Counted("{0} beam too short", "{0} beams too short", 1);
        presentation.Readiness.Text.ShouldNotBe(tooShort);
        build.PlacePart(BuildPart.Servo, Joint(1)).ShouldNotBeNull();
        presentation.Readiness.Text.ShouldBe(tooShort);

        // A loaded one, from a share code say, has its panel ask for the side.
        build = Loaded(new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new BeamDef(4, 1, 2), new AngleSensorDef(7, 2, fixedLinkId: 4)]));
        build.SelectOnly(CreatureElementKind.AngleSensor, 7);
        build.SingleSelectedAngleSensorId.ShouldBe(7);
        new BuildPresentationViewModel(build).SinglePart!.Pickers!.Select(picker => (picker.SelectedIndex, picker.Placeholder)).ShouldBe(
            [(0, null), (null, UiText.Plain("Pick a Target part"))]);

        var picker = new PartPickerPresentation(
            UiText.Plain("Fixed part"), [new PartPickerOption(4, UiText.Format("Beam {0}", 1), CreatureElementKind.Beam, ServoLinkRole.None)],
            SelectedIndex: null, Note: null, UiText.Plain("Pick a Fixed part"));
        new[] { 0, -1, 1 }.Select(picker.LinkIdAt).ShouldBe([4, null, null]);
    }

    [Fact]
    public void PresentationChanged_RaisesOncePerEdit_AndTheUndoRowsFollowTheHistory()
    {
        var build = new BuildViewModel();
        var presentation = new BuildPresentationViewModel(build);
        var raised = 0;
        presentation.PresentationChanged += (_, _) => raised++;
        build.PlaceNode(new Vector2D(0, 0));
        raised = 0;
        build.PlaceNode(new Vector2D(90, 0));
        raised.ShouldBe(1);

        build = Loaded(PairCreature());
        presentation = new BuildPresentationViewModel(build);
        presentation.CanUndo.ShouldBeFalse();
        presentation.PresentationChanged += (_, _) => raised++;
        var gestures = new BuildGestures(build);
        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(0, 40));
        raised = 0;
        gestures.Release(new Vector2D(0, 40));
        raised.ShouldBeGreaterThan(0);
        presentation.CanUndo.ShouldBeTrue();
        build.Undo();
        (presentation.CanUndo, presentation.CanRedo).ShouldBe((false, true));

        // The first refresh of a delete must not see the removed part still selected.
        presentation.PresentationChanged += (_, _) => _ = presentation.SinglePart;
        build.SelectOnly(CreatureElementKind.Beam, 101);
        build.DeleteSelectedParts();
        build.Beams.ShouldBeEmpty();
        presentation.CanUndo.ShouldBeTrue();
    }

    private static CreatureDef PairCreature() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new BeamDef(101, 1, 2), new SensorDef(201, 101, SensorKind.Accelerometer)]);

    private static CreatureDef PistonCreature() => new(
        [
            new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 0)),
            new BeamDef(101, 1, 2), new BeamDef(102, 2, 3), new SensorDef(201, 101, SensorKind.Accelerometer), new PistonDef(301, 1, 3),
        ]);

    private static CreatureDef SpringCreature() => new(
        [
            new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 0)),
            new BeamDef(101, 1, 2), new PistonDef(301, 1, 3), new SpringDef(401, 2, 3),
        ],
        nextPartId: 402);

    private static CreatureDef TwoPistonCreature(double stroke, double otherStroke = 0.3) => new(
        [
            new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(0, 90)), new NodeDef(4, new Vector2D(90, 90)),
            new BeamDef(101, 1, 2), new BeamDef(102, 3, 4),
            new PistonDef(301, 1, 3, strength: 25000, stroke: stroke), new PistonDef(302, 2, 4, strength: 10000, stroke: otherStroke),
        ]);

    private static CreatureDef Cameras(int rays, int otherRays = 3) => new(
        [
            new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 0)), new BeamDef(101, 1, 2), new BeamDef(102, 2, 3),
            new SensorDef(201, 101, SensorKind.Camera, rays: rays), new SensorDef(202, 102, SensorKind.Camera, rays: otherRays),
        ]);
}
