namespace NodeRunner.App.Builders;

/// <summary>A part setting (#704). Parts with the same id have the same setting, whatever their kind.</summary>
public enum PartParameterId
{
    Strength,
    ServoStrength,
    Stroke,
    Range,
    StartPosition,
    ServoStartPosition,
    MaxSpeed,
    AngularMaxSpeed,
    RiseTime,
    Aim,
    Stiffness,
    Damping,
    CoilLength,
    WheelRadius,
    Grip,
}
