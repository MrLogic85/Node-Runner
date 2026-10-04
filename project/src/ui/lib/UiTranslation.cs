using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Godot does not pass a Control's <c>translation_context</c> on to its children, so a component
/// that shows its own text through an inner control hands its context on (#777). A context set on
/// the component in the Inspector then picks the translation its inner text uses.
/// </summary>
public static class UiTranslation
{
    /// <summary>
    /// Gives <paramref name="inner"/> the context of <paramref name="owner"/>. Godot does not
    /// translate again when only the context changes, so a changed context re-translates the
    /// inner control's current text.
    /// </summary>
    public static void ShareContext(Control owner, Control inner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(inner);
        if (inner.TranslationContext == owner.TranslationContext)
        {
            return;
        }

        inner.TranslationContext = owner.TranslationContext;
        inner.Notification((int)Node.NotificationTranslationChanged);
    }
}
