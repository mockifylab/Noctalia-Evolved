using NoctaliaWin.Core.Geometry;
using Xunit;

namespace NoctaliaWin.Tests.Geometry;

public sealed class PanelRevealTests
{
    [Theory]
    [InlineData(AttachedRevealDirection.Down, 0.0, 0.0, -200.0)]
    [InlineData(AttachedRevealDirection.Down, 1.0, 0.0, 0.0)]
    [InlineData(AttachedRevealDirection.Up, 0.0, 0.0, 200.0)]
    [InlineData(AttachedRevealDirection.Left, 0.0, 300.0, 0.0)]
    [InlineData(AttachedRevealDirection.Right, 0.0, -300.0, 0.0)]
    [InlineData(AttachedRevealDirection.Up, 0.5, 0.0, 100.0)]
    [InlineData(AttachedRevealDirection.Right, 0.25, -225.0, 0.0)]
    public void ContentSlidesFromBehindTheBar(
        AttachedRevealDirection direction, double progress, double expectedX, double expectedY)
    {
        var reveal = PanelRevealMath.At(progress, 300, 200, 12f, direction);

        Assert.Equal(expectedX, reveal.ContentOffsetX, 3);
        Assert.Equal(expectedY, reveal.ContentOffsetY, 3);
    }

    [Fact]
    public void VerticalPanelsTravelAlongTheirWidth()
    {
        var reveal = PanelRevealMath.At(0.5, 300, 200, 12f, AttachedRevealDirection.Left);

        Assert.Equal(150, reveal.ContentOffsetX, 3);
        Assert.Equal(0, reveal.ContentOffsetY, 3);
    }

    [Fact]
    public void BulgesOnlyAppearInTheLastRadiusOfTravel()
    {
        // A 200px panel with a 12px radius: the concave corners are still hidden until the
        // content is within 12px of its final position.
        var early = PanelRevealMath.At(0.5, 300, 200, 12f, AttachedRevealDirection.Down);
        var nearlyThere = PanelRevealMath.At(0.95, 300, 200, 12f, AttachedRevealDirection.Down);
        var settled = PanelRevealMath.At(1.0, 300, 200, 12f, AttachedRevealDirection.Down);

        Assert.Equal(0f, early.BulgeRadius);
        Assert.Equal(2f, nearlyThere.BulgeRadius, 2);
        Assert.Equal(12f, settled.BulgeRadius);
    }

    [Fact]
    public void BulgeRadiusNeverExceedsTheConfiguredRadius()
    {
        for (double p = 0; p <= 1.0; p += 0.05)
        {
            var reveal = PanelRevealMath.At(p, 300, 200, 12f, AttachedRevealDirection.Up);
            Assert.InRange(reveal.BulgeRadius, 0f, 12f);
        }
    }

    [Fact]
    public void ProgressBelowTheVisibilityFloorPublishesNoSurface()
    {
        Assert.False(PanelRevealMath.At(0.0, 300, 200, 12f, AttachedRevealDirection.Down).HasVisibleSurface);
        Assert.True(PanelRevealMath.At(0.02, 300, 200, 12f, AttachedRevealDirection.Down).HasVisibleSurface);
    }

    [Fact]
    public void OffsetsAndBulgesClampOutOfRangeProgress()
    {
        var below = PanelRevealMath.At(-1, 300, 200, 12f, AttachedRevealDirection.Down);
        var above = PanelRevealMath.At(3, 300, 200, 12f, AttachedRevealDirection.Down);

        Assert.Equal(-200, below.ContentOffsetY, 3);
        Assert.Equal(0, above.ContentOffsetY, 3);
        Assert.Equal(12f, above.BulgeRadius);
    }

    [Fact]
    public void OpeningStartsFastAndSettlesSlowly()
    {
        // EaseOutCubic: the halfway point of the clock is past halfway in distance.
        Assert.Equal(0.875, PanelRevealMath.OpenEased(100, 200), 3);
        Assert.Equal(0.0, PanelRevealMath.OpenEased(0, 200), 3);
        Assert.Equal(1.0, PanelRevealMath.OpenEased(200, 200), 3);
    }

    [Fact]
    public void ClosingAttachedPanelUsesEaseInOutQuad()
    {
        Assert.Equal(1.0, PanelRevealMath.AttachedCloseEased(0, 200), 3);
        Assert.Equal(0.5, PanelRevealMath.AttachedCloseEased(100, 200), 3);
        Assert.Equal(0.0, PanelRevealMath.AttachedCloseEased(200, 200), 3);
    }

    [Fact]
    public void ClosingDetachedPanelAcceleratesAway()
    {
        // EaseInQuad on the way out: a quarter of the clock is only a sixteenth of the
        // distance, so the panel leaves slowly and picks up speed.
        Assert.Equal(0.9375, PanelRevealMath.DetachedCloseEased(50, 200), 3);
        Assert.Equal(0.0, PanelRevealMath.DetachedCloseEased(200, 200), 3);
    }

    [Fact]
    public void ZeroDurationAnimationCompletesImmediately()
    {
        Assert.Equal(1.0, PanelRevealMath.OpenEased(0, 0), 3);
        Assert.Equal(0.0, PanelRevealMath.AttachedCloseEased(0, 0), 3);
    }
}
