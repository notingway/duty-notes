using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;

namespace DutyNotes;

/// <summary>
/// Fonts rendered at their real pixel size. Scaling one baked font (SetWindowFontScale) stretches its
/// bitmap and looks blurry, so each size used gets its own font handle. Handles are built asynchronously
/// and cached; the least recently used are disposed to keep the number of atlases small.
/// </summary>
/// <remarks>
/// Three things were learned from a player whose text scaling stopped at about 1.2 to 1.4 and then fell back to the default size for good:
/// <list type="bullet">
/// <item>The size handed to Dalamud is in unscaled units, and Dalamud applies its global UI scale on top when it draws. A size measured from
/// ImGui (which already includes that scale) therefore has to be divided by it, or the text comes out that many times too big, and the big
/// font may not be buildable at all.</item>
/// <item>A font that fails to build was kept for good and failed again on every frame, so the text stayed at the default size. A failed size is
/// now dropped and tried again after a while, and in the meantime the text is scaled the old blurry way, so that it still follows the slider.</item>
/// <item>One font per whole pixel meant that dragging a slider made dozens. Sizes are rounded to steps, and fewer fonts are kept.</item>
/// </list>
/// </remarks>
public sealed class FontCache : IDisposable
{
    private const int MaxHandles = 6;
    private const int StepPx = 2;
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromSeconds(15);

    private readonly Dictionary<int, Entry> fonts = new();
    private readonly Dictionary<int, DateTime> failedUntil = new();
    private long clock;

    private sealed class Entry
    {
        public required IFontHandle Handle;
        public long LastUsed;
        public bool Reported;
    }

    /// <summary>
    /// Makes text drawn from now until the result is disposed use <paramref name="sizePx"/> (in drawn pixels, so measured from
    /// <c>ImGui.GetFontSize()</c>): the font of that size when it is ready, and the current font stretched to it while it is not.
    /// </summary>
    public IDisposable Use(float sizePx, float baseSizePx)
    {
        var key = Math.Max(StepPx, (int)MathF.Round(sizePx / StepPx) * StepPx);
        if (GetReady(key) is { } entry)
        {
            var pushed = entry.Handle.Push();
            if (!entry.Reported)
            {
                entry.Reported = true;
                Plugin.Log.Information("Font {Key}px is ready: drawn at {Drawn}px (global scale {Scale}, base size {Base}px)",
                                       key, ImGui.GetFontSize(), ImGuiHelpers.GlobalScale, baseSizePx);
            }
            return pushed;
        }

        // Not ready (still building, or it failed): the closest font that is ready, stretched to the size. Blurry, but it still follows the
        // slider, and while a slider is dragged the closest one is only a step away, so the stretch is small.
        var closest = ClosestReady(sizePx, baseSizePx);
        IDisposable? pushedClosest = null;
        var from = baseSizePx;
        if (closest is { } near)
        {
            pushedClosest = near.Entry.Handle.Push();
            from = near.Key;
        }

        var factor = from > 0f ? sizePx / from : 1f;
        if (MathF.Abs(factor - 1f) < 0.01f) return pushedClosest ?? NoScope.Instance;

        ImGui.SetWindowFontScale(factor);
        return new ResetScale(pushedClosest);
    }

    /// <summary>The font that is ready and nearest to this size, if it is nearer than the normal font; null means use the normal font.</summary>
    private (int Key, Entry Entry)? ClosestReady(float sizePx, float baseSizePx)
    {
        (int Key, Entry Entry)? best = null;
        var bestDistance = baseSizePx > 0f ? MathF.Abs(MathF.Log(sizePx / baseSizePx)) : float.MaxValue;
        foreach (var (key, entry) in fonts)
        {
            if (!entry.Handle.Available) continue;
            var distance = MathF.Abs(MathF.Log(sizePx / key));
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = (key, entry);
        }
        return best;
    }

    private Entry? GetReady(int key)
    {
        if (failedUntil.TryGetValue(key, out var until))
        {
            if (DateTime.UtcNow < until) return null;
            failedUntil.Remove(key);
        }

        if (!fonts.TryGetValue(key, out var entry))
        {
            Evict();
            var handle = Plugin.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
                e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(key / ImGuiHelpers.GlobalScale)));
            fonts[key] = entry = new Entry { Handle = handle };
        }

        entry.LastUsed = ++clock;

        if (entry.Handle.LoadException is { } ex)
        {
            Plugin.Log.Warning(ex, "The font at {Key}px could not be built; text at that size is stretched instead, and the font is tried again in a while", key);
            entry.Handle.Dispose();
            fonts.Remove(key);
            failedUntil[key] = DateTime.UtcNow + RetryAfterFailure;
            return null;
        }

        return entry.Handle.Available ? entry : null;
    }

    private void Evict()
    {
        while (fonts.Count >= MaxHandles)
        {
            var oldest = fonts.MinBy(kv => kv.Value.LastUsed);
            oldest.Value.Handle.Dispose();
            fonts.Remove(oldest.Key);
        }
    }

    public void Dispose()
    {
        foreach (var entry in fonts.Values) entry.Handle.Dispose();
        fonts.Clear();
    }

    private sealed class NoScope : IDisposable
    {
        public static readonly NoScope Instance = new();
        public void Dispose() { }
    }

    private sealed class ResetScale : IDisposable
    {
        private readonly IDisposable? pushedFont;
        public ResetScale(IDisposable? pushedFont) => this.pushedFont = pushedFont;

        public void Dispose()
        {
            pushedFont?.Dispose();
            ImGui.SetWindowFontScale(1f);
        }
    }
}
