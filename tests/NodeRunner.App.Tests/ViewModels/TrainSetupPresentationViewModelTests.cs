using NodeRunner.App.Navigation;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class TrainSetupPresentationViewModelTests
{
    [Fact]
    public void NewCreation_StartsFromTheDefault_AndSaysItHasNotTrained()
    {
        var setup = new TrainSetupPresentationViewModel(Creation());

        setup.Title.ShouldBe("Train Worm");
        setup.Subtitle.ShouldBe("Not trained yet");
        setup.MapName.ShouldBe("Flat ground");
        setup.Settings.ShouldBe(TrainSettingsDef.Default);
        setup.Shadows.Readout.ShouldBe("8");
        setup.RunLength.Readout.ShouldBe("10 s");
    }

    [Fact]
    public void SavedSettings_FillTheSliders()
    {
        var setup = new TrainSetupPresentationViewModel(Creation(new TrainSettingsDef(TrainSettingsDef.MaxShadows, TrainSettingsDef.MinRunLengthSeconds), generation: 12));

        setup.Subtitle.ShouldBe("12 generations so far");
        setup.Shadows.Position.ShouldBe(1);
        setup.RunLength.Position.ShouldBe(0);
    }

    [Fact]
    public void Steps_AreOneShadowAndFiveSeconds()
    {
        var setup = new TrainSetupPresentationViewModel(Creation());

        setup.Shadows.Step.ShouldBe(1.0 / 30, 1e-9);
        setup.RunLength.Step.ShouldBe(5.0 / 55, 1e-9);
    }

    [Fact]
    public void Ends_AreTheSettingsLimits()
    {
        var setup = new TrainSetupPresentationViewModel(Creation());

        setup.ShadowsEnds.ShouldBe(["2", "32"]);
        setup.RunLengthEnds.ShouldBe(["5 s", "60 s"]);
    }

    [Theory]
    [InlineData(0, TrainSettingsDef.MinShadows)]
    [InlineData(1, TrainSettingsDef.MaxShadows)]
    [InlineData(-1, TrainSettingsDef.MinShadows)]
    [InlineData(0.5, 17)]
    public void SetShadows_SnapsToAWholeShadowWithinTheRange(double position, int expected)
    {
        var setup = new TrainSetupPresentationViewModel(Creation());

        setup.SetShadows(position);

        setup.Settings.ShouldBe(new TrainSettingsDef(expected, TrainSettingsDef.Default.RunLengthSeconds));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 60)]
    [InlineData(0.3, 20)]
    public void SetRunLength_SnapsToFiveSecondSteps(double position, int expected)
    {
        var setup = new TrainSetupPresentationViewModel(Creation());

        setup.SetRunLength(position);

        setup.Settings.RunLengthSeconds.ShouldBe(expected);
        setup.RunLength.Readout.ShouldBe($"{expected} s");
    }

    [Fact]
    public void Changed_FiresOnlyWhenAValueChanges()
    {
        var setup = new TrainSetupPresentationViewModel(Creation());
        var changes = 0;
        setup.Changed += (_, _) => changes++;

        setup.SetShadows(setup.Shadows.Position!.Value);
        setup.SetShadows(1);

        changes.ShouldBe(1);
    }

    [Fact]
    public void UntrainedCreation_CannotSimulate()
    {
        var setup = new TrainSetupPresentationViewModel(Creation());

        setup.Mode.ShouldBe(TrainingRunMode.Train);
        setup.CanSimulate.ShouldBeFalse();
        setup.ModeNote.ShouldBe("Shadows race and the brain keeps learning. Simulate needs a trained brain.");
        Should.Throw<InvalidOperationException>(() => setup.SetMode(TrainingRunMode.Simulate));
    }

    [Fact]
    public void TrainedCreation_Trains_WithLiveSliders()
    {
        var setup = new TrainSetupPresentationViewModel(Creation(generation: 3));

        setup.CanSimulate.ShouldBeTrue();
        setup.ModeNote.ShouldBe("Shadows race and the brain keeps learning.");
        setup.Shadows.Disabled.ShouldBeFalse();
        setup.RunLength.Disabled.ShouldBeFalse();
    }

    [Fact]
    public void Simulate_DimsTheSliders_ToOneShadowUntilYouLeave()
    {
        var setup = new TrainSetupPresentationViewModel(Creation(generation: 3));
        var changes = 0;
        setup.Changed += (_, _) => changes++;

        setup.SetMode(TrainingRunMode.Simulate);
        setup.SetMode(TrainingRunMode.Simulate);

        changes.ShouldBe(1);
        setup.Mode.ShouldBe(TrainingRunMode.Simulate);
        setup.ModeNote.ShouldBe("Plays the trained brain with one shadow. Nothing is learned or saved.");
        setup.Shadows.ShouldBe(new SettingSlider("Shadows", "1", null, setup.Shadows.Step, Disabled: true));
        setup.RunLength.ShouldBe(new SettingSlider("Run length", "Until you leave", null, setup.RunLength.Step, Disabled: true));
        setup.Settings.ShouldBe(TrainSettingsDef.Default);
    }

    [Fact]
    public void Simulate_SetsNoSettings()
    {
        var setup = new TrainSetupPresentationViewModel(Creation(generation: 3));
        setup.SetMode(TrainingRunMode.Simulate);

        Should.Throw<InvalidOperationException>(() => setup.SetShadows(1));
        Should.Throw<InvalidOperationException>(() => setup.SetRunLength(1));
    }

    [Fact]
    public void BackToTrain_KeepsTheSettings()
    {
        var setup = new TrainSetupPresentationViewModel(Creation(new TrainSettingsDef(12, 30), generation: 3));
        var before = setup.Shadows;

        setup.SetMode(TrainingRunMode.Simulate);
        setup.SetMode(TrainingRunMode.Train);

        setup.Shadows.ShouldBe(before);
        setup.RunLength.Readout.ShouldBe("30 s");
    }

    private static CreationDef Creation(TrainSettingsDef? settings = null, int? generation = null) =>
        new(
            Guid.NewGuid(),
            "Worm",
            new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0))], [new BeamDef(3, 1, 2)], []),
            generation is { } finished ? TestTraining.State(finished, 1, TestTraining.Run) : null,
            settings);
}
