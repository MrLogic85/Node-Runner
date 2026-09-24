namespace NodeRunner.Ui.Lib;

public enum UiPopupType { Default, Warn, Danger }

public sealed record UiDialogSpec(
    UiPopupType Type, string Title, string Content, string? ActionText = null,
    bool HoldToAction = false, string AbortText = "Cancel")
{
    private Func<Task<UiDialogResult>> _action = () => Task.FromResult(UiDialogResult.Success);

    public Func<Task<UiDialogResult>> Action
    {
        get => _action;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _action = value;
        }
    }

    public bool HasAction => !string.IsNullOrWhiteSpace(ActionText);

    public UiDialogSpec(
        UiPopupType type, string title, string content, string? actionText,
        Func<Task<UiDialogResult>> action, bool holdToAction = false, string abortText = "Cancel")
        : this(type, title, content, actionText, holdToAction, abortText)
    {
        Action = action;
    }
}

/// <summary>An action either succeeds or supplies a user-facing error for retry.</summary>
public sealed record UiDialogResult
{
    public static UiDialogResult Success { get; } = new(errorMessage: null);
    public string? ErrorMessage { get; }
    public bool Succeeded => ErrorMessage is null;

    private UiDialogResult(string? errorMessage) => ErrorMessage = errorMessage;

    public static UiDialogResult Failure(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new(message);
    }
}

public sealed record UiNotificationSpec(
    UiPopupType Type, string Title, string Message, Func<bool>? OnClick = null);
