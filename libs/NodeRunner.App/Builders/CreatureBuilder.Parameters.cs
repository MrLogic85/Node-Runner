using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Builders;

/// <summary>
/// The parts' panel settings: which settings each kind has, reading one and setting one. The part is
/// found by id (<see cref="FindPart"/>); each kind's own handler reads or changes its typed settings.
/// </summary>
public sealed partial class CreatureBuilder
{
    private static readonly PartParameterId[] _pistonParameters = [PartParameterId.Strength, PartParameterId.Stroke, PartParameterId.StartPosition, PartParameterId.MaxSpeed, PartParameterId.RiseTime];

    private static readonly PartParameterId[] _servoParameters = [PartParameterId.ServoStrength, PartParameterId.Range, PartParameterId.ServoStartPosition, PartParameterId.AngularMaxSpeed, PartParameterId.RiseTime];

    private static readonly PartParameterId[] _springParameters = [PartParameterId.Stiffness, PartParameterId.Damping, PartParameterId.Stroke, PartParameterId.CoilLength];

    private static readonly PartParameterId[] _wheelParameters = [PartParameterId.WheelRadius];

    private static readonly PartParameterId[] _cameraParameters = [PartParameterId.Aim, PartParameterId.Rays, PartParameterId.Spread, PartParameterId.CameraRange];

    /// <summary>The settings part <paramref name="partId"/> has, in panel order; a joint and a beam have none.</summary>
    public IReadOnlyList<PartParameterId> ParametersOf(int partId) => FindPart(partId) is { } part ? ParametersOfKind(part.PartKind) : [];

    private static IReadOnlyList<PartParameterId> ParametersOfKind(PartKind kind) => kind switch
    {
        PartKind.Piston => _pistonParameters,
        PartKind.Servo => _servoParameters,
        PartKind.VelocityMotor => _velocityMotorParameters,
        PartKind.Stepper => _stepperParameters,
        PartKind.Brake => _brakeParameters,
        PartKind.Spring => _springParameters,
        PartKind.Wheel => _wheelParameters,
        PartKind.Camera => _cameraParameters,
        PartKind.Node or PartKind.Beam or PartKind.Accelerometer or PartKind.TouchSensor or PartKind.Rubber or PartKind.AngleSensor => [],
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Every part kind says which settings it has."),
    };

    /// <summary>
    /// Whether the drawing is one Build lets a player make: every joint inside
    /// <see cref="BuildViewModel.BuildArea"/> and every panel setting inside its slider's range, a
    /// Locked Brake's top step included. A creature from outside the app, like a share code,
    /// is checked with this.
    /// </summary>
    public bool IsWithinBuildLimits() =>
        _nodes.All(node => BuildViewModel.BuildArea.Contains(node.Position))
        && PartIdsWithSettings.All(partId => ParametersOf(partId).All(parameter => IsOnItsSlider(parameter, ParameterValue(partId, parameter))));

    /// <summary>Every part with settings (<see cref="ParametersOf"/>), in <see cref="Parts"/> order.</summary>
    public IEnumerable<int> PartIdsWithSettings =>
        Parts.Where(part => ParametersOfKind(part.PartKind).Count > 0).Select(part => part.Id);

    /// <summary>Whether <paramref name="value"/>, in world units, is one <paramref name="parameter"/>'s slider can set; a setting without one can be any.</summary>
    public static bool IsOnItsSlider(PartParameterId parameter, double value) =>
        PartParameters.Of(parameter).Slider is not { } slider || slider.Allows(value);

    /// <summary>
    /// Whether <paramref name="parameter"/> does anything on part <paramref name="partId"/> as it is
    /// set now: a one-ray Camera has no Spread.
    /// </summary>
    public bool ParameterHasEffect(int partId, PartParameterId parameter) =>
        parameter != PartParameterId.Spread || Camera(partId).Rays > 1;

    /// <summary>Part <paramref name="partId"/>'s <paramref name="parameter"/>, in world units.</summary>
    public double ParameterValue(int partId, PartParameterId parameter) => FindPart(partId) switch
    {
        SensorDef sensor => CameraParameterValue(Camera(sensor.Id), parameter),
        SpringDef spring => parameter switch
        {
            PartParameterId.Stiffness => spring.Stiffness,
            PartParameterId.Damping => spring.Damping,
            PartParameterId.Stroke => spring.Stroke,
            PartParameterId.CoilLength => spring.CoilLength,
            _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
        },
        WheelDef wheel => parameter == PartParameterId.WheelRadius ? wheel.Radius : throw new ArgumentOutOfRangeException(nameof(parameter)),
        ServoDef servo => parameter switch
        {
            PartParameterId.ServoStrength => servo.Strength,
            PartParameterId.Range => servo.Range,
            PartParameterId.ServoStartPosition => servo.Start,
            PartParameterId.AngularMaxSpeed => servo.MaxSpeed,
            PartParameterId.RiseTime => servo.RiseTime,
            _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
        },
        VelocityMotorDef motor => VelocityMotorParameterValue(motor, parameter),
        StepperDef stepper => StepperParameterValue(stepper, parameter),
        BrakeDef brake => parameter == PartParameterId.BrakeStrength ? brake.Strength : throw new ArgumentOutOfRangeException(nameof(parameter)),
        PistonDef piston => parameter switch
        {
            PartParameterId.Strength => piston.Strength,
            PartParameterId.Stroke => piston.Stroke,
            PartParameterId.StartPosition => piston.Start,
            PartParameterId.MaxSpeed => piston.MaxSpeed,
            PartParameterId.RiseTime => piston.RiseTime,
            _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(partId), "Part id must point to an existing part with settings."),
    };

    /// <summary>Sets part <paramref name="partId"/>'s <paramref name="parameter"/>, in world units, keeping its other settings.</summary>
    public void SetParameter(int partId, PartParameterId parameter, double value) => Replace(FindPart(partId) switch
    {
        SensorDef sensor => CameraWithParameter(Camera(sensor.Id), parameter, value),
        SpringDef spring => parameter switch
        {
            PartParameterId.Stiffness => spring.WithSettings(value, spring.Damping, spring.Stroke, spring.CoilLength),
            PartParameterId.Damping => spring.WithSettings(spring.Stiffness, value, spring.Stroke, spring.CoilLength),
            PartParameterId.Stroke => spring.WithSettings(spring.Stiffness, spring.Damping, value, spring.CoilLength),
            PartParameterId.CoilLength => spring.WithSettings(spring.Stiffness, spring.Damping, spring.Stroke, value),
            _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
        },
        WheelDef wheel => parameter == PartParameterId.WheelRadius ? wheel.WithSettings(value) : throw new ArgumentOutOfRangeException(nameof(parameter)),
        ServoDef servo => parameter switch
        {
            PartParameterId.ServoStrength => servo.WithSettings(value, servo.Range, servo.Start, servo.MaxSpeed, servo.RiseTime),
            PartParameterId.Range => servo.WithSettings(servo.Strength, value, servo.Start, servo.MaxSpeed, servo.RiseTime),
            PartParameterId.ServoStartPosition => servo.WithSettings(servo.Strength, servo.Range, value, servo.MaxSpeed, servo.RiseTime),
            PartParameterId.AngularMaxSpeed => servo.WithSettings(servo.Strength, servo.Range, servo.Start, value, servo.RiseTime),
            PartParameterId.RiseTime => servo.WithSettings(servo.Strength, servo.Range, servo.Start, servo.MaxSpeed, value),
            _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
        },
        VelocityMotorDef motor => VelocityMotorWithParameter(motor, parameter, value),
        StepperDef stepper => StepperWithParameter(stepper, parameter, value),
        BrakeDef brake => parameter == PartParameterId.BrakeStrength ? brake.WithStrength(value) : throw new ArgumentOutOfRangeException(nameof(parameter)),
        PistonDef piston => parameter switch
        {
            PartParameterId.Strength => piston.WithSettings(value, piston.Stroke, piston.Start, piston.MaxSpeed, piston.RiseTime),
            PartParameterId.Stroke => piston.WithSettings(piston.Strength, value, piston.Start, piston.MaxSpeed, piston.RiseTime),
            PartParameterId.StartPosition => piston.WithSettings(piston.Strength, piston.Stroke, value, piston.MaxSpeed, piston.RiseTime),
            PartParameterId.MaxSpeed => piston.WithSettings(piston.Strength, piston.Stroke, piston.Start, value, piston.RiseTime),
            PartParameterId.RiseTime => piston.WithSettings(piston.Strength, piston.Stroke, piston.Start, piston.MaxSpeed, value),
            _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(partId), "Part id must point to an existing part with settings."),
    });

    private double CameraParameterValue(SensorDef camera, PartParameterId parameter) => parameter switch
    {
        PartParameterId.Aim => camera.Aim ?? DefaultAim(camera.BeamId),
        PartParameterId.Rays => camera.Rays!.Value,
        PartParameterId.Spread => camera.Spread!.Value,
        PartParameterId.CameraRange => camera.Range!.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
    };

    private static SensorDef CameraWithParameter(SensorDef camera, PartParameterId parameter, double value) => parameter switch
    {
        PartParameterId.Aim => camera.WithAim(value),
        PartParameterId.Rays => camera.WithRays(value == Math.Round(value) && value is >= int.MinValue and <= int.MaxValue
            ? (int)value
            : throw new ArgumentOutOfRangeException(nameof(value), "A Camera's rays are a whole number.")),
        PartParameterId.Spread => camera.WithSpread(value),
        PartParameterId.CameraRange => camera.WithRange(value),
        _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
    };

    private SensorDef Camera(int sensorId) => _sensors[SensorIndexOf(sensorId)] is { Kind: SensorKind.Camera } camera
        ? camera
        : throw new ArgumentOutOfRangeException(nameof(sensorId), "Only a Camera has settings.");

    private double DefaultAim(int beamId)
    {
        var beam = _beams[BeamIndexOf(beamId)];
        return SensorDef.DefaultAim(_nodes[NodeIndexOf(beam.NodeA)].Position, _nodes[NodeIndexOf(beam.NodeB)].Position);
    }
}
