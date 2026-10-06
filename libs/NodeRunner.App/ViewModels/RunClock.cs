namespace NodeRunner.App.ViewModels;

/// <summary>How far a shadow is into its run (#715), in physics ticks.</summary>
/// <param name="ElapsedTicks">The ticks the run has gone on so far.</param>
/// <param name="LengthTicks">The ticks the whole run lasts.</param>
public readonly record struct RunClock(int ElapsedTicks, int LengthTicks);
