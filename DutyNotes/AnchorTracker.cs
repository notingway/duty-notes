using System.Numerics;

namespace DutyNotes;

/// <summary>
/// Keeps one point of an auto-sizing window (a corner, an edge's middle or its centre, chosen with the Anchor setting) at the same place
/// on the screen while the window changes size. It remembers where that point is, and each frame says where the window's top-left corner has
/// to be for the point to stay there. That holds however many frames the size takes to settle in, and it can't drift, which adding up the
/// changes from frame to frame did: ImGui keeps a window's position as whole pixels, so a position set from fractions of a pixel came back
/// slightly different, was taken for the window having been dragged, and the next size change was not corrected. Pure, so it can be tested.
/// </summary>
internal sealed class AnchorTracker
{
    private Vector2? anchor;
    private Vector2 expected;
    private int index = -1;

    /// <summary>Forgets where the anchor is: the next frame takes the window as it is.</summary>
    public void Reset() => anchor = null;

    /// <summary>Where the anchor sits in a window, as a fraction of its size: 0, 0.5 or 1 across, and the same down. Index 0 is the top-left, 8 the bottom-right.</summary>
    internal static Vector2 Fraction(int anchorIndex) => new(anchorIndex % 3 * 0.5f, anchorIndex / 3 * 0.5f);

    /// <summary>Takes the window as it is, without moving it (for frames while the window is still settling after opening).</summary>
    public Vector2 Hold(Vector2 pos, Vector2 size, int anchorIndex)
    {
        anchor = pos + size * Fraction(anchorIndex);
        expected = pos;
        index = anchorIndex;
        return pos;
    }

    /// <summary>
    /// Where the window's top-left corner should be this frame, given where it is and how big it is. A window that is not where it was put
    /// because the user is dragging it (<paramref name="mouseDown"/>), or a changed anchor setting, makes the window's current place the new
    /// one, and it stays where it is. A window that is somewhere else with no mouse button held was moved by ImGui itself, which pushes a window
    /// back onto the screen when it shrinks while mostly off it: that is not a choice of a new place, and the window goes back to the anchor.
    /// </summary>
    public Vector2 Place(Vector2 pos, Vector2 size, int anchorIndex, bool mouseDown)
    {
        var displaced = MathF.Abs(pos.X - expected.X) > 0.5f || MathF.Abs(pos.Y - expected.Y) > 0.5f;
        if (anchor is null || anchorIndex != index || (displaced && mouseDown)) return Hold(pos, size, anchorIndex);

        // ImGui keeps whole pixels, so the position is cut the same way here and then compared with what comes back.
        var target = anchor!.Value - size * Fraction(anchorIndex);
        target = new Vector2(MathF.Truncate(target.X), MathF.Truncate(target.Y));
        expected = target;
        return target;
    }
}
