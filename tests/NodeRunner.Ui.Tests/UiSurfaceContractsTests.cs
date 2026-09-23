using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiSurfaceContractsTests
{
    [Fact]
    public void FrameVariants_CoverTheCanonicalSharedSurfaceVocabulary()
    {
        UiSurfaceContracts.AllFrameVariants.ShouldBe(
        [
            UiSurfaceContracts.FrameVariant.Frame,
            UiSurfaceContracts.FrameVariant.Sel,
            UiSurfaceContracts.FrameVariant.Pick,
            UiSurfaceContracts.FrameVariant.Lock,
            UiSurfaceContracts.FrameVariant.Warn,
            UiSurfaceContracts.FrameVariant.Hint,
            UiSurfaceContracts.FrameVariant.Ok,
            UiSurfaceContracts.FrameVariant.Raised,
            UiSurfaceContracts.FrameVariant.StageCard,
        ]);
    }

    [Fact]
    public void FrameSizes_CoverDefaultAndAllCanonicalPaddingModes()
    {
        UiSurfaceContracts.AllFrameSizes.ShouldBe(
        [
            UiSurfaceContracts.FrameSize.Default,
            UiSurfaceContracts.FrameSize.Snug,
            UiSurfaceContracts.FrameSize.Tight,
            UiSurfaceContracts.FrameSize.Roomy,
            UiSurfaceContracts.FrameSize.Flush,
        ]);
    }

    [Fact]
    public void RaisedStates_AreGenericEnoughForEveryPressableSurface()
    {
        UiSurfaceContracts.AllRaisedStates.ShouldBe(
        [
            UiSurfaceContracts.RaisedState.Rest,
            UiSurfaceContracts.RaisedState.On,
            UiSurfaceContracts.RaisedState.Lock,
            UiSurfaceContracts.RaisedState.Off,
            UiSurfaceContracts.RaisedState.Primary,
            UiSurfaceContracts.RaisedState.Danger,
        ]);
    }

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
