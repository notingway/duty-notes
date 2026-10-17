using Dalamud.Interface.ManagedFontAtlas;

namespace DutyNotes;

/// <summary>
/// Fonts rendered at their real pixel size. Scaling one baked font (SetWindowFontScale) stretches its
/// bitmap and looks blurry, so each size used gets its own font handle. Handles are built asynchronously
/// and cached; the least recently used are disposed to keep the number of atlases small.
/// </summary>
public sealed class FontCache : IDisposable
{
    private const int MaxHandles = 12;

    private readonly Dictionary<int, Entry> fonts = new();
    private long clock;

    private sealed class Entry
    {
        public required IFontHandle Handle;
        public long LastUsed;
    }

    /// <summary>The font at this pixel size, or null while it is still being built.</summary>
    public IFontHandle? Get(float sizePx)
    {
        var key = Math.Max(1, (int)MathF.Round(sizePx));

        if (!fonts.TryGetValue(key, out var entry))
        {
            Evict();
            var handle = Plugin.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
                e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(key)));
            fonts[key] = entry = new Entry { Handle = handle };
        }

        entry.LastUsed = ++clock;
        return entry.Handle.Available ? entry.Handle : null;
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
}
