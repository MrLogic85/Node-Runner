namespace NodeRunner.App.ViewModels;

/// <summary>
/// One shadow as the shadow strip (#387) shows it: its 1-based number, its distance so far this
/// trial (NaN when not running), and whether it is followed, the leader or the previous best.
/// </summary>
public sealed record ShadowStanding(int Number, double Distance, bool IsFollowed, bool IsLeader, bool IsPreviousBest);
