using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiPopupContractsTests
{
    [Fact]
    public void Types_IncludeDefaultWarningAndDanger()
    {
        Enum.GetNames<UiPopupType>().ShouldBe(["Default", "Warn", "Danger"]);
    }

    [Theory]
    [InlineData(UiPopupType.Default)]
    [InlineData(UiPopupType.Warn)]
    [InlineData(UiPopupType.Danger)]
    public void Hold_IsIndependentOfDialogType(UiPopupType type)
    {
        var spec = new UiDialogSpec(type, "Title", "Content", "Continue", () => Task.FromResult(UiDialogResult.Success));

        spec.HoldToAction.ShouldBeFalse();
        (spec with { HoldToAction = true }).Type.ShouldBe(type);
    }

    [Fact]
    public void Notification_WithoutActionDoesNotRequestClickDismissal()
    {
        var spec = new UiNotificationSpec(UiPopupType.Default, "Title", "Message");

        spec.Icon.ShouldBeNull();
        (spec.OnClick?.Invoke() ?? false).ShouldBeFalse();
        (spec with { OnClick = () => true }).OnClick!().ShouldBeTrue();
    }

    [Fact]
    public void Result_DistinguishesSuccessFromUserFacingFailure()
    {
        UiDialogResult.Success.Succeeded.ShouldBeTrue();
        UiDialogResult.Success.ErrorMessage.ShouldBeNull();
        var result = UiDialogResult.Failure("Could not save.");
        result.Succeeded.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Could not save.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Failure_RequiresVisibleMessage(string message)
    {
        Should.Throw<ArgumentException>(() => UiDialogResult.Failure(message));
    }

    [Fact]
    public async Task Dialog_DefaultActionSucceedsWithoutCallback()
    {
        var spec = new UiDialogSpec(UiPopupType.Default, "Title", "Body", ActionText: "OK", AbortText: "Close");

        spec.HasAction.ShouldBeTrue();
        spec.AbortText.ShouldBe("Close");
        (await spec.Action()).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n")]
    public void Dialog_BlankActionTextOmitsAction(string? text)
    {
        var spec = new UiDialogSpec(UiPopupType.Warn, "Title", "Body", text);

        spec.HasAction.ShouldBeFalse();
        spec.AbortText.ShouldBe("Cancel");
        spec.Action.ShouldNotBeNull();
    }

    [Fact]
    public async Task Dialog_CustomActionPreservesFailure()
    {
        var spec = new UiDialogSpec(UiPopupType.Default, "Title", "Body", "Run")
        {
            Action = () => Task.FromResult(UiDialogResult.Failure("Could not save.")),
        };

        (await spec.Action()).ErrorMessage.ShouldBe("Could not save.");
    }

    [Fact]
    public void Dialog_ExplicitNullActionIsRejected()
    {
        Should.Throw<ArgumentNullException>(() => new UiDialogSpec(UiPopupType.Default, "Title", "Body", "OK", null!));
        Should.Throw<ArgumentNullException>(() => new UiDialogSpec(UiPopupType.Default, "Title", "Body") { Action = null! });
    }
}
