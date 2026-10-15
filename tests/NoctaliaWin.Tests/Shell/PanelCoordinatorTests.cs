using NoctaliaWin.Core.Geometry;
using NoctaliaWin.Core.Shell;
using Xunit;

namespace NoctaliaWin.Tests.Shell;

public sealed class PanelCoordinatorTests
{
    private static PanelSurface ControlCenter(string id = "control-center") =>
        new(id, new PxRect(500, 40, 623, 414), new PxRect(514, 40, 595, 400), AttachedRevealDirection.Down, 12f);

    private static PanelSurface Calendar() => ControlCenter("calendar");

    private static readonly PxRect[] Bar = [new(180, 10, 1560, 34)];

    [Fact]
    public void ARunningBuildStartsClosed()
    {
        var panels = new PanelCoordinator();

        Assert.Equal(PanelPhase.Closed, panels.Phase);
        Assert.Null(panels.ActiveId);
        Assert.False(panels.KeepsBarShown);
    }

    [Fact]
    public void ToggleOpensAndSecondToggleClosesWithoutACloseAnimation()
    {
        var panels = new PanelCoordinator();

        Assert.True(panels.Toggle(ControlCenter(), Bar));
        Assert.Equal(PanelPhase.Opening, panels.Phase);

        panels.FinishOpen();
        Assert.Equal(PanelPhase.Open, panels.Phase);

        Assert.False(panels.Toggle(ControlCenter(), Bar));
        Assert.Equal(PanelPhase.Closed, panels.Phase);
        Assert.Null(panels.ActiveId);
    }

    [Fact]
    public void OpeningASecondPanelReplacesTheFirstImmediately()
    {
        // Upstream destroys the previous panel with no close animation so the new surface
        // can take its place on the same frame.
        var panels = new PanelCoordinator();
        panels.Toggle(Calendar(), Bar);
        panels.FinishOpen();

        Assert.True(panels.Toggle(ControlCenter(), Bar));
        Assert.Equal("control-center", panels.ActiveId);
        Assert.Equal(PanelPhase.Opening, panels.Phase);
    }

    [Fact]
    public void TheLauncherReplacesTheControlCenterAndViceVersa()
    {
        // One surface, three ids: the launcher is a panel, so opening it from the bar must
        // dismiss an open control centre instead of stacking a second window on it.
        var panels = new PanelCoordinator();
        panels.Toggle(ControlCenter(), Bar);
        panels.FinishOpen();

        Assert.True(panels.Toggle(ControlCenter("launcher"), Bar));
        Assert.Equal("launcher", panels.ActiveId);
        panels.FinishOpen();

        // Toggle is click semantics, so clicking the launcher button again closes it.
        Assert.False(panels.Toggle(ControlCenter("launcher"), Bar));
        Assert.Equal(PanelPhase.Closed, panels.Phase);

        Assert.True(panels.Toggle(ControlCenter("launcher"), Bar));
        Assert.True(panels.Toggle(ControlCenter(), Bar));
        Assert.Equal("control-center", panels.ActiveId);
    }

    [Fact]
    public void ClosingRevealsNothingUntilTheAnimatorFinishes()
    {
        var panels = new PanelCoordinator();
        panels.Toggle(ControlCenter(), Bar);
        panels.FinishOpen();

        panels.BeginClose();

        Assert.Equal(PanelPhase.Closing, panels.Phase);
        // The surface is still published while it slides away, and the bar still has to stay
        // shown for it.
        Assert.NotNull(panels.Active);
        Assert.True(panels.KeepsBarShown);

        panels.FinishClose();
        Assert.Equal(PanelPhase.Closed, panels.Phase);
        Assert.Null(panels.Active);
    }

    [Fact]
    public void ClosingFromTheClosedStateIsANoOp()
    {
        var panels = new PanelCoordinator();

        panels.BeginClose();
        Assert.Equal(PanelPhase.Closed, panels.Phase);

        panels.FinishOpen();
        Assert.Equal(PanelPhase.Closed, panels.Phase);
    }

    [Fact]
    public void AnyOpenPhaseKeepsTheAttachedBarShown()
    {
        var panels = new PanelCoordinator();
        panels.Toggle(ControlCenter(), Bar);

        Assert.Equal(PanelPhase.Opening, panels.Phase);
        Assert.True(panels.KeepsBarShown);

        panels.FinishOpen();
        Assert.True(panels.KeepsBarShown);

        panels.BeginClose();
        Assert.True(panels.KeepsBarShown);

        panels.FinishClose();
        Assert.False(panels.KeepsBarShown);
    }

    [Fact]
    public void ClicksInsideThePanelBodyAreAbsorbed()
    {
        var panels = new PanelCoordinator();
        panels.Toggle(ControlCenter(), Bar);
        var surface = panels.Active!.VisualRect;

        Assert.True(panels.AbsorbsPointer(surface.X + 5, surface.Y + 5));
        Assert.False(panels.AbsorbsPointer(surface.Right, surface.Bottom));
    }

    [Fact]
    public void ClicksInTheFlareMarginDismissThePanel()
    {
        var panels = new PanelCoordinator();
        panels.Toggle(ControlCenter(), Bar);
        var active = panels.Active!;

        // The concave flare sticks out past the body on the bar side; a click there is a
        // click on nothing, as long as it is below the bar itself.
        Assert.True(panels.DismissesAt(
            active.SurfaceRect.X + 1,
            active.VisualRect.Y + active.VisualRect.Height / 2));
    }

    [Fact]
    public void ClicksOnTheBarDoNotCountAsDismissals()
    {
        // The click shield excludes the bar rects so bar widgets keep working while a panel
        // is open; closing through a widget is the widget's own toggle.
        var panels = new PanelCoordinator();
        panels.Toggle(ControlCenter(), Bar);

        Assert.False(panels.DismissesAt(900, 20));
        Assert.True(panels.DismissesAt(900, 600));
    }

    [Fact]
    public void PointerTestsAreFalseWhenNothingIsOpen()
    {
        var panels = new PanelCoordinator();

        Assert.False(panels.AbsorbsPointer(0, 0));
        Assert.False(panels.DismissesAt(0, 0));
    }

    [Fact]
    public void EveryBarPanelWidgetMapsToARealPanel()
    {
        Assert.Equal("calendar", PanelCoordinator.PanelFor("clock"));
        Assert.Equal("control-center", PanelCoordinator.PanelFor("volume"));
        Assert.Equal("control-center", PanelCoordinator.PanelFor("network"));
        Assert.Equal("control-center", PanelCoordinator.PanelFor("battery"));
        Assert.Equal("control-center", PanelCoordinator.PanelFor("control-center"));
        Assert.Equal("launcher", PanelCoordinator.PanelFor("launcher"));

        Assert.True(PanelCoordinator.OpensPanel("clock"));
        Assert.False(PanelCoordinator.OpensPanel("spacer"));

        // The launcher is a panel: it grows out of the bar, takes focus and closes on Escape.
        Assert.True(PanelCoordinator.OpensPanel("launcher"));

        // Settings is its own window, so the bar button must not steal the panel surface.
        Assert.False(PanelCoordinator.OpensPanel("settings"));
    }

    [Fact]
    public void ChangeEventsFireOnEveryTransition()
    {
        var panels = new PanelCoordinator();
        int changes = 0;
        panels.Changed += () => changes++;

        panels.Toggle(ControlCenter(), Bar);
        panels.FinishOpen();
        panels.BeginClose();
        panels.FinishClose();

        Assert.Equal(4, changes);
    }
}
