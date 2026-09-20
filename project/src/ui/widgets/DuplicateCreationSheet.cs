using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

public partial class DuplicateCreationSheet : Control
{
    private UiTokens _tokens = UiTokens.Neon;
    private UiSheet? _sheet;
    private string _creationName = string.Empty;

    [Signal]
    public delegate void CopyBrainRequestedEventHandler();

    [Signal]
    public delegate void StartFreshRequestedEventHandler();

    [Signal]
    public delegate void CancelRequestedEventHandler();

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            if (IsInsideTree())
            {
                Rebuild();
            }
        }
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Rebuild();
        Hide();
    }

    public void ShowFor(string creationName)
    {
        _creationName = creationName;
        if (IsInsideTree())
        {
            Rebuild();
        }

        Show();
    }

    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        AddChild(new ColorRect
        {
            Color = new Color(0, 0, 0, 0.52f),
            MouseFilter = MouseFilterEnum.Stop,
            AnchorRight = 1,
            AnchorBottom = 1,
        });

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        _sheet = new UiSheet
        {
            Tokens = _tokens,
            Title = "Duplicate Creation",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _sheet.SetBody(CreateBody());
        center.AddChild(_sheet);
    }

    private Control CreateBody()
    {
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 12);
        stack.AddChild(CreateLabel(
            $"Duplicate {_creationName}? Copy brain keeps trained progress. Start fresh keeps the body and creates an untrained copy.",
            16,
            _tokens.Ink));

        var defaultLabel = CreateLabel("Copy brain is selected by default.", 14, _tokens.Accent);
        stack.AddChild(defaultLabel);

        var actions = new VBoxContainer();
        actions.AddThemeConstantOverride("separation", 12);
        var copy = CreateButton("Copy brain", UiActionButton.ActionKind.Primary);
        copy.Pressed += () => EmitSignal(SignalName.CopyBrainRequested);
        actions.AddChild(copy);
        var fresh = CreateButton("Start fresh", UiActionButton.ActionKind.Secondary);
        fresh.Pressed += () => EmitSignal(SignalName.StartFreshRequested);
        actions.AddChild(fresh);
        var cancel = CreateButton("Cancel", UiActionButton.ActionKind.Secondary);
        cancel.Pressed += () => EmitSignal(SignalName.CancelRequested);
        actions.AddChild(cancel);
        stack.AddChild(actions);

        return stack;
    }

    private Label CreateLabel(string text, int size, Color color)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private UiActionButton CreateButton(string text, UiActionButton.ActionKind kind) =>
        new()
        {
            Tokens = _tokens,
            LabelText = text,
            Kind = kind,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
        };
}
