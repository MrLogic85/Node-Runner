using System.Diagnostics.CodeAnalysis;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Builders;

/// <summary>
/// The Steppers (#1036): joint motors that share a joint's hinge-part slot with a Servo, a Velocity motor,
/// a Brake and an Angle sensor and take their sides by the Servo's rules (#1118). Their Max strength is
/// the Servo's setting, so it edits together with a selected Servo's (#1038).
/// </summary>
public sealed partial class CreatureBuilder
{
    /// <summary>Why a Stepper dropped on anything but a joint is refused (#1038).</summary>
    public static UiText SteppersGoOnAJointReason { get; } = UiText.Plain("Steppers go on a joint");

    /// <summary>Why a Stepper cannot work yet: it needs two sides at its joint (#1118).</summary>
    public static UiText StepperNeedsTwoSidesReason { get; } = UiText.Plain("A Stepper needs two links, or a link and a Wheel");

    private static readonly PartParameterId[] _stepperParameters = [PartParameterId.ServoStrength, PartParameterId.StepsPerTurn, PartParameterId.MaxFrequency];

    private readonly List<StepperDef> _steppers = [];

    public IReadOnlyList<StepperDef> Steppers => _steppers;

    /// <summary>Whether a Stepper can sit on this node; if not, <paramref name="reason"/> says why.</summary>
    public bool CanAddStepper(int nodeId, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeId))
        {
            reason = SteppersGoOnAJointReason;
            return false;
        }

        if (HasHingePart(nodeId))
        {
            reason = OneHingePartPerJointReason;
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>Adds a Stepper with its sides picked as a new Servo's are (<see cref="JointMotor.DefaultSides"/>).</summary>
    public int AddStepper(int nodeId)
    {
        var (fixedSide, targetSide) = HasNode(nodeId) ? JointMotor.DefaultSides(LinksAt(nodeId), WheelAt(nodeId)) : (null, null);
        return AddStepper(nodeId, fixedSide, targetSide);
    }

    /// <summary>Adds a Stepper with the given Fixed and Target sides, each at the joint or empty.</summary>
    public int AddStepper(int nodeId, int? fixedLinkId, int? targetLinkId)
    {
        if (!CanAddStepper(nodeId, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        foreach (var linkId in new[] { fixedLinkId, targetLinkId }.OfType<int>())
        {
            ValidateServoSide(linkId, nodeId);
        }

        var id = AllocatePartId();
        _steppers.Add(new StepperDef(id, nodeId, fixedLinkId, targetLinkId));
        return id;
    }

    /// <summary>
    /// Changes the Stepper's fixed or target side, as <see cref="SetServoLink"/> does a Servo's:
    /// picking the other role swaps them, and it keeps its id.
    /// </summary>
    public void SetStepperLink(int stepperId, bool fixedRole, int linkId) =>
        SetSide(_steppers, StepperIndexOf(stepperId), fixedRole, linkId, (stepper, fixedLink, targetLink) => stepper.WithLinks(fixedLink, targetLink));

    /// <summary>Removes a Stepper by id.</summary>
    public void RemoveStepper(int stepperId) => _steppers.RemoveAt(StepperIndexOf(stepperId));

    public int StepperIndexOf(int stepperId)
    {
        var index = _steppers.FindIndex(stepper => stepper.Id == stepperId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stepperId), "Stepper id must point to an existing stepper.");
        }

        return index;
    }

    private double StepperParameterValue(int stepperId, PartParameterId parameter)
    {
        var stepper = _steppers[StepperIndexOf(stepperId)];
        return parameter switch
        {
            PartParameterId.ServoStrength => stepper.Strength,
            PartParameterId.StepsPerTurn => stepper.StepsPerTurn,
            PartParameterId.MaxFrequency => stepper.MaxFrequency,
            _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
        };
    }

    private void SetStepperParameter(int stepperId, PartParameterId parameter, double value)
    {
        var index = StepperIndexOf(stepperId);
        var stepper = _steppers[index];
        _steppers[index] = parameter switch
        {
            PartParameterId.ServoStrength => stepper.WithSettings(value, stepper.StepsPerTurn, stepper.MaxFrequency),
            PartParameterId.StepsPerTurn => stepper.WithSettings(stepper.Strength, (int)Math.Round(value), stepper.MaxFrequency),
            PartParameterId.MaxFrequency => stepper.WithSettings(stepper.Strength, stepper.StepsPerTurn, value),
            _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
        };
    }

    private bool RenameStepper(int partId, string? name)
    {
        var index = _steppers.FindIndex(stepper => stepper.Id == partId);
        if (index < 0)
        {
            return false;
        }

        _steppers[index] = _steppers[index].WithName(name);
        return true;
    }
}
