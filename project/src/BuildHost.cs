using Godot;
using NodeRunner.App.Navigation;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Managers;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;
using NodeRunner.Ui.Widgets;

namespace NodeRunner;

/// <summary>
/// The Build scene for one creation, or a new draft. Training lives in its own scene,
/// <see cref="SimulateHost"/> (#469).
/// </summary>
public partial class BuildHost : Node2D, IRoutedScene
{
    private readonly VisualTheme _theme = VisualTheme.Neon;
    private BuildScreen? _buildScreen;
    private UiDialog? _deleteCreationDialog;
    private bool _activeCreationDeleted;
    private ISceneNavigator? _navigator;
    private BuildRoute? _route;
    private Guid? _activeCreationId;

    public ConstructionViewModel Construction { get; } = new();

    private ConstructionPresentationViewModel ConstructionPresentation => new(Construction);

    private SaveManager Saves => GetNode<SaveManager>("/root/SaveManager");

    public void Enter(SceneRoute route, ISceneNavigator navigator)
    {
        _route = (BuildRoute)route;
        _navigator = navigator;
    }

    public override void _Ready()
    {
        ApplyProgression();
        AddBuildModeBackdrop();
        AddConstructionCanvas();
        AddBuildScreen();
        AddDeleteCreationDialog();
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

        var back = new UiBackHandler { CanTakeBack = () => _deleteCreationDialog?.IsOpen != true };
        back.BackRequested += OnBackRequested;
        AddChild(back, @internal: InternalMode.Front);
    }

    private void OnBackRequested()
    {
        if (_buildScreen?.CloseOverlay() != true)
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

    private void AddBuildModeBackdrop()
    {
        var layer = new CanvasLayer
        {
            Name = "BuildModeBackdropLayer",
            Layer = -1,
        };
        AddChild(layer);

        var backdrop = new ColorRect
        {
            Name = "BuildModeBackdrop",
            Color = UiThemes.Color(UiThemes.Neon, UiTokens.Color.Background),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(backdrop);
    }

    private void AddConstructionCanvas()
    {
        AddChild(new ConstructionCanvas
        {
            Name = "ConstructionCanvas",
            Theme = _theme,
            ViewModel = Construction,
            Position = new Vector2(250, 260),
        });
    }

    private void AddBuildScreen()
    {
        var buildLayer = new CanvasLayer
        {
            Name = "BuildOverlay",
            Layer = 2,
        };
        AddChild(buildLayer);

        _buildScreen = new BuildScreen
        {
            Name = "LiveBuildScreen",
            Hosted = true,
            ShowCanvasPreview = false,
            Presentation = ConstructionPresentation,
        };
        _buildScreen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _buildScreen.ToolRequested += tool => Construction.ActiveTool = (ConstructionTool)(int)tool;
        _buildScreen.BrainShapeChanged += (layers, neurons) =>
        {
            if (!Construction.IsMoveOnly)
            {
                Construction.SetBrainShape(new BrainShapeDef(layers, neurons));
            }
        };
        _buildScreen.SaveRequested += SaveCreationFromBuild;
        _buildScreen.BackRequested += BackFromBuildScreen;
        _buildScreen.CreationNameChanged += RenameActiveCreation;
        _buildScreen.ResetTrainingRequested += ResetActiveCreationTraining;
        _buildScreen.DeleteCreationRequested += RequestDeleteActiveCreation;
        _buildScreen.ClearSelectionRequested += Construction.ClearSelection;
        _buildScreen.DeleteSelectionRequested += Construction.DeleteSelectedParts;
        _buildScreen.ResumeTrainingRequested += ResumeTrainingFromSavedCreation;
        _buildScreen.StatsRequested += () => Notify("Stats", "Stats open in milestone 0.12.0.");
        _buildScreen.BrainRequested += () => Notify("Brain view", "Brain view opens in milestone 0.12.0.");
        buildLayer.AddChild(_buildScreen);
    }

    // Delete from Build's overflow menu; the Creations scene has its own.
    private void AddDeleteCreationDialog()
    {
        var layer = new CanvasLayer
        {
            Name = "DialogLayer",
            Layer = 20,
        };
        AddChild(layer);
        _deleteCreationDialog = new UiDialog();
        _deleteCreationDialog.Finished += OnDeleteCreationDialogFinished;
        layer.AddChild(_deleteCreationDialog);
    }

    private void ShowCreations() => _navigator?.ReturnToRoot();

    private void Notify(string title, string message) =>
        UiNotificationLayer.Enqueue(this, new UiNotificationSpec(UiPopupType.Default, title, message));

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
        _navigator?.Navigate(new SceneNavigation(new SimulateRoute(id)));
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
        Construction.SetCompletedMessage(succeeded && editResult is not null
            ? editResult.StatusMessage
            : "Could not save the edited creature; your edit was discarded.");
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
        _navigator.Navigate(new SceneNavigation(new SimulateRoute(creation.Id)));
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
            $"Creation {saves.List().Count + 1}",
            Construction.HasCustomBrainShape ? Construction.BrainShape : RecommendedBrainShape(creature));
        if (!CreationActions.TryRunFileOperation(
            () => saves.Save(completedCreation),
            $"Saving Creation '{completedCreation.Name}'"))
        {
            Construction.SetCompletedMessage("Save failed — see log.");
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
        if (_activeCreationId is not { } id || _deleteCreationDialog is null || _deleteCreationDialog.IsOpen)
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
