using Godot;
using NodeRunner.App.Navigation;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Managers;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Hosts;

/// <summary>
/// The Build scene for one saved creation. Every edit saves itself (#368): once edits settle, and
/// whenever the player leaves Build or the app pauses. Training lives in its own scene,
/// <see cref="TrainingHost"/> (#469).
/// </summary>
public partial class BuildHost : Node, IRoutedScene
{
    private BuildScreen _buildScreen = null!;
    private UiDialog _deleteCreationDialog = null!;
    private bool _activeCreationDeleted;
    private ISceneNavigator? _navigator;
    private BuildRoute? _route;
    // The open creation: it holds its id and saves its edits. Null once the creation is deleted.
    private BuildAutosave? _autosave;
    private Godot.Timer _autosaveTimer = null!;
    private bool _saveFailureShown;

    // How long edits must settle before they save; a drag saves once, when it stops.
    private const double _autosaveDelaySeconds = 0.5;

    public BuildViewModel Build { get; } = new();

    private SaveManager Saves => GetNode<SaveManager>("/root/SaveManager");

    public void Enter(SceneRoute route, ISceneNavigator navigator)
    {
        _route = (BuildRoute)route;
        _navigator = navigator;
    }

    public override void _Ready()
    {
        BindBuildScreen();
        _deleteCreationDialog = GetNode<UiDialog>("%DeleteDialog");
        _deleteCreationDialog.Finished += OnDeleteCreationDialogFinished;
        AddAutosaveTimer();
        AddBackHandler();
        OpenRoute();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationPaused)
        {
            SaveEdits(playerAsked: false);
        }
        else if (what == NotificationWMCloseRequest)
        {
            LeaveCreation(playerAsked: false);
        }
    }

    // Build left for another scene saves, or removes an untouched new creation.
    public override void _ExitTree() => LeaveCreation(playerAsked: false);

    private void AddAutosaveTimer()
    {
        _autosaveTimer = new Godot.Timer { OneShot = true, WaitTime = _autosaveDelaySeconds };
        _autosaveTimer.Timeout += () => SaveEdits(playerAsked: false);
        AddChild(_autosaveTimer);
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

    // Opened by the router, Build shows the route's creation. Run on its own (F6) it makes a new one,
    // as + New does.
    private void OpenRoute()
    {
        if (_route is null)
        {
            EditCreation(Saves.CreateNew(), openedAsNew: true);
            return;
        }

        var id = _route.CreationId;
        if (Saves.Get(id) is { } creation)
        {
            EditCreation(creation, _route.IsNew);
            return;
        }

        GD.PrintErr($"Creation {id} was not found.");
        Notify("Creations", "That creation could not be found.");
        Callable.From(ShowCreations).CallDeferred();
    }

    private void BindBuildScreen()
    {
        _buildScreen = GetNode<BuildScreen>("%BuildScreen");
        _buildScreen.Setup(Build);
        _buildScreen.ToolRequested += tool => Build.ActiveTool = tool;
        _buildScreen.BrainShapeChanged += (layers, neurons) =>
        {
            if (!Build.IsMoveOnly)
            {
                Build.SetBrainShape(new BrainShapeDef(layers, neurons));
            }
        };
        _buildScreen.StartTrainingRequested += StartTraining;
        _buildScreen.BackRequested += BackFromBuildScreen;
        _buildScreen.CreationNameChanged += RenameActiveCreation;
        _buildScreen.ResetTrainingRequested += ResetActiveCreationTraining;
        _buildScreen.DeleteCreationRequested += RequestDeleteActiveCreation;
        _buildScreen.ClearSelectionRequested += Build.ClearSelection;
        _buildScreen.DeleteSelectionRequested += Build.DeleteSelectedParts;
        _buildScreen.StatsRequested += () => Notify("Stats", "Stats open in milestone 0.12.0.");
        _buildScreen.BrainRequested += () => Notify("Brain view", "Brain view opens in milestone 0.12.0.");
    }

    // Training opens in its own scene from the saved creation, so the edits save first; only a
    // creature that cannot train stays in Build.
    private void StartTraining()
    {
        if (_autosave?.CreationId is not { } id || !SaveEdits(playerAsked: true) || !Build.TryGetTrainableCreature(out _))
        {
            return;
        }

        // Training made it a real creation: Back from training must not treat it as an untouched New.
        if (_route?.IsNew == true)
        {
            _route = new BuildRoute(id);
            _navigator?.ReplaceCurrent(_route);
        }

        Notify("Train setup", "Train setup opens in milestone 0.12.0.");
        _navigator?.Navigate(new SceneNavigation(new TrainingRoute(id)));
    }

    private void ShowCreations() => _navigator?.ReturnToRoot();

    private void Notify(string title, string message, UiPopupType type = UiPopupType.Default) =>
        UiNotificationLayer.Enqueue(this, new UiNotificationSpec(type, title, message));

    private void EditCreation(CreationDef creation, bool openedAsNew)
    {
        StopAutosave();
        Build.LoadCreation(creation);
        Build.IsActive = true;
        _autosave = new BuildAutosave(Build, Saves.BuildEditWorkflow, creation.Id, openedAsNew);
        _autosave.Changed += OnBuildEdited;
    }

    private void OnBuildEdited(object? sender, EventArgs e) => _autosaveTimer.Start();

    private void StopAutosave()
    {
        _autosaveTimer.Stop();
        if (_autosave is not null)
        {
            _autosave.Changed -= OnBuildEdited;
            _autosave.Dispose();
            _autosave = null;
        }
    }

    // Back leaves even if the save fails, so a broken disk never traps the player in Build; the
    // failure notice outlives the scene.
    private void BackFromBuildScreen()
    {
        LeaveCreation(playerAsked: true);
        _navigator?.Back();
    }

    // Saves the drawing as it stands, finished or not: only training needs a creature that can be
    // simulated (#515). A locked creation is move-only and keeps its brain and training
    // (CreationLock). Returns false when edits are left unsaved; the next save tries again. A save
    // the player asked for reports every failure; background saves report only the first in a row.
    private bool SaveEdits(bool playerAsked)
    {
        _autosaveTimer.Stop();
        if (_autosave is not { HasUnsavedEdits: true } autosave)
        {
            return true;
        }

        var saved = false;
        CreationActions.TryRunFileOperation(
            () => saved = autosave.Save(),
            $"Saving edits to Creation {autosave.CreationId}");
        if (saved)
        {
            _saveFailureShown = false;
        }
        else if (playerAsked || !_saveFailureShown)
        {
            _saveFailureShown = true;
            Notify("Save failed", "Your latest edits could not be saved.", UiPopupType.Danger);
        }

        return saved;
    }

    // A + New creation left with no nodes is removed again, so it leaves no empty creation behind.
    private void LeaveCreation(bool playerAsked)
    {
        if (_autosave is null)
        {
            return;
        }

        if (_autosave.ShouldDiscardOnLeave)
        {
            var id = _autosave.CreationId;
            StopAutosave();
            CreationActions.TryRunFileOperation(() => Saves.Delete(id), $"Removing empty new Creation {id}");
            return;
        }

        SaveEdits(playerAsked);
    }

    private void RenameActiveCreation(string name)
    {
        if (_autosave?.CreationId is not { } id)
        {
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
            Build.SetCreationName(renamed.Name);
        }
    }

    private void ResetActiveCreationTraining()
    {
        if (_autosave?.CreationId is not { } id || !SaveEdits(playerAsked: true))
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
            EditCreation(creation, openedAsNew: false);
        }
    }

    private void RequestDeleteActiveCreation()
    {
        if (_autosave?.CreationId is not { } id || _deleteCreationDialog.IsOpen)
        {
            return;
        }

        var name = Build.CreationName;
        _deleteCreationDialog.Open(CreationActions.DeleteDialog(name, () => DeleteCreation(id, name)));
    }

    private bool DeleteCreation(Guid id, string name)
    {
        var succeeded = CreationActions.TryDelete(Saves, id, name);
        if (succeeded && _autosave?.CreationId == id)
        {
            StopAutosave();
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
