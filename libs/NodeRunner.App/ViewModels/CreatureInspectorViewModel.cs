using System.ComponentModel;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

public sealed class CreatureInspectorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly CreatureDef _creature;
    private readonly SelectionViewModel _selection;

    public CreatureInspectorViewModel(CreatureDef creature, SelectionViewModel selection)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(selection);

        _creature = creature;
        _selection = selection;
        _selection.PropertyChanged += OnSelectionPropertyChanged;
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; private set; } = string.Empty;

    public string Role { get; private set; } = string.Empty;

    public string Values { get; private set; } = string.Empty;

    public void Dispose()
    {
        _selection.PropertyChanged -= OnSelectionPropertyChanged;
    }

    private void OnSelectionPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(SelectionViewModel.SelectedElement))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var selection = _selection.SelectedElement;
        if (selection is null)
        {
            SetContent(
                "Creature inspector",
                "Tap a joint, bone, or muscle to inspect it.",
                "The creature is built from joints, bones, and muscles.");
            return;
        }

        switch (selection.Kind)
        {
            case CreatureElementKind.Joint:
                var joint = _creature.Joints[selection.Index];
                SetContent(
                    $"Joint {selection.Index + 1}",
                    "A moving physical node that other parts attach to.",
                    $"Position: ({joint.Position.X:0.#}, {joint.Position.Y:0.#})\nRadius: {joint.Radius:0.#}");
                break;
            case CreatureElementKind.Bone:
                var bone = _creature.Bones[selection.Index];
                SetContent(
                    $"Bone {selection.Index + 1}",
                    "A passive structural connection that helps the creature keep its shape.",
                    $"Connects: Joint {bone.JointA + 1} to Joint {bone.JointB + 1}\nBrain output: none");
                break;
            case CreatureElementKind.Muscle:
                var muscle = _creature.Muscles[selection.Index];
                SetContent(
                    $"Muscle {selection.Index + 1}",
                    "An active actuator. The brain changes it to move the body.",
                    $"Connects: Joint {muscle.JointA + 1} to Joint {muscle.JointB + 1}\nBrain output: {selection.Index + 1}\nRest length: {muscle.RestLength:0.#}\nMax force: {muscle.MaxForce:0.#}");
                break;
        }
    }

    private void SetContent(string title, string role, string values)
    {
        Title = title;
        Role = role;
        Values = values;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
