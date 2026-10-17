namespace NodeRunner.App.ViewModels;

/// <summary>How far a shadow is into its run, in physics ticks.</summary>
public readonly record struct RunClock(int ElapsedTicks, int LengthTicks);
