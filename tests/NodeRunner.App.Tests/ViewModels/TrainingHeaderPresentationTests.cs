using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class TrainingHeaderPresentationTests
{
    [Fact]
    public void For_Training_SaysTrainingOnTheMapAndShowsTheGeneration()
    {
        var header = TrainingHeaderPresentation.For("Walker-1", TrainingRunMode.Train, MapIds.Flat);

        header.ShouldBe(new TrainingHeaderPresentation("Walker-1", "Training · Flat ground", ShowsGeneration: true));
    }

    [Fact]
    public void For_Simulating_SaysSimulatingAndHidesTheGeneration()
    {
        var header = TrainingHeaderPresentation.For("Walker-1", TrainingRunMode.Simulate, MapIds.Flat);

        header.StatusText.ShouldBe("Simulating · Flat ground");
        header.ShowsGeneration.ShouldBeFalse();
    }

    [Fact]
    public void For_AnUnknownMap_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => TrainingHeaderPresentation.For("Walker-1", TrainingRunMode.Train, "moon"));

    [Fact]
    public void For_ABlankName_Throws() =>
        Should.Throw<ArgumentException>(() => TrainingHeaderPresentation.For(" ", TrainingRunMode.Train, MapIds.Flat));
}
