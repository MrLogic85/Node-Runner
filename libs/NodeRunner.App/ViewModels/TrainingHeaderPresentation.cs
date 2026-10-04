using NodeRunner.App.Navigation;
using NodeRunner.App.Services;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The Training screen's top bar: the creation's name and what the run does where, such as
/// "Training · Flat ground". Only a run that learns has a generation caption.
/// </summary>
public sealed record TrainingHeaderPresentation(UiText CreationName, UiText StatusText, bool ShowsGeneration)
{
    /// <summary>
    /// The bar for the creation named <paramref name="creationName"/>, or for the Worm example that
    /// Training falls back to when it opens without a creation (null).
    /// </summary>
    public static TrainingHeaderPresentation For(string? creationName, TrainingRunMode mode, string mapId)
    {
        if (creationName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(creationName);
        }

        var map = MapNames.Of(mapId);
        var status = mode switch
        {
            TrainingRunMode.Train => UiText.Format("Training · {0}", map),
            TrainingRunMode.Simulate => UiText.Format("Simulating · {0}", map),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
        var name = creationName is null ? CreationExamples.Worm.Name : UiText.AsWritten(creationName);
        return new TrainingHeaderPresentation(name, status, mode == TrainingRunMode.Train);
    }
}
