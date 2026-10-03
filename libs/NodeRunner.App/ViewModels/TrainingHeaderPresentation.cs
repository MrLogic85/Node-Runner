using NodeRunner.App.Navigation;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The Training screen's top bar: the creation's name and what the run does where, such as
/// "Training · Flat ground". Only a run that learns has a generation caption.
/// </summary>
public sealed record TrainingHeaderPresentation(string CreationName, string StatusText, bool ShowsGeneration)
{
    public static TrainingHeaderPresentation For(string creationName, TrainingRunMode mode, string mapId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(creationName);
        var activity = mode switch
        {
            TrainingRunMode.Train => "Training",
            TrainingRunMode.Simulate => "Simulating",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
        return new TrainingHeaderPresentation(creationName, $"{activity} · {MapName(mapId)}", mode == TrainingRunMode.Train);
    }

    public static string MapName(string mapId) => Maps.Get(mapId).Name;
}
