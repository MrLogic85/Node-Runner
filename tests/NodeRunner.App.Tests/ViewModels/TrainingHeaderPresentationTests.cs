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
    public void Status_SaysWhatRunsOnTheMap_OrThatItIsPaused_AndOnlyTrainingShowsTheGeneration(TrainingRunMode mode, string running, bool showsGeneration)
    {
        var header = TrainingHeaderPresentation.For("Walker-1", mode, MapIds.Flat);

        header.CreationName.ShouldBe(UiText.AsWritten("Walker-1"));
        header.Status(paused: false).ShouldBe(UiText.Format(running, UiText.Plain("Flat ground")));
        header.Status(paused: true).ShouldBe(UiText.Format("Paused · {0}", UiText.Plain("Flat ground")));
        header.ShowsGeneration.ShouldBe(showsGeneration);
    }

    [Fact]
    public void ForWalker_NamesTheWalkerExample_AndAnUnknownModeMapOrBlankNameThrows()
    {
        TrainingHeaderPresentation.ForWalker(TrainingRunMode.Train, MapIds.Flat).CreationName.ShouldBe(CreationExamples.Walker.Name);
        Should.Throw<ArgumentOutOfRangeException>(() => TrainingHeaderPresentation.For("Walker-1", (TrainingRunMode)99, MapIds.Flat));
        Should.Throw<ArgumentOutOfRangeException>(() => TrainingHeaderPresentation.For("Walker-1", TrainingRunMode.Train, "moon"));
        Should.Throw<ArgumentException>(() => TrainingHeaderPresentation.For(" ", TrainingRunMode.Train, MapIds.Flat));
    }
}
