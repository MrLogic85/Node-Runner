namespace NodeRunner.App.ViewModels;

/// <summary>What Build's readiness chip says (#1163): nothing drawn yet, blocked, or ready to train.</summary>
public enum BuildReadiness
{
    Empty,
    Blocked,
    Ready,
}

public sealed record BuildReadinessPresentation(BuildReadiness State, UiText Text)
{
    public bool CanStartTraining => State == BuildReadiness.Ready;
}
