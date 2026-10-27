using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildGridSizesTests
{
    [Fact]
    public void Labels_ShowEachSizeInMetres_WithTheDecimalsItNeeds()
    {
        BuildGridSizes.Labels.ShouldBe(
        [
            UiText.Format("{0} m", new FixedNumber(0.25, 2)),
            UiText.Format("{0} m", new FixedNumber(0.5, 1)),
            UiText.Format("{0} m", new FixedNumber(1, 0)),
        ]);
    }
}
