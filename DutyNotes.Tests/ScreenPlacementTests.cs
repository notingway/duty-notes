using System.Numerics;

namespace DutyNotes.Tests;

/// <summary>A window that was saved partly off the screen is moved back where it can be seen and grabbed, and one that is fine is left alone.</summary>
public sealed class ScreenPlacementTests : CheckBase
{
    private static readonly Vector2 Origin = Vector2.Zero;
    private static readonly Vector2 Screen = new(1920, 1080);
    private static readonly Vector2 Window = new(420, 240);

    private static Vector2 Place(Vector2 pos, Vector2? size = null, Vector2? screen = null, Vector2? screenPos = null)
        => ScreenPlacement.KeepOnScreen(pos, size ?? Window, screenPos ?? Origin, screen ?? Screen);

    [Fact]
    public void AWindowThatIsReachableIsLeftAlone()
    {
        Check("a window well inside the screen does not move", Place(new Vector2(300, 200)) == new Vector2(300, 200));
        Check("a window in the top-left corner does not move", Place(new Vector2(0, 0)) == new Vector2(0, 0));
        Check("a window touching the bottom-right does not move", Place(new Vector2(1500, 840)) == new Vector2(1500, 840));
        Check("a window hanging off the left, with enough still visible, does not move", Place(new Vector2(-300, 100)) == new Vector2(-300, 100));
        Check("a window hanging off the right, with enough still visible, does not move", Place(new Vector2(1800, 100)) == new Vector2(1800, 100));
        Check("a window with less than 80 pixels visible on the right is pulled in to 80", Place(new Vector2(1850, 100)) == new Vector2(1840, 100));
    }

    [Fact]
    public void AWindowAboveTheScreenIsMovedDown()
    {
        // The case that was seen in game: only a sliver of the window was on the screen, at the top.
        Check("a window above the top is brought down to the top edge", Place(new Vector2(500, -230)) == new Vector2(500, 0));
        Check("a window far above the top is brought down to the top edge", Place(new Vector2(500, -5000)) == new Vector2(500, 0));
    }

    [Fact]
    public void AWindowTooFarToAnySideIsMovedBackIn()
    {
        Check("a window past the left edge keeps 80 pixels visible", Place(new Vector2(-2000, 100)) == new Vector2(-340, 100));
        Check("a window past the right edge keeps 80 pixels visible", Place(new Vector2(5000, 100)) == new Vector2(1840, 100));
        Check("a window below the bottom keeps 80 pixels visible", Place(new Vector2(100, 5000)) == new Vector2(100, 1000));
    }

    [Fact]
    public void ACornerCaseMovesBothWays()
    {
        Check("a window off the top-left corner is corrected in both directions", Place(new Vector2(-9000, -9000)) == new Vector2(-340, 0));
        Check("a window off the bottom-right corner is corrected in both directions", Place(new Vector2(9000, 9000)) == new Vector2(1840, 1000));
    }

    [Fact]
    public void ASecondMonitorOrViewportKeepsItsOwnOrigin()
    {
        // A window on a screen whose top-left is not (0, 0): the rule is relative to that screen.
        var screenPos = new Vector2(1920, 0);
        Check("a window on the second screen is left alone", Place(new Vector2(2400, 200), screenPos: screenPos) == new Vector2(2400, 200));
        Check("a window above the second screen is brought to its top edge", Place(new Vector2(2400, -500), screenPos: screenPos) == new Vector2(2400, 0));
    }

    [Fact]
    public void SmallAndOddWindowsAreHandled()
    {
        var tiny = new Vector2(40, 30);
        Check("a window smaller than the visible minimum has to stay in as a whole", Place(new Vector2(-9000, 100), tiny) == new Vector2(0, 100));
        Check("an unknown screen size moves nothing", Place(new Vector2(-9000, -9000), screen: Vector2.Zero) == new Vector2(-9000, -9000));
        Check("a window bigger than the screen still gets its top-left on the screen", Place(new Vector2(-500, -500), new Vector2(4000, 3000)).Y == 0);
    }
}