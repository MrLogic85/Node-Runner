using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiSurfaceContractsTests
{
    [Theory]
    [InlineData(UiSurfaceContracts.FrameVariant.Lock, true, true, false)]
    [InlineData(UiSurfaceContracts.FrameVariant.Sel, false, false, true)]
    [InlineData(UiSurfaceContracts.FrameVariant.StageCard, false, false, true)]
    [InlineData(UiSurfaceContracts.FrameVariant.Warn, false, false, false)]
    public void FrameVariantHelpers_DescribeOnlySharedVisualSemantics(
        UiSurfaceContracts.FrameVariant variant,
        bool expectedDashed,
        bool expectedDimmed,
        bool expectedGlow)
    {
        UiSurfaceContracts.IsDashed(variant).ShouldBe(expectedDashed);
        UiSurfaceContracts.IsDimmed(variant).ShouldBe(expectedDimmed);
        UiSurfaceContracts.HasGlow(variant).ShouldBe(expectedGlow);
    }
}
