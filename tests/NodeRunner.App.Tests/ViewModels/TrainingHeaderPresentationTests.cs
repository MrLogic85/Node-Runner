using NodeRunner.App.Navigation;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class TrainingHeaderPresentationTests
{
    [Theory]
    [InlineData(TrainingRunMode.Train, "Training · {0}", true)]
    [InlineData(TrainingRunMode.Simulate, "Simulating · {0}", false)]
    public void For_SaysWhatRunsOnTheMap_AndShowsTheGenerationOnlyWhenTraining(TrainingRunMode mode, string status, bool showsGeneration)
    {
        var header = TrainingHeaderPresentation.For("Walker-1", mode, MapIds.Flat);

        header.ShouldBe(new TrainingHeaderPresentation(
            UiText.AsWritten("Walker-1"),
            UiText.Format(status, UiText.Plain("Flat ground")),
            showsGeneration));
    }

    [Fact]
    public void ForWalker_NamesTheWalkerExample_AndAnUnknownMapOrBlankNameThrows()
    {
        TrainingHeaderPresentation.ForWalker(TrainingRunMode.Train, MapIds.Flat).ShouldBe(new TrainingHeaderPresentation(
            CreationExamples.Walker.Name,
            UiText.Format("Training · {0}", UiText.Plain("Flat ground")),
            ShowsGeneration: true));
        Should.Throw<ArgumentOutOfRangeException>(() => TrainingHeaderPresentation.For("Walker-1", TrainingRunMode.Train, "moon"));
        Should.Throw<ArgumentException>(() => TrainingHeaderPresentation.For(" ", TrainingRunMode.Train, MapIds.Flat));
    }
}
