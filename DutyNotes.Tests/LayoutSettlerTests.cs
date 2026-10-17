using System.Numerics;

namespace DutyNotes.Tests;

/// <summary>An auto-sizing window is kept off the screen while it catches up with new content, and shown as soon as its size has settled.</summary>
public sealed class LayoutSettlerTests : CheckBase
{
    private static readonly Vector2 Old = new(300, 200);
    private static readonly Vector2 TallerOnly = new(300, 260);
    private static readonly Vector2 New = new(420, 260);

    /// <summary>One frame, as the overlay does it: counted before it is drawn, and its size seen when it is.</summary>
    private static void Frame(LayoutSettler s, Vector2 size)
    {
        s.Tick();
        s.Observe(size);
    }

    [Fact]
    public void ANewSettlerShowsTheWindow()
    {
        var s = new LayoutSettler();
        Check("nothing is hidden until the content changes", !s.Hidden);
        Frame(s, Old);
        Check("watching sizes does not hide anything by itself", !s.Hidden);
    }

    [Fact]
    public void AChangeHidesTheWindowUntilItsSizeSettles()
    {
        // The frames of a real Next press where both the height and the width change: the old size, the new height, then the new width.
        var s = new LayoutSettler(stableFramesNeeded: 1, minHiddenFrames: 1);
        s.Restart();
        Check("a change hides the window", s.Hidden);
        Frame(s, Old);
        Check("still hidden while it shows the old size", s.Hidden);
        Frame(s, TallerOnly);
        Check("still hidden when the size has just changed (the height)", s.Hidden);
        Frame(s, New);
        Check("still hidden when it changed again (the width)", s.Hidden);
        Frame(s, New);
        Check("shown again once the size is the same two frames in a row", !s.Hidden);
    }

    [Fact]
    public void AWidthThatCatchesUpALaterFrameDoesNotShowTheWindowTooEarly()
    {
        // The case the minimum guards: only the width changes, a frame late. The size looks settled for a frame (the same twice), and then changes.
        var s = new LayoutSettler(stableFramesNeeded: 1, minHiddenFrames: 3);
        s.Restart();
        Frame(s, Old);
        Frame(s, Old);                         // looks settled, but the real change is still to come
        Check("the size looking settled is not enough before the minimum number of frames", s.Hidden);
        Frame(s, New);                         // the width arrives
        Check("and the window is still hidden when the width arrives", s.Hidden);
        Frame(s, New);
        Check("it is shown once the real size has settled", !s.Hidden);
    }

    [Fact]
    public void AChangeThatDoesNotResizeIsShownAfterTheMinimumFrames()
    {
        var s = new LayoutSettler();           // the defaults: one stable frame, at least three hidden
        s.Restart();
        Frame(s, Old);
        Frame(s, Old);
        Check("hidden for the first two frames", s.Hidden);
        Frame(s, Old);
        Check("shown after the third, which has the same size", !s.Hidden);
    }

    [Fact]
    public void AQuickRestartHidesOnlyTheOneStaleFrame()
    {
        // What the overlay does when what it shows changes: the window size is stale for exactly one frame (measured in game: for example 484x297
        // for one frame, then 347x133 from the next on), so that one frame is hidden, and the size is not waited for.
        var s = new LayoutSettler();
        s.Restart(minHiddenFrames: 1, stableFramesNeeded: 0);
        Check("hidden for the stale frame", s.Hidden);
        Frame(s, Old);
        Check("shown from the very next frame, without waiting for the size to repeat", !s.Hidden);
    }

    [Fact]
    public void ASlowerRestartWaitsLongerAndForTheSize()
    {
        // Opening the overlay: ImGui does extra work in the first frames, so it waits at least three, and for the size to repeat.
        var s = new LayoutSettler();
        s.Restart(minHiddenFrames: 3, stableFramesNeeded: 1);
        Frame(s, Old);
        Frame(s, Old);
        Check("not shown after two frames", s.Hidden);
        Frame(s, Old);
        Check("shown after the third, which repeats the size", !s.Hidden);
    }

    [Fact]
    public void ASizeThatKeepsChangingIsNeverHiddenForLong()
    {
        var s = new LayoutSettler(stableFramesNeeded: 1, minHiddenFrames: 3, maxHiddenFrames: 12);
        s.Restart();
        var frames = 0;
        while (s.Hidden && frames < 100)
        {
            Frame(s, new Vector2(300 + frames, 200));     // a different size every frame
            frames++;
        }
        Check("the safety limit shows the window anyway", !s.Hidden);
        Check("after exactly the most frames allowed", frames == 12, frames.ToString());
    }

    [Fact]
    public void AWindowThatIsNeverDrawnIsStillShownEventually()
    {
        // The bug this guards against: ImGui does not draw a window at all while its style alpha is exactly 0, so Observe is never reached
        // and the overlay stayed hidden for ever.
        var s = new LayoutSettler(stableFramesNeeded: 1, minHiddenFrames: 3, maxHiddenFrames: 12);
        s.Restart();
        var frames = 0;
        while (s.Hidden && frames < 100)
        {
            s.Tick();            // Observe is never called
            frames++;
        }
        Check("a window that never reaches its draw code is shown again by the safety limit", !s.Hidden);
        Check("after exactly the most frames allowed", frames == 12, frames.ToString());
    }

    [Fact]
    public void ACancelShowsTheWindowAtOnce()
    {
        var s = new LayoutSettler();
        s.Restart();
        s.Cancel();
        Check("cancelling shows it straight away", !s.Hidden);
        Frame(s, Old);
        Check("and it stays shown", !s.Hidden);
    }

    [Fact]
    public void ARestartStartsAFreshCount()
    {
        var s = new LayoutSettler(stableFramesNeeded: 1, minHiddenFrames: 2);
        s.Restart();
        Frame(s, Old);
        Frame(s, Old);                        // settled
        Check("settled", !s.Hidden);

        s.Restart();                          // a second change
        Frame(s, Old);
        Check("a second change hides it again, with its own count", s.Hidden);
        Frame(s, New);
        Check("a size change in the second round keeps it hidden", s.Hidden);
        Frame(s, New);
        Check("and it is shown after the second round settles", !s.Hidden);
    }

    [Fact]
    public void MoreStableFramesCanBeRequired()
    {
        var s = new LayoutSettler(stableFramesNeeded: 3, minHiddenFrames: 1);
        s.Restart();
        Frame(s, New);
        Frame(s, New);
        Frame(s, New);
        Check("two equal frames in a row are not enough when three are needed", s.Hidden);
        Frame(s, New);
        Check("three are", !s.Hidden);
    }
}