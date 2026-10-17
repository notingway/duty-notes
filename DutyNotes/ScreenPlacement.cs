using System.Numerics;

namespace DutyNotes;

/// <summary>
/// Keeps a window where it can be reached. A window that was left (or saved) partly off the screen, for example after a change of
/// resolution or a monitor, would otherwise come back where it can't be seen or grabbed. Pure (no game code), so it can be tested.
/// </summary>
internal static class ScreenPlacement
{
    /// <summary>
    /// Moves a window just far enough that its title bar is on the screen and at least <paramref name="minVisible"/> pixels of it are
    /// visible in each direction. A window that is already reachable is returned unchanged.
    /// </summary>
    /// <param name="pos">The window's top-left corner.</param>
    /// <param name="size">The window's size.</param>
    /// <param name="screenPos">The top-left corner of the screen (or viewport) the window is on.</param>
    /// <param name="screenSize">The size of that screen. Zero or negative means unknown, and nothing is moved.</param>
    public static Vector2 KeepOnScreen(Vector2 pos, Vector2 size, Vector2 screenPos, Vector2 screenSize, float minVisible = 80f)
    {
        if (screenSize.X <= 0 || screenSize.Y <= 0) return pos;

        var visibleX = Math.Min(minVisible, Math.Max(size.X, 1f));
        var visibleY = Math.Min(minVisible, Math.Max(size.Y, 1f));

        // Horizontally, the window may hang off either side, as long as visibleX of it stays in.
        var minX = screenPos.X - (size.X - visibleX);
        var maxX = screenPos.X + screenSize.X - visibleX;

        // Vertically, the top edge (the title bar) must not be above the screen, and visibleY of it must stay above the bottom.
        var minY = screenPos.Y;
        var maxY = screenPos.Y + screenSize.Y - visibleY;

        return new Vector2(Math.Clamp(pos.X, minX, Math.Max(minX, maxX)), Math.Clamp(pos.Y, minY, Math.Max(minY, maxY)));
    }
}