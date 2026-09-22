using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Compatibility facade for existing scene actions over the canonical button.</summary>
public partial class UiActionButton : UiButton
{
    public enum ActionKind
    {
        Primary,
        Secondary,
        Danger,
        Flat,
    }

    private ActionKind _kind = ActionKind.Secondary;
    private bool _locked;
    private string _lockReason = "Unavailable";
    private bool _showLockReasonInText = true;

    [Export]
    public new ActionKind Kind
    {
        get => _kind;
        set
        {
            if (!Enum.IsDefined(value))
            {
                GD.PushError($"Invalid action button kind: {value}. Keeping {_kind}.");
                return;
            }

            Style = value switch
            {
                ActionKind.Primary => UiButtonStyle.Primary,
                ActionKind.Secondary => UiButtonStyle.Secondary,
                ActionKind.Danger => UiButtonStyle.Tertiary,
                ActionKind.Flat => UiButtonStyle.Flat,
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
            };
            _kind = value;
        }
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

    [Export]
    public bool ShowLockReasonInText
    {
        get => _showLockReasonInText;
        set
        {
            _showLockReasonInText = value;
            RefreshStyle();
        }
    }

    protected override string DisplayText =>
        (Locked && ShowLockReasonInText ? $"{LabelText} · {LockReason}" : LabelText).ToUpperInvariant();

    protected override string AccessibleDescription => Locked ? LockReason : string.Empty;
}
