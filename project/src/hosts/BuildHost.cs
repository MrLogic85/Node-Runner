using Godot;
using NodeRunner.App.Builders;
using NodeRunner.App.Navigation;
using NodeRunner.App.Repositories;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Managers;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Hosts;

/// <summary>
/// The Build scene for one saved creation. Every edit saves itself (#368): once edits settle, and
/// whenever the player leaves Build or the app pauses. Training lives in its own scene,
/// <see cref="TrainingHost"/> (#469).
/// </summary>
public partial class BuildHost : Node, IRoutedScene
{
    private BuildScreen _buildScreen = null!;
    private UiDialog _dialog = null!;
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
        _dialog = GetNode<UiDialog>("%Dialog");
        _dialog.Finished += OnDialogFinished;
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

        var back = new UiBackHandler { CanTakeBack = () => !_dialog.IsOpen && _buildScreen.CanTakeBack };
        back.BackRequested += BackFromBuildScreen;
        AddChild(back, @internal: InternalMode.Front);
    }

    // Opened by the router, Build shows the route's creation. Run on its own (F6) it makes a new one,
    // as + New does.
    private void OpenRoute()
    {
        if (_route is null)
        {
            EditCreation(Saves.CreateNew(UiTextTranslation.Now), openedAsNew: true);
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
        _buildScreen.ToolRequested += Build.PickTool;
        _buildScreen.LinkPicked += link => Build.PickLink((BuildLink)link);
        _buildScreen.PartPicked += part => Build.PickPart((BuildPart)part);
        _buildScreen.PartPickHidden += () => Build.ClearPickedPart();
        _buildScreen.ParameterChanged += (parameter, value) =>
        {
            Build.BeginEdit(_buildScreen);
            Build.SetParameter((PartParameterId)parameter, value);
        };
        _buildScreen.ParameterChangeFinished += () => Build.EndEdit(_buildScreen);
        _buildScreen.ServoLinkChanged += (servoId, fixedRole, linkId) => Build.SetServoLink(servoId, fixedRole, linkId);
        _buildScreen.JointPartChosen += (kind, partId) => Build.ReplaceSelection(PartSet.Of(new CreatureElementSelection((CreatureElementKind)kind, partId)));
        _buildScreen.AdvancedSettingsToggled += open => Build.AdvancedSettingsOpen = open;
        _buildScreen.UndoRequested += Build.Undo;
        _buildScreen.RedoRequested += Build.Redo;
        _buildScreen.StartTrainingRequested += StartTraining;
        _buildScreen.BackRequested += BackFromBuildScreen;
        _buildScreen.CreationNameChanged += RenameActiveCreation;
        _buildScreen.UnlockRequested += RequestUnlock;
        _buildScreen.ResetTrainingRequested += RequestResetTraining;
        _buildScreen.CopyCreationRequested += CopyActiveCreation;
        _buildScreen.ShareBuildRequested += ShareActiveBuild;
        _buildScreen.DeleteCreationRequested += RequestDeleteActiveCreation;
        _buildScreen.PartNameChanged += Build.RenamePart;
        _buildScreen.DeleteSelectionRequested += DeleteSelection;
        _buildScreen.CopySelectionRequested += Build.CopySelectedParts;
        _buildScreen.CreationLockedPressed += () => NotifyUnavailable("build.locked", UiTextTranslation.Source(BuildViewModel.LockedReason)!);
        _buildScreen.NoEffectSettingPressed += setting => NotifyUnavailable($"build.setting.{(PartParameterId)setting}", UiTextTranslation.Source(PartParameters.Of((PartParameterId)setting).NoEffectReason)!, icon: null);
        _buildScreen.ComingLaterPartPressed += part => NotifyUnavailable($"build.part.{(BuildPart)part}", UiTextTranslation.Source(PartTray.ComingLaterReason((BuildPart)part))!);
        _buildScreen.ComingLaterLinkPressed += link => NotifyUnavailable($"build.link.{(BuildLink)link}", UiTextTranslation.Source(BuildLinkList.ComingLaterReason((BuildLink)link))!);
    }

    // A locked Creation keeps Delete in view; when deleting would change the model, it says why (#896)
    // beside the notes the refused delete puts on the parts that block it (#987).
    private void DeleteSelection()
    {
        if (UiTextTranslation.Source(Build.DeleteLockedReason) is { } reason)
        {
            NotifyUnavailable("build.delete", reason);
        }

        Build.DeleteSelectedParts();
    }

    // A tap answer: tapping again does not queue it twice, and the next tap's answer replaces it (#1004).
    private void NotifyUnavailable(string id, Func<string> reason, UiIconId? icon = UiIconId.Lock) =>
        UiNotificationLayer.Enqueue(this, new UiNotificationSpec(UiPopupType.Default, "Build", string.Empty, Icon: icon is { } shown ? new(shown) : null)
        {
            MessageSource = reason,
            Id = id,
            Replaceable = true,
        });

    // Train setup and Training open in their own scenes from the saved creation, so the edits save
    // first; a creature that cannot train stays in Build, which points at what blocks it (#844).
    private void StartTraining()
    {
        if (_autosave?.CreationId is not { } id || !SaveEdits(playerAsked: true))
        {
            return;
        }

        if (!Build.TryGetTrainableCreature(out _))
        {
            Build.ShowTrainingBlockers();
            return;
        }

        // Training made it a real creation: Back from training must not treat it as an untouched New.
        if (_route?.IsNew == true)
        {
            _route = new BuildRoute(id);
            _navigator?.ReplaceCurrent(_route);
        }

        _navigator?.Navigate(new SceneNavigation(new TrainSetupRoute(id)));
    }

    // The copy keeps the trained brain, so the player can change one and keep the other. Build then
    // opens the copy in place of the original (#840), so Back still returns to Creations.
    private void CopyActiveCreation()
    {
        if (_autosave?.CreationId is not { } id || !SaveEdits(playerAsked: true))
        {
            return;
        }

        if (CreationActions.TryCopy(this, Build.CreationName, () => Saves.Duplicate(id, UiTextTranslation.Now)) is not { } copy)
        {
            return;
        }

        UiNotificationLayer.Enqueue(this, new UiNotificationSpec(
            UiPopupType.Default, "Creation copied", string.Empty, Icon: new(UiIconId.Copy))
        {
            MessageSource = UiTextTranslation.Source(UiText.Format("Now editing {0}. The original is in Creations.", copy.Name)),
        });
        _navigator?.Navigate(new SceneNavigation(new BuildRoute(copy.Id), KeepCurrent: false));
    }

    // The code is made from the build on screen, so it needs no save first (#899).
    private void ShareActiveBuild()
    {
        if (_autosave?.CreationId is not { } id)
        {
            return;
        }

        UiClipboard.Copy(this, CreationShareCode.Create(new CreationDef(id, Build.CreationName, Build.Snapshot())), new UiNotificationSpec(
            UiPopupType.Default, "Code copied", string.Empty, Icon: new(UiIconId.Share))
        {
            Id = $"creation.share.{id}",
            MessageSource = UiTextTranslation.Source(
                UiText.Format("Paste it in a chat to share {0}. It holds the build, not the training.", Build.CreationName)),
        });
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
        // A picked tray part is the step Back takes first (#805).
        if (Build.ClearPickedPart())
        {
            return;
        }

        LeaveCreation(playerAsked: true);
        _navigator?.Back();
    }

    // Saves the drawing as it stands, finished or not: only training needs a creature that can be
    // simulated (#515). Every saved edit keeps the training, with its brain refitted to the edited
    // creature's ports (ICreationUpdateCoordinator.ApplyEdit, #516). Returns false when edits are left unsaved; the next save tries again. A save
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
                source => source.WithName(name)),
            $"Renaming Creation '{id}'"))
        {
            return;
        }

        if (renamed is not null)
        {
            Build.SetCreationName(renamed.Name);
        }
    }

    private void RequestResetTraining()
    {
        if (_autosave?.CreationId is null || _dialog.IsOpen)
        {
            return;
        }

        var warning = UiTextTranslation.Source(new BuildPresentationViewModel(Build).ResetTrainingWarning);
        _dialog.Open(CreationActions.ResetTrainingDialog(warning, ResetActiveCreationTraining));
    }

    private bool ResetActiveCreationTraining()
    {
        if (_autosave?.CreationId is not { } id || !SaveEdits(playerAsked: true))
        {
            return false;
        }

        if (!CreationActions.TryRunFileOperation(
            () => Saves.ResetTraining(id),
            $"Resetting training for Creation {id}"))
        {
            return false;
        }

        if (Saves.Get(id) is { } creation)
        {
            EditCreation(creation, openedAsNew: false);
        }

        return true;
    }

    private void RequestUnlock()
    {
        if (!Build.IsLocked || _dialog.IsOpen)
        {
            return;
        }

        _dialog.Open(CreationActions.UnlockDialog(Build.CreationName, Build.Unlock));
    }

    private void RequestDeleteActiveCreation()
    {
        if (_autosave?.CreationId is not { } id || _dialog.IsOpen)
        {
            return;
        }

        var name = Build.CreationName;
        _dialog.Open(CreationActions.DeleteDialog(name, () => DeleteCreation(id, name)));
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
    private void OnDialogFinished(bool confirmed)
    {
        if (_activeCreationDeleted)
        {
            _activeCreationDeleted = false;
            ShowCreations();
        }
    }
}
