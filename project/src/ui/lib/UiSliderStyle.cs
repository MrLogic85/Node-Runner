namespace NodeRunner.Ui.Lib;

public readonly record struct UiSliderStyle(
    float ThumbRadius,
    float TrackWidth,
    float MarkerHalfHeight,
    float StepTickHalfHeight,
    float DisabledDashLength,
    float DisabledThumbInset,
    float SteppedHeight,
    float CompactSteppedHeight)
{
    public const float DisabledOpacity = 0.5f;
    public const int DisabledThumbSegments = 8;
    public const int DisabledThumbArcPoints = 4;

    public static UiSliderStyle From(UiTokens tokens) =>
        new(
            tokens.SliderThumbDiameter * 0.5f,
            tokens.SliderTrackWidth,
            tokens.SliderMarkerHeight * 0.5f,
            tokens.SliderStepTickHeight * 0.5f,
            tokens.SliderDisabledDashLength,
            tokens.StrokeHair,
            tokens.SliderSteppedHeight,
            tokens.SliderCompactSteppedHeight);
}
