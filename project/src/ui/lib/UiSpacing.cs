namespace NodeRunner.Ui.Lib;

/// <summary>
/// Semantic spacing roles for the 640 x 360 reference canvas. Use these names
/// instead of one-off pixel values or a raw <see cref="UiSize.Space"/> step, so the
/// intent of a gap stays visible at the call site.
/// </summary>
public static class UiSpacing
{
    public const int ControlGap = UiSize.Space.S2;
    public const int SegmentedControlHorizontalPadding = UiSize.Space.S3;
}
