using NodeRunner.App.Navigation;
using NodeRunner.App.Services;
using NodeRunner.App.Tests.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class TrainSetupPresentationViewModelTests
{
    [Fact]
    public void NewCreation_StartsFromTheDefault_SaysItHasNotTrained_AndCannotSimulate()
    {
        var setup = new TrainSetupPresentationViewModel(Creation());

        setup.Title.ShouldBe(UiText.Format("Train {0}", "Worm"));
        setup.Subtitle.ShouldBe(UiText.Plain("Not trained yet"));
        setup.MapName.ShouldBe(UiText.Plain("Flat ground"));
        setup.Settings.ShouldBe(TrainSettingsDef.Default);
        setup.Shadows.Readout.ShouldBe(UiText.Number(8));
        setup.RunLength.Readout.ShouldBe(UiText.Format("{0} s", 10));
        setup.Shadows.Step.ShouldBe(1.0 / 98, 1e-9);
        setup.RunLength.Step.ShouldBe(5.0 / 55, 1e-9);
        setup.ShadowsEnds.ShouldBe([UiText.Number(2), UiText.Number(100)]);
        setup.RunLengthEnds.ShouldBe([UiText.Format("{0} s", 5), UiText.Format("{0} s", 60)]);
        setup.Mode.ShouldBe(TrainingRunMode.Train);
        setup.CanSimulate.ShouldBeFalse();
        setup.ModeNote.ShouldBe(UiText.Plain("Shadows race and the network keeps learning."));
        Should.Throw<InvalidOperationException>(() => setup.SetMode(TrainingRunMode.Simulate));
    }

    [Fact]
    public void TrainedCreation_FillsTheSlidersFromItsSavedSettings_AndCanSimulate()
    {
        var setup = new TrainSetupPresentationViewModel(Creation(new TrainSettingsDef(TrainSettingsDef.MaxShadows, TrainSettingsDef.MinRunLengthSeconds), generation: 12));

        setup.Subtitle.ShouldBe(UiText.Counted("{0} generation so far", "{0} generations so far", 12));
        setup.Shadows.Position.ShouldBe(1);
        setup.RunLength.Position.ShouldBe(0);
        setup.CanSimulate.ShouldBeTrue();
        setup.ModeNote.ShouldBe(UiText.Plain("Shadows race and the network keeps learning."));
        setup.Shadows.Disabled.ShouldBeFalse();
        setup.RunLength.Disabled.ShouldBeFalse();
        setup.SetMode(TrainingRunMode.Simulate);
        setup.Mode.ShouldBe(TrainingRunMode.Simulate);
    }

    [Theory]
    [InlineData(-1, TrainSettingsDef.MinShadows)]
    [InlineData(0.5, 51)]
    public void SetShadows_SnapsToAWholeShadowWithinTheRange(double position, int expected)
    {
        var setup = new TrainSetupPresentationViewModel(Creation());

        setup.SetShadows(position);

        setup.Settings.ShouldBe(new TrainSettingsDef(expected, TrainSettingsDef.Default.RunLengthSeconds));
    }

    [Theory]
    [InlineData(1, 60)]
    [InlineData(0.3, 20)]
    public void SetRunLength_SnapsToFiveSecondSteps(double position, int expected)
    {
        var setup = new TrainSetupPresentationViewModel(Creation());

        setup.SetRunLength(position);

        setup.Settings.RunLengthSeconds.ShouldBe(expected);
        setup.RunLength.Readout.ShouldBe(UiText.Format("{0} s", expected));
    }

    [Fact]
    public void SetShadows_MovesTheNote_AndFiresChangedOnlyWhenTheValueChanges()
    {
        var setup = new TrainSetupPresentationViewModel(Creation(creature: ShadowsBudgetTests.Creature(ShadowsBudget.ReferenceParts)));
        var changes = 0;
        setup.Changed += (_, _) => changes++;

        setup.SetShadows(setup.Shadows.Position!.Value);
        setup.SetShadows(1);

        changes.ShouldBe(1);
        setup.Settings.Shadows.ShouldBe(TrainSettingsDef.MaxShadows);
        setup.ShadowsNote.ShouldNotBeNull().Load.ShouldBe(ShadowsLoad.TooMany);
        setup.SetShadows(0);
        setup.ShadowsNote.ShouldNotBeNull().Load.ShouldBe(ShadowsLoad.Smooth);
    }

    [Fact]
    public void Simulate_DimsTheSliders_ToOneShadowUntilYouLeave_AndBackToTrainKeepsTheSettings()
    {
        var setup = new TrainSetupPresentationViewModel(Creation(new TrainSettingsDef(12, 30), generation: 3));
        var before = setup.Shadows;
        var changes = 0;
        setup.Changed += (_, _) => changes++;

        setup.SetMode(TrainingRunMode.Simulate);
        setup.SetMode(TrainingRunMode.Simulate);

        changes.ShouldBe(1);
        setup.Mode.ShouldBe(TrainingRunMode.Simulate);
        setup.ModeNote.ShouldBe(UiText.Plain("Plays the trained network with one shadow. Nothing is learned or saved."));
        setup.Shadows.ShouldBe(new SettingSlider(UiText.Plain("Shadows"), UiText.Number(1), null, setup.Shadows.Step, Disabled: true));
        setup.RunLength.ShouldBe(new SettingSlider(UiText.Plain("Run length"), UiText.Plain("Until you leave"), null, setup.RunLength.Step, Disabled: true));
        setup.Settings.ShouldBe(new TrainSettingsDef(12, 30));
        setup.ShadowsNote.ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => setup.SetShadows(1));
        Should.Throw<InvalidOperationException>(() => setup.SetRunLength(1));

        setup.SetMode(TrainingRunMode.Train);

        setup.Shadows.ShouldBe(before);
        setup.RunLength.Readout.ShouldBe(UiText.Format("{0} s", 30));
    }

    [Fact]
    public void OnlyAPoweredPart_GivesNoWarning()
    {
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 10))],
            [new BeamDef(101, 1, 2), new BeamDef(102, 2, 3)],
            [],
            [new PistonDef(301, 1, 3)]);

        new TrainSetupPresentationViewModel(Creation()).HasPoweredParts.ShouldBeFalse();
        new TrainSetupPresentationViewModel(Creation(creature: creature)).HasPoweredParts.ShouldBeTrue();
    }

    // A small creature runs every shadow smoothly.
    [Theory]
    [InlineData(32, true, ShadowsLoad.Smooth, "The network learns faster with more shadows.")]
    [InlineData(33, true, ShadowsLoad.Caution, "Phone may stutter above {0}.")]
    [InlineData(65, true, ShadowsLoad.TooMany, "Probably too many shadows.")]
    [InlineData(TrainSettingsDef.MaxShadows, false, ShadowsLoad.Smooth, "The network learns faster with more shadows.")]
    public void ShadowsNote_FollowsTheBudget(int shadows, bool referenceCreature, ShadowsLoad load, string message)
    {
        var setup = new TrainSetupPresentationViewModel(Creation(
            new TrainSettingsDef(shadows, 10),
            creature: referenceCreature ? ShadowsBudgetTests.Creature(ShadowsBudget.ReferenceParts) : null));

        var note = setup.ShadowsNote.ShouldNotBeNull();
        note.Load.ShouldBe(load);
        note.Text.ShouldBe(message.Contains("{0}", StringComparison.Ordinal)
            ? UiText.Format(message, ShadowsBudget.ReferenceSmoothLimit)
            : UiText.Plain(message));
    }

    private static CreationDef Creation(TrainSettingsDef? settings = null, int? generation = null, CreatureDef? creature = null) =>
        new(
            Guid.NewGuid(),
            "Worm",
            creature ?? new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0))], [new BeamDef(3, 1, 2)], []),
            generation is { } finished ? TestTraining.State(finished, 1, TestTraining.Run) : null,
            settings);
}
