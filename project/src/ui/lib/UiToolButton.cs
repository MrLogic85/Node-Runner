using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Compatibility facade for a selectable or locked canonical secondary button.</summary>
public partial class UiToolButton : UiButton
{
    [Signal]
    public delegate void ToolActivatedEventHandler();

    private string _toolLabel = "Tool";
    private string _iconText = "•";
    private bool _locked;
    private string _lockReason = "Unavailable";

    public UiToolButton()
    {
        Style = UiButtonStyle.Secondary;
        IconId = UiIconId.Move;
    }

    [Export]
    public string ToolLabel
    {
        get => _toolLabel;
        set
        {
            _toolLabel = value;
            LabelText = value;
        }
    }

    [Export]
    public string IconText
    {
        get => _iconText;
        set
        {
            _iconText = value;
            RefreshStyle();
        }
    }

    [Export]
    public new UiIconId IconId
    {
        get => base.IconId ?? UiIconId.Move;
        set => base.IconId = value;
    }

    [Export]
    public bool Active
    {
        get => On;
        set => On = value;
    }

    [Export]
    public bool Locked
    {
        get => _locked;
        set
        {
            _locked = value;
            Enabled = !value;
        }
    }

    [Export]
    public string LockReason
    {
        get => _lockReason;
        set
        {
            _lockReason = value;
            RefreshStyle();
        }
    }

    protected override string DisplayText =>
        (Locked ? $"{ToolLabel} · {LockReason}" : ToolLabel).ToUpperInvariant();

    protected override string AccessibleDescription => Locked ? LockReason : ToolLabel;

    protected override void OnActivated() => EmitSignal(SignalName.ToolActivated);
}
