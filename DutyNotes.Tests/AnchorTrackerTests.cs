using System.Numerics;

namespace DutyNotes.Tests;

/// <summary>The overlay keeps its chosen anchor point where it was while it changes size, however the size arrives, and however often it changes back.</summary>
public sealed class AnchorTrackerTests : CheckBase
{
    // ImGui keeps a window's position as whole pixels, so a position that is set comes back cut.
    private static Vector2 Imgui(Vector2 set) => new(MathF.Truncate(set.X), MathF.Truncate(set.Y));

    private static Vector2 Corner(Vector2 pos, Vector2 size, int index) => pos + size * AnchorTracker.Fraction(index);

    private static bool Near(Vector2 a, Vector2 b) => MathF.Abs(a.X - b.X) <= 1f && MathF.Abs(a.Y - b.Y) <= 1f;

    [Fact]
    public void TheAnchorStaysPutThroughGrowingAndShrinkingAgain()
    {
        foreach (var index in new[] { 0, 2, 4, 5, 6, 8 })
        {
            var tracker = new AnchorTracker();
            var size = new Vector2(310.6f, 125.3f);
            var pos = new Vector2(1000f, 600f);
            tracker.Hold(pos, size, index);
            var anchor = Corner(pos, size, index);

            // Whole frames of a toggle each way, with sizes that are not whole pixels: first the old size for a frame (the new content
            // in the old frame), then the final size, and a step on the way in the second case, as the table's columns catch up.
            var sizes = new[]
            {
                size, new Vector2(520.3f, 260.7f), new Vector2(520.3f, 260.7f),
                new Vector2(520.3f, 260.7f), new Vector2(402.9f, 260.7f), new Vector2(402.9f, 130.2f),
                new Vector2(310.6f, 125.3f), new Vector2(310.6f, 125.3f), new Vector2(877.7f, 402.4f), new Vector2(310.6f, 125.3f),
            };
            var worst = 0f;
            foreach (var s in sizes)
            {
                pos = Imgui(tracker.Place(pos, s, index, false));
                var offset = Corner(pos, s, index) - anchor;
                worst = Math.Max(worst, Math.Max(MathF.Abs(offset.X), MathF.Abs(offset.Y)));
            }
            Check($"anchor {index}: the anchor point stays within a pixel through every size change", worst <= 1f, $"worst {worst}");
        }
    }

    [Fact]
    public void TheTopLeftAnchorNeverMovesTheWindow()
    {
        var tracker = new AnchorTracker();
        var pos = new Vector2(120f, 80f);
        tracker.Hold(pos, new Vector2(200, 100), 0);
        var moved = false;
        foreach (var size in new[] { new Vector2(500, 300), new Vector2(210.5f, 99.5f), new Vector2(900, 700) })
            moved |= tracker.Place(pos, size, 0, false) != pos;
        Check("the corner that does not move with a size change keeps the window where it is", !moved);
    }

    [Fact]
    public void DraggingTheWindowMovesTheAnchorWithIt()
    {
        var tracker = new AnchorTracker();
        var size = new Vector2(400, 200);
        var pos = new Vector2(1000, 600);
        tracker.Hold(pos, size, 8);
        pos = Imgui(tracker.Place(pos, size, 8, false));

        // The user drags it somewhere else: it is left there, and the anchor is now at its new bottom-right.
        pos = new Vector2(700, 300);
        var afterDrag = tracker.Place(pos, size, 8, true);
        Check("a dragged window is left where it was dropped", afterDrag == pos);
        var anchor = Corner(pos, size, 8);

        var bigger = new Vector2(600, 350);
        pos = Imgui(tracker.Place(pos, bigger, 8, false));
        Check("the next size change keeps the new anchor point", Near(Corner(pos, bigger, 8), anchor));
    }

    [Fact]
    public void ChangingTheAnchorSettingDoesNotMoveTheWindow()
    {
        var tracker = new AnchorTracker();
        var size = new Vector2(400, 200);
        var pos = new Vector2(500, 400);
        tracker.Hold(pos, size, 8);
        Check("choosing another anchor leaves the window where it is", tracker.Place(pos, size, 0, false) == pos);

        var bigger = new Vector2(700, 500);
        Check("and the new anchor is the one kept from then on", tracker.Place(pos, bigger, 0, false) == pos);
    }

    [Fact]
    public void HoldingRemembersTheWindowWithoutMovingIt()
    {
        var tracker = new AnchorTracker();
        var pos = new Vector2(640f, 360f);
        Check("a window that is being held is not moved", tracker.Hold(pos, new Vector2(10, 10), 8) == pos);

        // The window then settles at its real size, and is placed by that.
        var real = new Vector2(480f, 240f);
        var held = tracker.Hold(pos, real, 8);
        var next = tracker.Place(held, new Vector2(480f, 240f), 8, false);
        Check("once it has settled, the same size leaves it where it is", Near(next, pos));

        var fresh = new AnchorTracker();
        Check("a tracker that has seen nothing takes the window as it is", fresh.Place(pos, real, 4, false) == pos);
        fresh.Reset();
        Check("after a reset the next frame is taken as it is again", fresh.Place(new Vector2(5, 6), real, 4, false) == new Vector2(5, 6));
    }

    [Fact]
    public void AWindowPushedBackOntoTheScreenByImguiGoesBackToItsAnchor()
    {
        // What the game's log showed: bottom-left anchor, the full encounter makes the window 1434 high, taller than the screen, so its top is
        // above the screen. Switched off again, the window is small, and ImGui moves it so that part of it is on the screen (no mouse button held).
        var tracker = new AnchorTracker();
        var small = new Vector2(288, 256);
        var tall = new Vector2(718, 1434);
        var pos = new Vector2(107, 755);
        tracker.Hold(pos, small, 6);
        var anchor = Corner(pos, small, 6);

        pos = Imgui(tracker.Place(pos, tall, 6, false));
        Check("the tall window goes up from the bottom-left anchor", Near(Corner(pos, tall, 6), anchor) && pos.Y < 0);

        // ImGui keeps 19 pixels of the window on the screen when it is smaller than where it sits.
        pos = new Vector2(pos.X, 19 - small.Y);
        pos = Imgui(tracker.Place(pos, small, 6, false));
        Check("after ImGui moved it, the window is put back so the anchor is where it was", Near(Corner(pos, small, 6), anchor));
        Check("and it is fully on the screen", pos.Y > 0 && pos.Y + small.Y < 1080);
    }
}
