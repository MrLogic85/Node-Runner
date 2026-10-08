using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public class UiClipboardTests
{
    [Theory]
    [InlineData("Android", "13 (SDK 33 build T123)", true)]
    [InlineData("Android", "14 (SDK 34 build UP1A.231005)", true)]
    [InlineData("Android", "12 (SDK 32 build S1)", false)]
    [InlineData("Android", "", false)]
    [InlineData("Android", "(SDK 99999999999 build x)", false)]
    [InlineData("macOS", "15 (SDK 34 build x)", false)]
    public void OnlyAndroid13AndLater_ConfirmsACopyItself(string osName, string versionAlias, bool confirms) =>
        UiClipboard.SystemConfirmsCopy(osName, versionAlias).ShouldBe(confirms);
}
