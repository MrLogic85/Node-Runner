using NodeRunner.App.Navigation;
using NodeRunner.App.Services;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The Training screen's top bar: the creation's name and what the run does where, such as
/// "Training · Flat ground". Only a run that learns has a generation caption.
/// </summary>
public sealed record TrainingHeaderPresentation(UiText CreationName, TrainingRunMode Mode, UiText Map)
{
    public bool ShowsGeneration => Mode == TrainingRunMode.Train;

    public static TrainingHeaderPresentation For(string creationName, TrainingRunMode mode, string mapId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(creationName);
        return For(UiText.AsWritten(creationName), mode, mapId);
    }

    /// <summary>The bar for the Walker example, which Training runs when it opens without a creation.</summary>
    public static TrainingHeaderPresentation ForWalker(TrainingRunMode mode, string mapId) =>
        For(CreationExamples.Walker.Name, mode, mapId);

    /// <summary>The subtitle: "Paused · Flat ground" while the run is paused, in either mode.</summary>
    public UiText Status(bool paused) => paused
        ? UiText.Format("Paused · {0}", Map)
        : Mode switch
        {
            TrainingRunMode.Train => UiText.Format("Training · {0}", Map),
            TrainingRunMode.Simulate => UiText.Format("Simulating · {0}", Map),
            _ => throw new InvalidOperationException($"No status for {Mode}."),
        };

    private static TrainingHeaderPresentation For(UiText name, TrainingRunMode mode, string mapId)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }

        return new TrainingHeaderPresentation(name, mode, MapNames.Of(mapId));
    }
}
