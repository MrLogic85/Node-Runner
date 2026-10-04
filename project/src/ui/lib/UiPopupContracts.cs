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

    /// <summary>The glyph beside the title; null keeps the severity's default.</summary>
    public UiNotificationIcon? Icon { get; init; }

    /// <summary>
    /// Already translated text shown instead of <see cref="Title"/>; asked again when the language
    /// changes, with the title's own auto-translation off meanwhile.
    /// </summary>
    public Func<string>? TitleSource { get; init; }

    /// <summary>
    /// Already translated text shown instead of <see cref="Content"/>; asked again when the language
    /// changes, with the body's own auto-translation off meanwhile.
    /// </summary>
    public Func<string>? ContentSource { get; init; }

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
    public static UiDialogResult Success { get; } = new(errorMessage: null, errorSource: null);
    public string? ErrorMessage { get; }

    /// <summary>
    /// Already translated error shown instead of <see cref="ErrorMessage"/>; asked again when the
    /// language changes, with the error's own auto-translation off meanwhile.
    /// </summary>
    public Func<string>? ErrorSource { get; }

    public bool Succeeded => ErrorMessage is null && ErrorSource is null;

    private UiDialogResult(string? errorMessage, Func<string>? errorSource)
    {
        ErrorMessage = errorMessage;
        ErrorSource = errorSource;
    }

    public static UiDialogResult Failure(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new(message, errorSource: null);
    }

    public static UiDialogResult Failure(Func<string> messageSource)
    {
        ArgumentNullException.ThrowIfNull(messageSource);
        return new(errorMessage: null, messageSource);
    }
}

public sealed record UiNotificationSpec(
    UiPopupType Type, string Title, string Message, Func<bool>? OnClick = null,
    UiNotificationIcon? Icon = null)
{
    /// <summary>
    /// Already translated text shown instead of <see cref="Message"/>; asked again when the language
    /// changes, with the message's own auto-translation off meanwhile.
    /// </summary>
    public Func<string>? MessageSource { get; init; }
}
