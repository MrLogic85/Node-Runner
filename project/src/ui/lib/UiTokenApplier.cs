using Godot;

namespace NodeRunner.Ui.Lib;

internal static class UiTokenApplier
{
    public static void Apply(Control control, UiTokens tokens)
    {
        switch (control)
        {
            case Label label:
                tokens.ApplyTextStyle(label, tokens.NoteText);
                label.AddThemeColorOverride("font_color", tokens.Muted);
                break;
            case UiButton button:
                button.Tokens = tokens;
                break;
            case UiOverflowMenu menu:
                menu.Tokens = tokens;
                break;
            case UiSegmentedSwitch segmentedSwitch:
                segmentedSwitch.Tokens = tokens;
                break;
            case UiSlider slider:
                slider.Tokens = tokens;
                break;
            case UiToggleRow toggleRow:
                toggleRow.Tokens = tokens;
                break;
            case UiCheckRow checkRow:
                checkRow.Tokens = tokens;
                break;
            case UiPicker picker:
                picker.Tokens = tokens;
                break;
            case UiNameField nameField:
                nameField.Tokens = tokens;
                break;
            case UiNoteRow noteRow:
                noteRow.Tokens = tokens;
                break;
            case UiTextField textField:
                textField.Tokens = tokens;
                break;
            case UiIconTabs iconTabs:
                iconTabs.Tokens = tokens;
                break;
            case UiSelectionHandle selectionHandle:
                selectionHandle.Tokens = tokens;
                break;
            case UiProgressRing progressRing:
                progressRing.Tokens = tokens;
                break;
            case UiNumber number:
                number.Tokens = tokens;
                break;
            case UiInspectorPanel inspectorPanel:
                inspectorPanel.Tokens = tokens;
                break;
            case UiStageCard stageCard:
                stageCard.Tokens = tokens;
                break;
            case UiCard card:
                card.Tokens = tokens;
                break;
            case UiPartRow partRow:
                partRow.Tokens = tokens;
                break;
            case UiValueRow valueRow:
                valueRow.Tokens = tokens;
                break;
            case UiReadonlyValue readonlyValue:
                readonlyValue.Tokens = tokens;
                break;
            case UiMeterRow meterRow:
                meterRow.Tokens = tokens;
                break;
            case UiInfoRow infoRow:
                infoRow.Tokens = tokens;
                break;
            case UiPanelHeader panelHeader:
                panelHeader.Tokens = tokens;
                break;
            case UiChip chip:
                chip.Tokens = tokens;
                break;
        }
    }
}
