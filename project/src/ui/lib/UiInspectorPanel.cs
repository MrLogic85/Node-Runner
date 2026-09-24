using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Part settings panel: header, shared row stack, and optional destructive action.</summary>
public partial class UiInspectorPanel : UiCard
{
    [Signal]
    public delegate void CloseRequestedEventHandler();

    [Signal]
    public delegate void DeleteRequestedEventHandler();

    [Signal]
    public delegate void HeaderActionSelectedEventHandler(string actionId);

    private UiIconId _glyphIconId = UiIconId.Gear;
    private Control[] _rows = [];
    private VBoxContainer? _stack;
    private VBoxContainer? _rowsContainer;
    private UiPanelHeader? _header;
    private MarginContainer? _deleteContainer;
    private UiButton? _deleteButton;
    private string _title = "Part settings";
    private string _deleteText = string.Empty;

    [Export]
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            Rebuild();
        }
    }

    [Export]
    public UiIconId GlyphIconId
    {
        get => _glyphIconId;
        set
        {
            _glyphIconId = value;
            Rebuild();
        }
    }

    [Export]
    public string DeleteText
    {
        get => _deleteText;
        set
        {
            _deleteText = value;
            Rebuild();
        }
    }

    public override UiTokens Tokens
    {
        get => base.Tokens;
        set
        {
            base.Tokens = value;
            Rebuild();
        }
    }

    public override void _Ready()
    {
        Kind = CardVariant.Frame;
        SizeVariant = CardSize.Default;
        base._Ready();
        Rebuild();
    }

    public void SetRows(params Control[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        _rows = rows;
        Rebuild();
    }

    private void Rebuild()
    {
        if (!IsInsideTree())
        {
            return;
        }

        EnsureShell();
        _stack!.AddThemeConstantOverride("separation", (int)Tokens.Space1);
        _rowsContainer!.AddThemeConstantOverride("separation", (int)Tokens.Space1);
        _header!.Title = Title;
        _header.GlyphIconId = GlyphIconId;
        _header.Tokens = Tokens;

        foreach (var child in _rowsContainer.GetChildren().OfType<Node>().ToArray())
        {
            if (!_rows.Contains(child))
            {
                _rowsContainer.RemoveChild(child);
                child.QueueFree();
            }
        }

        for (var index = 0; index < _rows.Length; index++)
        {
            var row = _rows[index];
            if (row.GetParent() != _rowsContainer)
            {
                row.GetParent()?.RemoveChild(row);
                _rowsContainer.AddChild(row);
            }

            _rowsContainer.MoveChild(row, index);
            row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            UiTokenApplier.Apply(row, Tokens);
        }

        _deleteContainer!.Visible = !string.IsNullOrWhiteSpace(DeleteText);
        if (!string.IsNullOrWhiteSpace(DeleteText))
        {
            _deleteContainer.AddThemeConstantOverride("margin_top", (int)Tokens.Space1);
            _deleteButton!.LabelText = DeleteText;
            _deleteButton.Tokens = Tokens;
        }
    }

    private void EnsureShell()
    {
        if (_stack is not null)
        {
            return;
        }

        _stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        AddChild(_stack);

        _header = new UiPanelHeader
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _header.SetActions(new UiPanelHeader.PanelAction("close", UiIconId.Close, "Close settings"));
        _header.ActionSelected += OnHeaderActionSelected;
        _stack.AddChild(_header);

        _rowsContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _stack.AddChild(_rowsContainer);

        _deleteButton = new UiButton
        {
            IconId = UiIconId.Trash,
            Style = UiButtonStyle.Tertiary,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _deleteContainer = new MarginContainer();
        _deleteButton.Activated += () => EmitSignal(SignalName.DeleteRequested);
        _deleteContainer.AddChild(_deleteButton);
        _stack.AddChild(_deleteContainer);
    }

    private void OnHeaderActionSelected(string actionId)
    {
        EmitSignal(SignalName.HeaderActionSelected, actionId);
        if (actionId == "close")
        {
            EmitSignal(SignalName.CloseRequested);
        }
    }
}
