using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildPresentationViewModelTests
{
    [Fact]
    public void ToolPanel_WithPartsAndNothingSelected_ShowsTray()
    {
        var panel = new BuildPresentationViewModel(new BuildViewModel { ActiveTool = BuildTool.Parts }).ToolPanel;

        panel.Mode.ShouldBe(ToolPanelMode.PartsTray);
        panel.Title.ShouldBe(UiText.Plain("Parts"));
    }

    [Fact]
    public void ToolPanel_WithLinksAndNothingSelected_ShowsLinkList()
    {
        var presentation = new BuildPresentationViewModel(new BuildViewModel { ActiveTool = BuildTool.Beam });

        presentation.ToolPanel.Mode.ShouldBe(ToolPanelMode.LinkList);
        presentation.ToolPanel.Title.ShouldBe(UiText.Plain("Links"));
        presentation.LinkList.ShouldNotBeNull();
    }

    [Fact]
    public void ToolPanel_WithJointAndNothingSelected_ShowsJointHelp()
    {
        var panel = new BuildPresentationViewModel(new BuildViewModel { ActiveTool = BuildTool.Joint }).ToolPanel;

        panel.Mode.ShouldBe(ToolPanelMode.JointHelp);
        panel.Title.ShouldBe(UiText.Plain("Joint"));
    }

    [Fact]
    public void ToolPanel_WithSelectAndNothingSelected_ShowsSelectHelp()
    {
        var panel = new BuildPresentationViewModel(new BuildViewModel { ActiveTool = BuildTool.Select }).ToolPanel;

        panel.Mode.ShouldBe(ToolPanelMode.SelectHelp);
        panel.Title.ShouldBe(UiText.Plain("Select"));
    }

    [Fact]
    public void LinkList_ShowsForUnlockedLinksToolWithNothingSelected()
    {
        var presentation = new BuildPresentationViewModel(new BuildViewModel { ActiveTool = BuildTool.Beam });

        var list = presentation.LinkList.ShouldNotBeNull();
        list.Rows.Select(row => (row.Link, row.State)).ShouldBe([
            (BuildLink.Beam, LinkListRowState.Selected),
            (BuildLink.Piston, LinkListRowState.Rest),
            (BuildLink.Spring, LinkListRowState.Rest),
            (BuildLink.Wing, LinkListRowState.Locked)]);
    }

    [Fact]
    public void PickLink_RaisesPresentationChanged_AndUpdatesLinkList()
    {
        var build = new BuildViewModel { ActiveTool = BuildTool.Beam };
        var presentation = new BuildPresentationViewModel(build);
        var changes = 0;
        presentation.PresentationChanged += (_, _) => changes++;

        build.PickLink(BuildLink.Piston);

        changes.ShouldBe(1);
        var list = presentation.LinkList.ShouldNotBeNull();
        list.PickedInfo.ShouldBe(PartInfo.Piston);
        list.Rows.Single(row => row.Link == BuildLink.Piston).State.ShouldBe(LinkListRowState.Selected);
        list.Rows.Single(row => row.Link == BuildLink.Beam).State.ShouldBe(LinkListRowState.Rest);
    }

    [Fact]
    public void LinkList_HidesWhenAPartIsSelected()
    {
        var build = new BuildViewModel { ActiveTool = BuildTool.Beam };
        build.PlaceNode(new Vector2D(0, 0));
        build.ReplaceSelection([1]);

        new BuildPresentationViewModel(build).LinkList.ShouldBeNull();
    }

    [Fact]
    public void LinkList_OnALockedCreation_LocksOnlyThePiston()
    {
        var build = new BuildViewModel();
        build.Load(PairCreature(), locked: true);
        build.ActiveTool = BuildTool.Beam;

        var list = new BuildPresentationViewModel(build).LinkList.ShouldNotBeNull();

        list.Rows.Select(row => (row.Link, row.State)).ShouldBe([
            (BuildLink.Beam, LinkListRowState.Selected),
            (BuildLink.Piston, LinkListRowState.CreationLocked),
            (BuildLink.Spring, LinkListRowState.Rest),
            (BuildLink.Wing, LinkListRowState.Locked)]);
    }

    [Fact]
    public void PartsTray_OnALockedCreation_ShowsEveryRowLocked_AndHowToUnlock()
    {
        var build = new BuildViewModel();
        build.Load(PairCreature(), locked: true);

        var tray = new BuildPresentationViewModel(build).Tray;
        var groups = tray.Groups;

        groups.SelectMany(group => group.Rows).ShouldAllBe(row => !row.IsAvailable);
        tray.HelpText.ShouldBe(UiText.Plain("Unlock to add parts."));
        tray.PickedInfo.ShouldBeNull();
        var servo = groups.SelectMany(group => group.Rows).Single(row => row.Part == BuildPart.Servo);
        servo.State.ShouldBe(PartTrayRowState.CreationLocked);
        groups.SelectMany(group => group.Rows).Single(row => row.Part == BuildPart.TouchSensor).State.ShouldBe(PartTrayRowState.ComingLater);
    }

    [Theory]
    [InlineData(BuildTool.Parts)]
    [InlineData(BuildTool.Joint)]
    [InlineData(BuildTool.Select)]
    public void LinkList_HidesForOtherTools(BuildTool tool)
    {
        new BuildPresentationViewModel(new BuildViewModel { ActiveTool = tool }).LinkList.ShouldBeNull();
    }

    [Fact]
    public void PartPickerPresentation_LinkAt_ReturnsNothingOutsideTheOptions()
    {
        var picker = new PartPickerPresentation(
            UiText.Plain("Fixed link"),
            [4],
            [UiText.Format("Beam {0}", 1)],
            SelectedIndex: null,
            IsLocked: false,
            Note: null,
            [CreatureElementKind.Beam],
            UiText.Plain("Pick a Fixed link"));

        picker.LinkIdAt(0).ShouldBe(4);
        picker.LinkIdAt(-1).ShouldBeNull();
        picker.LinkKindAt(0).ShouldBe(CreatureElementKind.Beam);
    }

    [Fact]
    public void LockedCreation_OpensOnTheJointTool_WithItsPanel()
    {
        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(
            Guid.NewGuid(),
            "Worm",
            PairCreature(),
            TestTraining.State(3, 1, TestTraining.Run)));
        var presentation = new BuildPresentationViewModel(build);

        presentation.ActiveTool.ShouldBe(BuildTool.Joint);
        presentation.ToolPanel.Mode.ShouldBe(ToolPanelMode.JointHelp);
        presentation.IsLocked.ShouldBeTrue();
        presentation.IsTrained.ShouldBeTrue();
    }

    [Fact]
    public void CanCopy_AnythingDrawn_TrainedOrNot()
    {
        var empty = new BuildViewModel();
        var untrained = new BuildViewModel();
        untrained.Load(PairCreature());
        var trained = new BuildViewModel();
        trained.LoadCreation(new CreationDef(Guid.NewGuid(), "Worm", PairCreature(), TestTraining.State(3)));

        new BuildPresentationViewModel(empty).CanCopy.ShouldBeFalse();
        new BuildPresentationViewModel(untrained).CanCopy.ShouldBeTrue();
        new BuildPresentationViewModel(trained).CanCopy.ShouldBeTrue();
    }

    [Fact]
    public void Selection_ReplacesToolPanel()
    {
        var build = new BuildViewModel { ActiveTool = BuildTool.Select };
        build.Load(PairCreature());
        build.ToggleSelected(new(CreatureElementKind.Node, 1));

        new BuildPresentationViewModel(build).ToolPanel.Mode.ShouldBe(ToolPanelMode.None);
    }

    [Fact]
    public void Unlock_OpensTheBodyForEditing_AndKeepsTheTraining()
    {
        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(Guid.NewGuid(), "Worm", PairCreature(), TestTraining.State(3)));
        var presentation = new BuildPresentationViewModel(build);
        var changes = 0;
        presentation.PresentationChanged += (_, _) => changes++;

        build.Unlock();

        presentation.IsLocked.ShouldBeFalse();
        presentation.IsTrained.ShouldBeTrue();
        build.TrainingGeneration.ShouldBe(3);
        changes.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(12)]
    public void ResetTrainingWarning_NamesTheTrainingThatIsLost(int generation)
    {
        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(Guid.NewGuid(), "Worm", PairCreature(), TestTraining.State(generation)));

        new BuildPresentationViewModel(build).ResetTrainingWarning.ShouldBe(UiText.Counted(
            "{1} forgets its {0} generation of training and keeps its body. Copy it first to keep the trained one.",
            "{1} forgets its {0} generations of training and keeps its body. Copy it first to keep the trained one.",
            generation,
            "Worm"));
    }

    [Fact]
    public void SelectedBeam_ShowsNameEndsAndLengthGuidance()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(3, 4))],
            [new BeamDef(101, 1, 2)],
            []));
        build.SelectOnly(CreatureElementKind.Beam, 101);
        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.ShouldBe(new PartSettingsPresentation(
            101,
            PartSettingsKind.Beam,
            UiText.Format("Beam {0}", 1),
            UiText.Format("Beam {0}", 1),
            UiText.Plain("Between"),
            UiText.Format("{0} ↔ {1}", UiText.Format("Joint {0}", 1), UiText.Format("Joint {0}", 2)),
            PartInfo.Beam,
            CanDelete: true,
            part.Settings));
        part.Settings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(SensorKind.Accelerometer, PartSettingsKind.Accelerometer, "Accel", "Measures its beam's acceleration.")]
    [InlineData(SensorKind.Camera, PartSettingsKind.Camera, "Camera", "Three rays see how near the ground is. Drag the round handle to aim it.")]
    public void SelectedSensor_ShowsNameBeamAndWhatItFeels(SensorKind kind, PartSettingsKind partKind, string name, string note)
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(3, 4))],
            [new BeamDef(101, 1, 2, "Thigh")],
            [new SensorDef(7, 101, kind)]));
        build.SelectOnly(CreatureElementKind.Sensor, 7);
        var presentation = new BuildPresentationViewModel(build);

        var part = presentation.SinglePart!;

        part.ShouldBe(new PartSettingsPresentation(
            7,
            partKind,
            UiText.Format(name + " {0}", 1),
            UiText.Format(name + " {0}", 1),
            UiText.Plain("On"),
            UiText.AsWritten("Thigh"),
            UiText.Plain(note),
            CanDelete: true,
            part.Settings));
        part.Settings.ShouldBeEmpty();
    }

    [Fact]
    public void SelectedCamera_WhenLocked_StillOffersToAimIt()
    {
        var build = new BuildViewModel();
        build.Load(
            new CreatureDef(
                [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(3, 4))],
                [new BeamDef(101, 1, 2)],
                [new SensorDef(7, 101, SensorKind.Camera)]),
            locked: true);
        build.SelectOnly(CreatureElementKind.Sensor, 7);

        new BuildPresentationViewModel(build).SinglePart!.Note.ShouldBe(UiText.Plain("Three rays see how near the ground is. Drag the round handle to aim it."));
    }

    [Fact]
    public void SelectedNode_ShowsNameAndNoConnections()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0)),
                new NodeDef(2, new Vector2D(20, 5), "Knee"),
                new NodeDef(3, new Vector2D(40, 0)),
            ],
            [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3, "Shin")],
            []));
        build.ToggleSelected(new(CreatureElementKind.Node, 2));
        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.ShouldBe(new PartSettingsPresentation(
            2,
            PartSettingsKind.Node,
            UiText.AsWritten("Knee"),
            UiText.Format("Joint {0}", 2),
            null,
            null,
            UiText.Plain("Links meet and turn here. Drag it to move them."),
            CanDelete: true,
            part.Settings));
        part.Settings.ShouldBeEmpty();
    }

    [Fact]
    public void SelectedPiston_HasASliderForEachOfItsSettings()
    {
        var build = new BuildViewModel();
        build.Load(TwoPistonCreature(stroke: 0.3));
        build.SelectOnly(CreatureElementKind.Piston, 301);

        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.Note.ShouldBe(PartInfo.Piston);
        part.Settings.Select(slider => slider.Readout).ShouldBe(
        [
            UiText.Format("{0} N", new FixedNumber(250, 0)),
            UiText.Format("{0}%", new FixedNumber(30, 0)),
            UiText.Format("{0}%", new FixedNumber(50, 0)),
            UiText.Format("{0} m/s", new FixedNumber(2, 1)),
            UiText.Format("{0} s", new FixedNumber(0.2, 1)),
        ]);
        part.Settings.ShouldAllBe(slider => !slider.ValuesDiffer);
    }

    [Fact]
    public void SelectedSpring_HasASliderForEachOfItsSettings()
    {
        var build = new BuildViewModel();
        build.Load(SpringCreature());
        build.SelectOnly(CreatureElementKind.Spring, 401);

        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.Kind.ShouldBe(PartSettingsKind.Spring);
        part.Note.ShouldBe(PartInfo.Spring);
        part.Settings.Select(slider => slider.Readout).ShouldBe(
        [
            UiText.Format("{0} N/m", new FixedNumber(400, 0)),
            UiText.Format("{0} N·s/m", new FixedNumber(10, 0)),
            UiText.Format("{0}%", new FixedNumber(100, 0)),
            UiText.Format("{0}%", new FixedNumber(67, 0)),
        ]);
    }

    [Fact]
    public void BuildPanel_WhenASpringIsTooShort_CountsItInReadiness()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(0, 20))],
            [new BeamDef(101, 1, 2)],
            [],
            [new PistonDef(301, 2, 3)],
            [new SpringDef(401, 1, 3)],
            nextPartId: 402));

        new BuildPresentationViewModel(build).BuildPanel.ReadinessText.ShouldBe(UiText.Counted("{0} spring too short", "{0} springs too short", 1));
    }

    [Fact]
    public void LockedCreation_PartSettingsDimDelete_WhenItTakesASensorAlong()
    {
        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(
            Guid.NewGuid(),
            "Worm",
            PairCreature(),
            TestTraining.State(3, 1, TestTraining.Run)));
        build.ToggleSelected(new(CreatureElementKind.Node, build.Nodes[0].Id));

        new BuildPresentationViewModel(build).SinglePart!.CanDelete.ShouldBeFalse();
    }

    [Fact]
    public void LockedCreation_PartSettingsOfferDelete_ForALinkWithoutPorts()
    {
        var build = new BuildViewModel();
        build.Load(PistonCreature(), locked: true);
        build.SelectOnly(CreatureElementKind.Beam, 102);

        new BuildPresentationViewModel(build).SinglePart!.CanDelete.ShouldBeTrue();
    }

    [Fact]
    public void NoOrManySelected_HasNoPartSettings()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(3, 4))],
            [new BeamDef(101, 1, 2)],
            []));
        var presentation = new BuildPresentationViewModel(build);

        presentation.SinglePart.ShouldBeNull();
        build.ToggleSelected(new(CreatureElementKind.Node, 1));
        build.ToggleSelected(new(CreatureElementKind.Node, 2));
        presentation.SinglePart.ShouldBeNull();
    }

    [Theory]
    [InlineData(SensorKind.Accelerometer, false)]
    [InlineData(SensorKind.Camera, false)]
    [InlineData(SensorKind.Camera, true)]
    public void SensorNote_AvoidsBrainWording(SensorKind kind, bool aimable)
    {
        var note = BuildPresentationViewModel.SensorNote(kind, aimable).Message.ToLowerInvariant();

        foreach (var word in new[] { "brain", "port", "neuron", "input", "layer" })
        {
            note.ShouldNotContain(word);
        }
    }

    [Fact]
    public void Selection_OfJoints_ShowsTheFrameHelp_AndOffersDelete()
    {
        var build = new BuildViewModel();
        build.Load(PistonCreature());
        build.ReplaceSelection([1, 2, 3]);
        var selection = new BuildPresentationViewModel(build).Selection!;

        selection.Settings.ShouldBeEmpty();
        selection.ShouldBe(new SelectionPanelPresentation(
            UiText.Counted("{0} selected", "{0} selected", 3),
            selection.Settings,
            SettingsNote: null,
            EmptyNote: null,
            ShowFrameRows: true,
            UiText.Counted("Delete {0}", "Delete {0}", 3),
            UiText.Plain("Links on a deleted joint go with it."),
            CanDelete: true,
            UiText.Counted("Copy {0}", "Copy {0}", 3),
            CanCopy: true));
    }

    [Fact]
    public void Selection_WithAPartCopyCannotTake_DimsCopy()
    {
        var build = new BuildViewModel();
        build.Load(PistonCreature());
        build.ReplaceSelection(PartSet.None with { Nodes = new HashSet<int> { 1 }, Pistons = new HashSet<int> { 301 } });

        new BuildPresentationViewModel(build).Selection!.CanCopy.ShouldBeFalse();
    }

    [Fact]
    public void Selection_OfPistons_SharesTheirSettings_AndShowsDifferingValuesAsARange()
    {
        var build = new BuildViewModel();
        build.Load(TwoPistonCreature(stroke: 0.3));
        build.ReplaceSelection(PartSet.None with { Pistons = new HashSet<int> { 301, 302 } });

        var selection = new BuildPresentationViewModel(build).Selection!;

        selection.ShowFrameRows.ShouldBeFalse();
        selection.EmptyNote.ShouldBeNull();
        selection.SettingsNote.ShouldBe(UiText.Plain("A slider sets one value for all of them."));
        selection.DeleteNote.ShouldBeNull();
        selection.Settings.Select(slider => slider.Id).ShouldBe(
            [PartParameterId.Strength, PartParameterId.Stroke, PartParameterId.StartPosition, PartParameterId.MaxSpeed, PartParameterId.RiseTime]);
        var strength = selection.Settings[0];
        strength.Readout.ShouldBe(UiText.Format("{0}–{1} N", new FixedNumber(100, 0), new FixedNumber(250, 0)));
        strength.ValuesDiffer.ShouldBeTrue();
        strength.Low.ShouldBe(PartParameters.Strength.Slider!.Range.Position(100));
        strength.High.ShouldBe(PartParameters.Strength.Slider!.Range.Position(250));
        selection.Settings[1].ShouldBe(new ParameterSlider(PartParameterId.Stroke, UiText.Plain("Stroke"), UiText.Format("{0}%", new FixedNumber(30, 0)), 2.0 / 9, 2.0 / 9, 5.0 / 90));
    }

    [Fact]
    public void Selection_OfPistons_SplitsTheirSettingsIntoBasicAndAdvanced()
    {
        var build = new BuildViewModel();
        build.Load(TwoPistonCreature(stroke: 0.3));
        build.ReplaceSelection(PartSet.None with { Pistons = new HashSet<int> { 301, 302 } });

        var selection = new BuildPresentationViewModel(build).Selection!;

        selection.BasicSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.Strength, PartParameterId.Stroke]);
        selection.AdvancedSettings.Select(slider => slider.Id).ShouldBe(
            [PartParameterId.StartPosition, PartParameterId.MaxSpeed, PartParameterId.RiseTime]);
    }

    [Fact]
    public void SelectedServo_SplitsItsSettingsIntoBasicAndAdvanced()
    {
        var build = new BuildViewModel();
        build.Load(ServoCreature());
        build.SelectOnly(CreatureElementKind.Servo, 6);

        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.BasicSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.ServoStrength, PartParameterId.Range]);
        part.AdvancedSettings.Select(slider => slider.Id).ShouldBe(
            [PartParameterId.ServoStartPosition, PartParameterId.AngularMaxSpeed, PartParameterId.RiseTime]);
    }

    [Fact]
    public void SelectedSpring_HasDampingAndCoilLengthUnderAdvanced()
    {
        var build = new BuildViewModel();
        build.Load(SpringCreature());
        build.SelectOnly(CreatureElementKind.Spring, 401);

        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.BasicSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.Stiffness, PartParameterId.Stroke]);
        part.AdvancedSettings.Select(slider => slider.Id).ShouldBe([PartParameterId.Damping, PartParameterId.CoilLength]);
    }

    [Fact]
    public void SelectedSensor_HasNoAdvancedSettings()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0))],
            [new BeamDef(101, 1, 2)],
            [new SensorDef(201, 101, SensorKind.Camera)]));
        build.SelectOnly(CreatureElementKind.Sensor, 201);

        var part = new BuildPresentationViewModel(build).SinglePart!;

        part.AdvancedSettings.ShouldBeEmpty();
    }

    [Fact]
    public void AdvancedSettings_StayOpenAcrossSelections_AndCloseOnTheNextVisit()
    {
        var build = new BuildViewModel();
        build.Load(TwoPistonCreature(stroke: 0.3));
        var presentation = new BuildPresentationViewModel(build);
        presentation.AdvancedSettingsOpen.ShouldBeFalse();

        build.ReplaceSelection(PartSet.None with { Pistons = new HashSet<int> { 301 } });
        build.AdvancedSettingsOpen = true;
        build.ReplaceSelection(PartSet.None with { Pistons = new HashSet<int> { 301, 302 } });
        presentation.AdvancedSettingsOpen.ShouldBeTrue();

        build.Load(TwoPistonCreature(stroke: 0.3));
        presentation.AdvancedSettingsOpen.ShouldBeFalse();
    }

    [Fact]
    public void Selection_ValuesThatShowTheSame_DoNotDiffer()
    {
        var build = new BuildViewModel();
        build.Load(TwoPistonCreature(stroke: 0.3, otherStroke: 0.3001));
        build.ReplaceSelection(PartSet.None with { Pistons = new HashSet<int> { 301, 302 } });

        new BuildPresentationViewModel(build).Selection!.Settings[1].ValuesDiffer.ShouldBeFalse();
    }

    [Fact]
    public void Selection_OfAJointAndAPiston_SharesNothing()
    {
        var build = new BuildViewModel();
        build.Load(PistonCreature());
        build.ReplaceSelection(new PartSet(new HashSet<int> { 1 }, new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int> { 301 }, new HashSet<int>()));

        var selection = new BuildPresentationViewModel(build).Selection!;

        selection.Settings.ShouldBeEmpty();
        selection.ShowFrameRows.ShouldBeFalse();
        selection.EmptyNote.ShouldBe(UiText.Plain("These parts share no settings."));
    }

    [Fact]
    public void Selection_OfBeams_WarnsThatTheirSensorsGoWithThem()
    {
        var build = new BuildViewModel();
        build.Load(PistonCreature());
        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 101, 102 } });

        new BuildPresentationViewModel(build).Selection!.DeleteNote.ShouldBe(UiText.Plain("A sensor on a deleted beam goes with it."));
    }

    [Fact]
    public void Selection_OnALockedCreation_DimsDelete_WhenItTakesASensorAlong_ButOffersCopy()
    {
        var build = new BuildViewModel();
        build.LoadCreation(new CreationDef(
            Guid.NewGuid(),
            "Worm",
            PairCreature(),
            TestTraining.State(3, 1, TestTraining.Run)));
        build.ReplaceSelection([1, 2]);

        var selection = new BuildPresentationViewModel(build).Selection!;
        selection.Title.ShouldBe(UiText.Counted("{0} selected", "{0} selected", 2));
        selection.CanDelete.ShouldBeFalse();
        selection.CanCopy.ShouldBeTrue();
    }

    [Fact]
    public void Selection_OnALockedCreation_OffersDelete_WithoutPortedParts()
    {
        var build = new BuildViewModel();
        build.Load(SpringCreature(), locked: true);
        build.ReplaceSelection(PartSet.None with { Beams = new HashSet<int> { 101 }, Springs = new HashSet<int> { 401 } });

        new BuildPresentationViewModel(build).Selection!.CanDelete.ShouldBeTrue();
    }

    [Fact]
    public void Selection_NeedsSeveralParts()
    {
        var build = new BuildViewModel();
        build.Load(PairCreature());
        var presentation = new BuildPresentationViewModel(build);

        presentation.Selection.ShouldBeNull();
        build.ToggleSelected(new(CreatureElementKind.Node, 1));
        presentation.Selection.ShouldBeNull();
    }

    [Fact]
    public void BuildPanel_WhenBeamsAreTooShort_CountsThemInReadiness()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(50, 0)), new NodeDef(3, new Vector2D(100, 0))],
            [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
            []));
        var presentation = new BuildPresentationViewModel(build);

        presentation.BuildPanel.CanStartTraining.ShouldBeFalse();
        presentation.BuildPanel.ReadinessText.ShouldBe(UiText.Counted("{0} beam too short", "{0} beams too short", 2));
    }

    [Fact]
    public void BuildPanel_WithOnlyAZeroLengthBeam_ShowsTheReasonWithItsNodes()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(0, 0))],
            [new BeamDef(101, 1, 2)],
            []));

        new BuildPresentationViewModel(build).BuildPanel.ReadinessText.ShouldBe(
            UiText.Format("The beam between joint {0} and joint {1} has zero length. Move one of the joints apart.", 1, 2));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void BuildPanel_WhenPistonsAreTooShort_CountsThemInReadiness(int tooShort)
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(0, 50)), new NodeDef(4, new Vector2D(90, 50))],
            [new BeamDef(101, 1, 2), new BeamDef(102, 3, 4)],
            [],
            [new PistonDef(301, 1, 3), tooShort == 2 ? new PistonDef(302, 2, 4) : new PistonDef(302, 1, 4)]));
        var presentation = new BuildPresentationViewModel(build);

        presentation.BuildPanel.CanStartTraining.ShouldBeFalse();
        presentation.BuildPanel.ReadinessText.ShouldBe(UiText.Counted("{0} piston too short", "{0} pistons too short", tooShort));
    }

    [Fact]
    public void BuildPanel_WhenEmpty_AsksForNodesAndBeams()
    {
        var presentation = new BuildPresentationViewModel(new BuildViewModel());

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeFalse();
        buildPanel.ReadinessText.ShouldBe(UiText.Plain("Add joints and links"));
    }

    [Fact]
    public void BuildPanel_WhenANodeIsUnconnected_SaysSoInReadiness()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        var presentation = new BuildPresentationViewModel(build);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeFalse();
        buildPanel.ReadinessText.ShouldBe(UiText.Counted("{0} joint not connected", "{0} joints not connected", 1));
    }

    [Fact]
    public void BuildPanel_WithSeparatePieces_SaysSoInReadiness()
    {
        var build = new BuildViewModel();
        build.Load(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(0, 300)), new NodeDef(4, new Vector2D(90, 300))],
            [new BeamDef(5, 1, 2), new BeamDef(6, 3, 4)],
            []));

        var buildPanel = new BuildPresentationViewModel(build).BuildPanel;

        buildPanel.CanStartTraining.ShouldBeFalse();
        buildPanel.ReadinessText.ShouldBe(UiText.Counted("{0} piece not connected", "{0} pieces not connected", 2));
    }

    [Fact]
    public void BuildPanel_WithAPiston_IsReadyToTrain()
    {
        var build = new BuildViewModel();
        build.Load(PistonCreature());
        var presentation = new BuildPresentationViewModel(build);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeTrue();
        buildPanel.ReadinessText.ShouldBe(UiText.Plain("Ready to train"));
    }

    [Fact]
    public void BuildPanel_WithoutAPiston_IsReadyToTrain()
    {
        var build = new BuildViewModel();
        build.Load(PairCreature());
        var presentation = new BuildPresentationViewModel(build);

        var buildPanel = presentation.BuildPanel;

        buildPanel.CanStartTraining.ShouldBeTrue();
        buildPanel.ReadinessText.ShouldBe(UiText.Plain("Ready to train"));
    }

    [Fact]
    public void PresentationChanged_WhenAnatomyChanges_RaisesForLiveBuildScreenRefresh()
    {
        var build = new BuildViewModel();
        var presentation = new BuildPresentationViewModel(build);
        var raiseCount = 0;
        presentation.PresentationChanged += (_, _) => raiseCount++;

        build.PlaceNode(new Vector2D(0, 0));
        raiseCount = 0;

        build.PlaceNode(new Vector2D(90, 0));

        raiseCount.ShouldBe(1);
    }

    [Fact]
    public void UndoAndRedoRows_FollowTheHistory_EvenWhenOnlyTheHistoryChanges()
    {
        var build = new BuildViewModel();
        build.Load(PairCreature());
        var presentation = new BuildPresentationViewModel(build);
        var gestures = new BuildGestures(build);
        presentation.CanUndo.ShouldBeFalse();
        var raised = false;
        presentation.PresentationChanged += (_, _) => raised = true;

        gestures.Press(new Vector2D(0, 0));
        gestures.Drag(new Vector2D(0, 40));
        raised = false;
        gestures.Release(new Vector2D(0, 40));

        raised.ShouldBeTrue();
        presentation.CanUndo.ShouldBeTrue();
        build.Undo();
        presentation.CanUndo.ShouldBeFalse();
        presentation.CanRedo.ShouldBeTrue();
    }

    [Fact]
    public void TheFirstStepsRefresh_SeesNoRemovedPartSelected()
    {
        var build = new BuildViewModel();
        build.Load(PairCreature());
        var presentation = new BuildPresentationViewModel(build);
        presentation.PresentationChanged += (_, _) => _ = presentation.SinglePart;
        build.SelectOnly(CreatureElementKind.Beam, 101);

        build.DeleteSelectedParts();

        build.Beams.ShouldNotContain(beam => beam.Id == 101);
        presentation.CanUndo.ShouldBeTrue();
    }

    private static CreatureDef PairCreature() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0))],
        [new BeamDef(101, 1, 2)],
        [new SensorDef(201, 101, SensorKind.Accelerometer)]);

    private static CreatureDef PistonCreature() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 0))],
        [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
        [new SensorDef(201, 101, SensorKind.Accelerometer)],
        [new PistonDef(301, 1, 3)]);

    private static CreatureDef SpringCreature() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 0))],
        [new BeamDef(101, 1, 2)],
        [],
        [new PistonDef(301, 1, 3)],
        [new SpringDef(401, 2, 3)],
        nextPartId: 402);

    private static CreatureDef ServoCreature() => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0))],
        [new BeamDef(4, 1, 2), new BeamDef(5, 2, 3)],
        [],
        [new ServoDef(6, 2, 4, 5)],
        [],
        [],
        nextPartId: 7);

    private static CreatureDef TwoPistonCreature(double stroke, double otherStroke = 0.3) => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(0, 90)), new NodeDef(4, new Vector2D(90, 90))],
        [new BeamDef(101, 1, 2), new BeamDef(102, 3, 4)],
        [],
        [new PistonDef(301, 1, 3, strength: 25000, stroke: stroke), new PistonDef(302, 2, 4, strength: 10000, stroke: otherStroke)]);
}
