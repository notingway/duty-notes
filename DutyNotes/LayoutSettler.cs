using System.Numerics;

namespace DutyNotes;

/// <summary>
/// Decides when an auto-sizing window has caught up with new content, so the in-between frames can be kept off the screen.
///
/// ImGui sizes an auto-fit window from what was drawn in the PREVIOUS frame, and a table's column widths lag the same way. So when the
/// content changes (the next phase, another encounter), the first frame shows the new content in the old frame, and the window then
/// resizes, a frame or two later. That reads as flicker. While it settles the window is drawn fully transparent, and it is shown again
/// once its size has stopped changing. Pure (no game code), so it can be tested.
/// </summary>
internal sealed class LayoutSettler
{
    private readonly int defaultStableFramesNeeded;
    private readonly int defaultMinHiddenFrames;
    private readonly int maxHiddenFrames;
    private int stableFramesNeeded;
    private int minHiddenFrames;
    private Vector2? lastSize;
    private int stableFrames;
    private int hiddenFrames;

    /// <param name="stableFramesNeeded">How many frames in a row the size must be unchanged before the window is shown again.</param>
    /// <param name="minHiddenFrames">The fewest frames the window is hidden. The height of an auto-fit window catches up a frame after the content and its
    /// width (from the table's columns) can catch up a frame after that, so for a frame in between the size can look settled when it is not.</param>
    /// <param name="maxHiddenFrames">The most frames the window is ever kept hidden, whatever its size does or whether it is drawn (a safety limit).</param>
    public LayoutSettler(int stableFramesNeeded = 1, int minHiddenFrames = 3, int maxHiddenFrames = 12)
    {
        defaultStableFramesNeeded = this.stableFramesNeeded = stableFramesNeeded;
        defaultMinHiddenFrames = this.minHiddenFrames = minHiddenFrames;
        this.maxHiddenFrames = maxHiddenFrames;
    }

    /// <summary>True while the window should be kept off the screen.</summary>
    public bool Hidden { get; private set; }

    /// <summary>The content changed: hide the window until it has settled, with the waits given to the constructor.</summary>
    public void Restart() => Restart(defaultMinHiddenFrames, defaultStableFramesNeeded);

    /// <summary>
    /// The content changed: hide the window until it has settled, for at least <paramref name="minHiddenFrames"/> frames and until its size has been
    /// the same for <paramref name="stableFramesNeeded"/> frames in a row (0 means the size is not checked).
    /// </summary>
    public void Restart(int minHiddenFrames, int stableFramesNeeded)
    {
        this.minHiddenFrames = minHiddenFrames;
        this.stableFramesNeeded = stableFramesNeeded;
        Hidden = true;
        lastSize = null;
        stableFrames = 0;
        hiddenFrames = 0;
    }

    /// <summary>Show the window now (for a window that does not resize with its content).</summary>
    public void Cancel() => Hidden = false;

    /// <summary>
    /// Call once per frame BEFORE the window is drawn, after reading <see cref="Hidden"/> for that frame. It counts the hidden frame and enforces the
    /// safety limit, even if the window is never drawn (ImGui does not draw a window at all while its style alpha is exactly 0).
    /// </summary>
    public void Tick()
    {
        if (!Hidden) return;

        hiddenFrames++;
        if (hiddenFrames >= maxHiddenFrames) Hidden = false;
    }

    // Observe is what ends a wait that does not look at the size (stableFramesNeeded 0), once the minimum number of frames has been hidden.

    /// <summary>Call once per frame, with the window's size in that frame, when it is drawn. It decides whether the next frame can be shown.</summary>
    public void Observe(Vector2 size)
    {
        if (!Hidden) return;

        stableFrames = lastSize is { } previous && previous == size ? stableFrames + 1 : 0;
        lastSize = size;

        if (stableFrames >= stableFramesNeeded && hiddenFrames >= minHiddenFrames) Hidden = false;
    }

}