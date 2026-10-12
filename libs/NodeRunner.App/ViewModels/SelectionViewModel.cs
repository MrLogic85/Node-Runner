using System.ComponentModel;
using System.Runtime.CompilerServices;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>The parts of the followed creature the player has tapped in Training's world view.</summary>
public sealed class SelectionViewModel : INotifyPropertyChanged
{
    private IReadOnlyList<CreatureElementSelection> _selectedParts = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The selected parts, a stacked joint's top → down; empty with nothing selected.</summary>
    public IReadOnlyList<CreatureElementSelection> SelectedParts
    {
        get => _selectedParts;
        private set
        {
            if (_selectedParts.SequenceEqual(value))
            {
                return;
            }

            _selectedParts = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Selects the part <paramref name="tapped"/> on <paramref name="creature"/>. A tap on a joint
    /// part, or on its joint, selects every part stacked on that joint, top → down
    /// (<see cref="CreatureDef.JointParts"/>, #1066), as Build does (#973); any other part alone.
    /// </summary>
    public void Select(CreatureDef creature, CreatureElementSelection tapped)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(tapped);
        var nodeId = tapped.Kind switch
        {
            CreatureElementKind.Node => tapped.Id,
            CreatureElementKind.Servo => creature.Servos.FirstOrDefault(servo => servo.Id == tapped.Id)?.NodeId,
            CreatureElementKind.Wheel => creature.Wheels.FirstOrDefault(wheel => wheel.Id == tapped.Id)?.NodeId,
            CreatureElementKind.TouchSensor => creature.TouchSensors.FirstOrDefault(touch => touch.Id == tapped.Id)?.NodeId,
            CreatureElementKind.Rubber => creature.Rubbers.FirstOrDefault(rubber => rubber.Id == tapped.Id)?.NodeId,
            CreatureElementKind.VelocityMotor => creature.VelocityMotors.FirstOrDefault(motor => motor.Id == tapped.Id)?.NodeId,
            CreatureElementKind.Brake => creature.Brakes.FirstOrDefault(brake => brake.Id == tapped.Id)?.NodeId,
            _ => null,
        };
        var stacked = nodeId is { } node ? CreatureDef.JointParts(node, creature.MotorsAndBrakes, creature.Wheels, creature.TouchSensors, creature.Rubbers) : [];
        SelectedParts = stacked.Count > 0 ? stacked : [tapped];
    }

    public void Clear()
    {
        SelectedParts = [];
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
