namespace NodeRunner.Ui.Lib;

/// <summary>Shared surface vocabulary for the reference component library.</summary>
public static class UiSurfaceContracts
{
    public enum FrameVariant
    {
        Frame,
        Sel,
        Pick,
        Lock,
        Warn,
        Hint,
        Ok,
        Raised,
        StageCard,
    }

    public enum FrameSize
    {
        Default,
        Snug,
        Tight,
        Roomy,
        Flush,
    }

    /// <summary>
    /// Generic raised-surface state. It deliberately has no control-specific
    /// terms so buttons, fields, rows, tools and chips can share the contract.
    /// </summary>
    public enum RaisedState
    {
        Rest,
        On,
        Lock,
        Off,
        Primary,
        Danger,
    }

    public static IReadOnlyList<FrameVariant> AllFrameVariants { get; } =
        Enum.GetValues<FrameVariant>();

    public static IReadOnlyList<FrameSize> AllFrameSizes { get; } =
        Enum.GetValues<FrameSize>();

    public static IReadOnlyList<RaisedState> AllRaisedStates { get; } =
        Enum.GetValues<RaisedState>();

    public static bool IsDashed(FrameVariant variant) => variant == FrameVariant.Lock;

    public static bool IsDimmed(FrameVariant variant) => variant == FrameVariant.Lock;

    public static bool HasGlow(FrameVariant variant) =>
        variant is FrameVariant.Sel or FrameVariant.StageCard;
}
