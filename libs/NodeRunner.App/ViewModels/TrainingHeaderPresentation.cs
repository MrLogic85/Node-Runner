using NodeRunner.App.Navigation;
using NodeRunner.App.Services;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The Training screen's top bar: the creation's name and what the run does where, such as
/// "Training · Flat ground". Only a run that learns has a generation caption.
/// </summary>
public sealed record TrainingHeaderPresentation(UiText CreationName, UiText StatusText, bool ShowsGeneration)
{
    public static TrainingHeaderPresentation For(string creationName, TrainingRunMode mode, string mapId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(creationName);
        return For(UiText.AsWritten(creationName), mode, mapId);
    }

    /// <summary>The bar for the Walker example, which Training runs when it opens without a creation.</summary>
    public static TrainingHeaderPresentation ForWalker(TrainingRunMode mode, string mapId) =>
        For(CreationExamples.Walker.Name, mode, mapId);

    private static TrainingHeaderPresentation For(UiText name, TrainingRunMode mode, string mapId)
    {
        var map = MapNames.Of(mapId);
        var status = mode switch
        {
            TrainingRunMode.Train => UiText.Format("Training · {0}", map),
            TrainingRunMode.Simulate => UiText.Format("Simulating · {0}", map),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
        return new TrainingHeaderPresentation(name, status, mode == TrainingRunMode.Train);
    }
}
