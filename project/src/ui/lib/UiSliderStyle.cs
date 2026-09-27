namespace NodeRunner.Ui.Lib;

public readonly record struct UiSliderStyle(
    float ThumbRadius,
    float TrackWidth,
    float MarkerHalfHeight,
    float StepTickHalfHeight,
    float DisabledDashLength,
    float DisabledThumbInset,
    float SteppedHeight)
{
    public const float DisabledOpacity = 0.5f;
    public const int DisabledThumbSegments = 8;
    public const int DisabledThumbArcPoints = 4;

    public static UiSliderStyle Default { get; } =
        new(
            UiSize.Widget.SliderThumbDiameter * 0.5f,
            UiSize.Widget.SliderTrackWidth,
            UiSize.Widget.SliderMarkerHeight * 0.5f,
            UiSize.Widget.SliderStepTickHeight * 0.5f,
            UiSize.Widget.SliderDisabledDashLength,
            UiSize.Stroke.Hair,
            UiSize.Widget.SliderSteppedHeight);
}
