using Godot;
using NodeRunner.App.Navigation;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Managers;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Hosts;

/// <summary>
/// The Build scene for one creation, or a new draft. Training lives in its own scene,
/// <see cref="TrainingHost"/> (#469).
/// </summary>
public partial class BuildHost : Node, IRoutedScene
{
    private BuildScreen _buildScreen = null!;
    private UiDialog _deleteCreationDialog = null!;
    private bool _activeCreationDeleted;
    private ISceneNavigator? _navigator;
    private BuildRoute? _route;
    private Guid? _activeCreationId;

    public ConstructionViewModel Construction { get; } = new();

    private SaveManager Saves => GetNode<SaveManager>("/root/SaveManager");

    public void Enter(SceneRoute route, ISceneNavigator navigator)
    {
        _route = (BuildRoute)route;
        _navigator = navigator;
    }

    public override void _Ready()
    {
        ApplyProgression();
        BindBuildScreen();
        _deleteCreationDialog = GetNode<UiDialog>("%DeleteDialog");
        _deleteCreationDialog.Finished += OnDeleteCreationDialogFinished;
        AddBackHandler();
        OpenRoute();
    }

    // Android Back and Escape: an open dialog, sheet or menu closes first, then Build goes one step
    // back.
    private void AddBackHandler()
    {
        if (_navigator is null)
        {
            return;
        }

        var back = new UiBackHandler { CanTakeBack = () => !_deleteCreationDialog.IsOpen && _buildScreen.CanTakeBack };
        back.BackRequested += OnBackRequested;
        AddChild(back, @internal: InternalMode.Front);
    }

    private void OnBackRequested()
    {
        if (!_buildScreen.CloseOverlay())
        {
            BackFromBuildScreen();
        }
    }

    // Opened by the router, Build shows the route's creation, or a new draft. Run on its own (F6) it
    // opens a new draft.
    private void OpenRoute()
    {
        if (_route?.CreationId is not { } id)
        {
            StartNewCreation();
            return;
        }

        if (Saves.Get(id) is { } creation)
        {
            EditCreation(creation);
            return;
        }

        GD.PrintErr($"Creation {id} was not found.");
        Notify("Creations", "That creation could not be found.");
        Callable.From(ShowCreations).CallDeferred();
    }

    private void ApplyProgression() =>
        Construction.SetMaxCores(Saves.Progression.ExtraCoreUnlocked ? 2 : 1);

    private void BindBuildScreen()
    {
        _buildScreen = GetNode<BuildScreen>("%BuildScreen");
        _buildScreen.Setup(Construction);
        _buildScreen.ToolRequested += tool => Construction.ActiveTool = tool;
        _buildScreen.BrainShapeChanged += (layers, neurons) =>
        {
            if (!Construction.IsMoveOnly)
            {
                Construction.SetBrainShape(new BrainShapeDef(layers, neurons));
            }
        };
        _buildScreen.StartTrainingRequested += StartTraining;
        _buildScreen.BackRequested += BackFromBuildScreen;
        _buildScreen.CreationNameChanged += RenameActiveCreation;
        _buildScreen.ResetTrainingRequested += ResetActiveCreationTraining;
        _buildScreen.DeleteCreationRequested += RequestDeleteActiveCreation;
        _buildScreen.ClearSelectionRequested += Construction.ClearSelection;
        _buildScreen.DeleteSelectionRequested += Construction.DeleteSelectedParts;
        _buildScreen.StatsRequested += () => Notify("Stats", "Stats open in milestone 0.12.0.");
        _buildScreen.BrainRequested += () => Notify("Brain view", "Brain view opens in milestone 0.12.0.");
    }

    // Start training saves a new draft first; a saved creation opens its training straight away.
    private void StartTraining()
    {
        if (_activeCreationId is null)
        {
            SaveCreationFromBuild();
        }
        else
        {
            ResumeTrainingFromSavedCreation();
        }
    }

    private void ShowCreations() => _navigator?.ReturnToRoot();

    private void Notify(string title, string message, UiPopupType type = UiPopupType.Default) =>
        UiNotificationLayer.Enqueue(this, new UiNotificationSpec(type, title, message));

    private void StartNewCreation()
    {
        _activeCreationId = null;
        Construction.ResetDraft();
        Construction.IsActive = true;
    }

    private void EditCreation(CreationDef creation)
    {
        _activeCreationId = creation.Id;
        Construction.Load(creation.Creature, moveOnly: true, brainShape: creation.BrainShape, creationName: creation.Name, training: creation.Training);
        Construction.IsActive = true;
    }

    // Training opens in its own scene; Back from there rebuilds Build from the saved creation.
    private void ResumeTrainingFromSavedCreation()
    {
        if (_activeCreationId is not { } id || !PersistMoveOnlyEdits())
        {
            return;
        }

        Notify("Train setup", "Train setup opens in milestone 0.12.0.");
        _navigator?.Navigate(new SceneNavigation(new TrainingRoute(id)));
    }

    private void BackFromBuildScreen()
    {
        // Back from an unsaved draft (New) is one step back and drops the draft (#474).
        if (Construction.IsMoveOnly && !PersistMoveOnlyEdits())
        {
            return;
        }

        _navigator?.Back();
    }

    // Saves a saved creation's moved nodes before Build closes. An edit that cannot be saved is
    // discarded (#114): the saved creation stays as it was.
    private bool PersistMoveOnlyEdits()
    {
        if (!Construction.TryLeave(out var editedCreature, out var errors))
        {
            Construction.SetBlockedLeaveMessage(errors);
            return false;
        }

        if (editedCreature is null || _activeCreationId is not { } id)
        {
            return true;
        }

        ConstructionEditResult? editResult = null;
        var succeeded = CreationActions.TryRunFileOperation(
            () => editResult = Saves.PersistMoveOnlyEdit(id, editedCreature),
            $"Applying creature edit for Creation {id}");
        if (succeeded && editResult is not null)
        {
            Construction.SetCompletedMessage(editResult.StatusMessage);
            return true;
        }

        Construction.SetCompletedMessage("Could not save the edited creature; your edit was discarded.");
        Notify("Save failed", "The moved parts could not be saved.", UiPopupType.Danger);
        return true;
    }

    // Saving a new draft opens its training, like Start training in the reference. The history
    // entry becomes the saved creation first, so Back from training rebuilds it, not a blank draft.
    private void SaveCreationFromBuild()
    {
        if (!TryCompleteCreation(out var creation) || creation is null)
        {
            return;
        }

        if (_navigator is null)
        {
            EditCreation(creation);
            return;
        }

        _navigator.ReplaceCurrent(new BuildRoute(creation.Id));
        _navigator.Navigate(new SceneNavigation(new TrainingRoute(creation.Id)));
    }

    private bool TryCompleteCreation(out CreationDef? creation)
    {
        creation = null;
        if (!Construction.TryLeave(out var creature, out var errors) || creature is null)
        {
            Construction.SetBlockedLeaveMessage(errors);
            return false;
        }

        var saves = Saves;
        var completedCreation = saves.ConstructionDraftWorkflow.CompleteDraft(
            creature,
            Construction.SaveName(saves.List().Count),
            Construction.HasCustomBrainShape ? Construction.BrainShape : RecommendedBrainShape(creature));
        if (!CreationActions.TryRunFileOperation(
            () => saves.Save(completedCreation),
            $"Saving Creation '{completedCreation.Name}'"))
        {
            Construction.SetCompletedMessage("Save failed — see log.");
            Notify("Save failed", "The creation could not be saved.", UiPopupType.Danger);
            return false;
        }

        creation = completedCreation;
        _activeCreationId = creation.Id;
        if (saves.TryAttributeExtraCoreUnlock(creation.Id))
        {
            ApplyProgression();
        }

        Construction.SetCompletedMessage($"Saved {creation.Name}.");
        return true;
    }

    private static BrainShapeDef RecommendedBrainShape(CreatureDef creature)
    {
        var motorRelationCount = MotorTopology.BuildNodeConnections(creature)
            .Count(connection => connection.IsMotorized);
        var inputCount = (creature.Cores.Count * 6) + (motorRelationCount * 2);
        var outputCount = motorRelationCount;
        return new BrainShapeDef(
            BrainShapeDef.DefaultHiddenLayers,
            Math.Clamp(
                (int)Math.Ceiling((inputCount + outputCount) / 2.0),
                BrainShapeDef.MinimumNeuronsPerLayer,
                BrainShapeDef.MaximumNeuronsPerLayer));
    }

    private void RenameActiveCreation(string name)
    {
        if (_activeCreationId is not { } id)
        {
            Construction.SetCreationName(name);
            return;
        }

        CreationDef? renamed = null;
        if (!CreationActions.TryRunFileOperation(
            () => renamed = Saves.UpdateIfPresent(
                id,
                source => new CreationDef(source.Id, name, source.Creature, source.BrainShape, source.Training)),
            $"Renaming Creation '{id}'"))
        {
            return;
        }

        if (renamed is not null)
        {
            Construction.SetCreationName(renamed.Name);
        }
    }

    private void ResetActiveCreationTraining()
    {
        if (_activeCreationId is not { } id)
        {
            return;
        }

        if (!CreationActions.TryRunFileOperation(
            () => Saves.ResetTraining(id),
            $"Resetting training for Creation {id}"))
        {
            return;
        }

        if (Saves.Get(id) is { } creation)
        {
            EditCreation(creation);
        }
    }

    private void RequestDeleteActiveCreation()
    {
        if (_activeCreationId is not { } id || _deleteCreationDialog.IsOpen)
        {
            return;
        }

        var name = Construction.CreationName;
        _deleteCreationDialog.Open(CreationActions.DeleteDialog(name, () => DeleteCreation(id, name)));
    }

    private bool DeleteCreation(Guid id, string name)
    {
        var succeeded = CreationActions.TryDelete(Saves, id, name);
        if (succeeded && _activeCreationId == id)
        {
            _activeCreationId = null;
            _activeCreationDeleted = true;
        }

        return succeeded;
    }

    // Leaves once the dialog has closed, so it is not freed mid-action.
    private void OnDeleteCreationDialogFinished(bool confirmed)
    {
        if (_activeCreationDeleted)
        {
            _activeCreationDeleted = false;
            ShowCreations();
        }
    }
}
