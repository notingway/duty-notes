using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;

namespace DutyNotes;

public sealed class OverlayWindow : Window, IDisposable
{
    /// <summary>How much of the theme's header colour the phase title bars keep (1 = solid).</summary>
    private const float TitleBackgroundOpacity = 0.7f;

    private readonly Plugin plugin;

    public OverlayWindow(Plugin plugin) : base("Duty Notes###mitsheet-overlay")
    {
        this.plugin = plugin;
        Size = new Vector2(420, 240);
        SizeCondition = ImGuiCond.FirstUseEver;

        // Where it first appears, on a fresh install (a window that has been placed before keeps its own place).
        Position = new Vector2(80, 80);
        PositionCondition = ImGuiCond.FirstUseEver;

        // Dalamud closes a focused window when Escape is pressed. The overlay stays up during play, and Escape is
        // what you press for the game's own menus, so it must not close (and switch "Show overlay" off) by accident.
        RespectCloseHotkey = false;

        // Closing with the X button moved the overlay when it was opened again, while closing it from the editor's "Show overlay" tickbox did not.
        // In Dalamud's WindowHost, the X button is the path that draws a fade-out stand-in for the window, under the same ImGui window name,
        // without our flags and at a size Dalamud remembers; code closing the window never does. That was the suspected cause. The
        // problem is gone with this and the settle frames in KeepAnchorFixed together (the two were not tried apart), and a HUD has no use
        // for the fade anyway, so it is switched off for this window.
        DisableFadeInFadeOut = true;
    }

    public override void PreDraw()
    {
        var cfg = plugin.Config;
        BgAlpha = cfg.BackgroundAlpha;

        var flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        if (cfg.LockOverlay)
        {
            flags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoNav;

            // Locked means click-through, except while Ctrl is held: then the overlay takes the mouse so
            // Ctrl + right-click can open its menu. It still can't be moved or resized.
            if (!ImGui.GetIO().KeyCtrl) flags |= ImGuiWindowFlags.NoMouseInputs;
        }
        if (cfg.AutoSize) flags |= ImGuiWindowFlags.AlwaysAutoResize;
        Flags = flags;

        // An auto-sizing window catches up with new content a frame late (ImGui sizes it from the previous frame, and the table's columns lag the
        // same way), which showed as a flicker when stepping between phases. While it catches up, the whole window is hidden: see HiddenThisFrame.
        if (!cfg.AutoSize)
        {
            settler.Cancel();
        }
        else if (CurrentView(cfg) is var view && view != lastView)
        {
            lastView = view;

            // Measured in game: after the content changes, the window size is stale for exactly one frame (the new content in the old frame), and
            // final from the next. In that second frame the anchor adjustment (KeepAnchorFixed) moves the window, so it is hidden too: hide, swap,
            // scale, anchor, show. Opening the overlay is slower (ImGui hides a new window for a frame, and re-fits it over two), so it waits longer.
            if (opening) settler.Restart(OpeningHiddenFrames, 1);
            else settler.Restart(HiddenFramesPerChange, 0);

            opening = false;
        }

        HiddenThisFrame = settler.Hidden;

        // The style alpha below hides everything except the window background: Window.BgAlpha becomes SetNextWindowBgAlpha, and ImGui then
        // overwrites the background's alpha channel with that value and ignores the style alpha (imgui.cpp, RenderWindowDecorations). Left alone, the
        // dark panel stayed on screen while the window was "hidden", resizing and moving with it: that was the flicker. So the background alpha is
        // 0 for the hidden frames too. (That also stops Dalamud's blurred backdrop, which is only drawn for a window that has a background.)
        BgAlpha = HiddenThisFrame ? 0f : cfg.BackgroundAlpha;

        settler.Tick(); // counts this frame, and enforces the safety limit even if the window is not drawn
        if (HiddenThisFrame)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, HiddenAlpha);
            pushedAlpha = true;
        }
    }

    /// <summary>
    /// The style alpha of a window that is being kept off the screen. NOT 0: while the style alpha is exactly 0, ImGui does not draw the window
    /// at all (it never calls Draw, so the window does not even lay itself out), see "Don't render if style alpha is 0.0" in imgui.cpp.
    /// This is small enough that every colour rounds to fully transparent.
    /// </summary>
    internal const float HiddenAlpha = 0.001f;

    public override void PostDraw()
    {
        if (!pushedAlpha) return;

        ImGui.PopStyleVar();
        pushedAlpha = false;
    }

    private readonly LayoutSettler settler = new();
    private (Guid Encounter, Guid Phase, bool Full)? lastView;
    private bool pushedAlpha;
    private bool opening;
    private const int OpeningHiddenFrames = 3;
    private const int HiddenFramesPerChange = 2;

    /// <summary>True while this frame is drawn transparent, because the window is catching up with new content. The phase buttons follow it.</summary>
    internal bool HiddenThisFrame { get; private set; }

    /// <summary>What the overlay is showing, as ids: when it changes, the content (and so the size) is about to change.</summary>
    private (Guid Encounter, Guid Phase, bool Full) CurrentView(Configuration cfg)
    {
        if (plugin.EditorOpen)
        {
            // Editing: the overlay shows the editor's selection.
            if (cfg.Selected is not { } selected) return (Guid.Empty, Guid.Empty, cfg.ShowFullEncounter);

            SheetNode? owner = null;
            foreach (var root in cfg.Roots)
            {
                if (root != selected && !root.Children.Contains(selected)) continue;
                owner = root;
                break;
            }

            // An encounter has no rows of its own: with it selected, the overlay shows its first phase (or all of them, with the full encounter on).
            var phase = cfg.ShowFullEncounter ? Guid.Empty : owner == selected ? owner.Children.FirstOrDefault(c => c.Enabled)?.Id ?? Guid.Empty : selected.Id;
            return (owner?.Id ?? Guid.Empty, phase, cfg.ShowFullEncounter);
        }

        var encounter = cfg.ActiveEncounter;
        return (encounter?.Id ?? Guid.Empty, cfg.ShowFullEncounter ? Guid.Empty : cfg.ActivePhase?.Id ?? Guid.Empty, cfg.ShowFullEncounter);
    }

    /// <summary>Where the overlay was drawn last frame; valid only while <see cref="HasBounds"/> is true.</summary>
    internal (Vector2 Position, Vector2 Size) Bounds { get; private set; }
    internal bool HasBounds { get; private set; }

    public override void OnOpen()
    {
        lastView = null; // opening shows new content too: hide the first, settling frames
        opening = true;
        anchorTracker.Reset();
        settleFrames = SettleFramesAfterOpen;
        placeOnScreen = true;
    }

    // Each time the overlay opens, a position that is off the screen (saved by an older version, or left behind by a change of
    // resolution or monitor) is corrected on its first frame.
    private bool placeOnScreen;

    private void KeepWindowOnScreen()
    {
        placeOnScreen = false;

        var viewport = ImGui.GetWindowViewport();
        var pos = ImGui.GetWindowPos();
        var fixedPos = ScreenPlacement.KeepOnScreen(pos, ImGui.GetWindowSize(), viewport.Pos, viewport.Size);
        if (fixedPos == pos) return;

        ImGui.SetWindowPos(fixedPos);
        Plugin.Log.Information("The overlay was off the screen at {Old}, and was moved to {New}", pos, fixedPos);
    }

    public override void OnClose()
    {
        HasBounds = false;
        plugin.Config.OverlayVisible = false;
        plugin.Save();
    }

    // Remembers where the anchor point is, and says where the window has to be to keep it there while the window changes size.
    private readonly AnchorTracker anchorTracker = new();

    // When the window is opened again, ImGui re-fits an auto-sized window over its first couple of frames. That settling is not the content
    // growing, and moving the window to compensate for it threw it off the screen, so the first frames after opening are left alone.
    private const int SettleFramesAfterOpen = 4;
    private int settleFrames;

    /// <summary>
    /// When the auto-sized window changes size, put it where the chosen anchor point (e.g. the bottom-right corner) stays where it was. A window
    /// the user dragged is left alone, and its new place becomes the anchor. See <see cref="AnchorTracker"/>.
    /// </summary>
    private void KeepAnchorFixed(Configuration cfg)
    {
        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        var index = (int)cfg.Anchor;

        if (settleFrames > 0)
        {
            settleFrames--;
            anchorTracker.Hold(pos, size, index);
            return;
        }

        var target = anchorTracker.Place(pos, size, index, ImGui.IsMouseDown(ImGuiMouseButton.Left));
        if (target != pos) ImGui.SetWindowPos(target);
    }
    private readonly FontCache fonts = new();
    private float baseFontSize = 17f;

    public void Dispose() => fonts.Dispose();

    public override void Draw()
    {
        var cfg = plugin.Config;
        baseFontSize = ImGui.GetFontSize(); // the unscaled size, before any font is pushed

        if (placeOnScreen) KeepWindowOnScreen();

        settler.Observe(ImGui.GetWindowSize());

        if (cfg.AutoSize) KeepAnchorFixed(cfg);
        else anchorTracker.Reset();

        // The controls window sits right under the overlay and follows it, so it needs to know where it is.
        Bounds = (ImGui.GetWindowPos(), ImGui.GetWindowSize());
        HasBounds = true;

        DrawContextMenu(cfg);

        // Nothing to show or switch between yet.
        if (cfg.Roots.Count == 0)
        {
            ImGui.TextDisabled("No encounters yet. Use /dutynotes edit to add one.");
            return;
        }

        // While unlocked, offer a quick switcher so you don't need the editor mid-pull. It lists encounters
        // only; moving between phases is what the buttons and chat commands are for.
        if (!cfg.LockOverlay)
        {
            var active = cfg.ActiveEncounter;
            // A relative width would feed back into an auto-sizing window, so use a fixed one there.
            ImGui.SetNextItemWidth(cfg.AutoSize ? 240 : -1);
            if (ImGui.BeginCombo("##encounter", active?.Name ?? "Select encounter"))
            {
                foreach (var encounter in cfg.Roots)
                {
                    if (ImGui.Selectable($"{encounter.Name}##{encounter.Id}", encounter.Id == active?.Id))
                    {
                        cfg.SetActiveEncounter(encounter);

                        // The editor's selection is what the overlay shows while the editor is open,
                        // so keep it in step or the pick would seem to do nothing.
                        if (plugin.EditorOpen) cfg.SelectedId = encounter.Id;
                        plugin.Save();
                    }
                }
                ImGui.EndCombo();
            }
        }

        var sections = new List<SheetNode>();

        if (plugin.EditorOpen)
        {
            // Editing: show what is selected in the editor, the same way it shows while playing. With
            // "Show full encounter" on that is every phase of the encounter the selection belongs to. With it off it is
            // the selected phase, or the first phase if the encounter itself is selected (an encounter has no rows of its own).
            var selected = cfg.Selected;
            if (selected is null) return;

            var owner = cfg.Roots.FirstOrDefault(r => r == selected || r.Children.Contains(selected));
            if (cfg.ShowFullEncounter && owner is not null)
            {
                // A phase that is switched off stays out, unless it is the one being edited.
                sections.AddRange(owner.Children.Where(c => c.Enabled || c == selected));
            }
            else if (owner is not null && owner == selected)
            {
                if (owner.Children.FirstOrDefault(c => c.Enabled) is { } first) sections.Add(first);
            }
            else
            {
                sections.Add(selected);
            }
        }
        else
        {
            // Playing: follow the navigation. The setting decides between every phase of the encounter and just the phase you are on.
            var encounter = cfg.ActiveEncounter;
            if (encounter is null) return;

            var phase = cfg.ActivePhase;
            if (cfg.ShowFullEncounter) sections.AddRange(encounter.Children.Where(c => c.Enabled));
            else if (phase is not null) sections.Add(phase);
        }

        // A table with nothing to show (no rows, or none that are switched on) is skipped.
        sections.RemoveAll(n => !n.HasContent());

        if (sections.Count == 0)
        {
            ImGui.TextDisabled("No rows yet. Use /dutynotes edit to add some.");
            return;
        }

        // The Notes column is shared: it only exists if at least one visible row has a note. Note rows don't
        // count, their text runs across the whole table instead.
        var showNotes = sections.Any(n => n.Rows.Any(r => r.Enabled && !r.IsNote && !string.IsNullOrWhiteSpace(r.Notes)));
        DrawTable(sections, showNotes);
    }
    /// <summary>Right-click menu (Ctrl + right-click while locked) with the switches you reach for most.</summary>
    private void DrawContextMenu(Configuration cfg)
    {
        if (!ImGui.BeginPopupContextWindow("##overlay-menu")) return;

        if (ImGui.MenuItem("Lock overlay", string.Empty, cfg.LockOverlay))
        {
            cfg.LockOverlay = !cfg.LockOverlay;
            plugin.Save();
        }

        if (ImGui.MenuItem("Show full encounter", string.Empty, cfg.ShowFullEncounter))
        {
            cfg.ShowFullEncounter = !cfg.ShowFullEncounter;
            plugin.Save();
        }

        // The buttons only exist in single-phase mode, so the item is greyed out (but keeps its tick) in full mode.
        if (ImGui.MenuItem("Show phase buttons", string.Empty, cfg.ShowControls, !cfg.ShowFullEncounter))
        {
            cfg.ShowControls = !cfg.ShowControls;
            plugin.Save();
        }
        if (cfg.ShowFullEncounter && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Hidden while the whole encounter is showing.\nUntick \"Show full encounter\" to use them.");

        ImGui.Separator();

        if (ImGui.MenuItem("Open editor")) plugin.OpenEditor();

        // Hiding the overlay takes the menu with it; bring it back with /dutynotes or from the plugin list.
        if (ImGui.MenuItem("Hide overlay")) plugin.SetOverlayVisible(false);

        ImGui.EndPopup();
    }

    private void ScaledText(string text, float scale, Vector4? color = null, bool wrap = true)
    {
        // Use a font built at the real size instead of stretching the default one. While that font is
        // still being built (first frames at a new size) the text is drawn at the normal size.
        var size = Math.Clamp(scale, Configuration.MinScale, Configuration.MaxScale) * baseFontSize;
        using var font = fonts.Use(size, baseFontSize);

        if (color is { } c) ImGui.PushStyleColor(ImGuiCol.Text, c);

        if (!wrap)
        {
            ImGui.TextUnformatted(text);
        }
        else if (plugin.Config.AutoSize)
        {
            // An auto-sized window has no fixed width to wrap at, so wrap at an explicit maximum instead.
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + Math.Max(40f, plugin.Config.MaxTextWidth));
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
        }
        else
        {
            ImGui.TextWrapped(text);
        }

        if (color is not null) ImGui.PopStyleColor();
    }

    /// <summary>
    /// All sections go in one table, each introduced by a title row. Sharing one table is what keeps the
    /// columns the same width across every phase: they fit the widest content of any section (when
    /// auto-sizing), and a single visible section simply fits itself.
    /// </summary>
    private void DrawTable(List<SheetNode> sections, bool showNotes)
    {
        var cfg = plugin.Config;
        var columns = showNotes ? 3 : 2;

        // Auto-sized: columns fit their content. Otherwise they share the window width.
        var sizing = cfg.AutoSize ? ImGuiTableFlags.SizingFixedFit : ImGuiTableFlags.SizingStretchProp;
        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.PadOuterX | sizing;

        // Horizontal cell padding is the gap between columns.
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(Math.Clamp(cfg.ColumnPadding, 0f, 60f), 3f));
        if (!ImGui.BeginTable("##mitsheet-table", columns, tableFlags))
        {
            ImGui.PopStyleVar();
            return;
        }

        var colFlags = cfg.AutoSize ? ImGuiTableColumnFlags.WidthFixed : ImGuiTableColumnFlags.WidthStretch;
        ImGui.TableSetupColumn("Mechanic", colFlags, cfg.AutoSize ? 0f : 1.2f);
        ImGui.TableSetupColumn("Actions", colFlags, cfg.AutoSize ? 0f : 1.5f);
        if (showNotes) ImGui.TableSetupColumn("Notes", colFlags, cfg.AutoSize ? 0f : 1.5f);

        var iconSize = new Vector2(Math.Clamp(cfg.IconSize, Configuration.MinIcon, Configuration.MaxIcon));
        foreach (var sheet in sections)
        {
            // Title row, in the first column.
            ImGui.TableNextRow();

            // The theme's header colour, a little see-through so the bar doesn't look so solid.
            var titleBg = ImGui.GetStyle().Colors[(int)ImGuiCol.TableHeaderBg];
            titleBg.W *= TitleBackgroundOpacity;
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(titleBg));
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ScaledText(sheet.Name, cfg.TitleScale, new Vector4(1f, 0.85f, 0.45f, 1f), wrap: false);


            // Column headers go under each phase title, so every table has its own.
            if (cfg.ShowHeaders)
            {
                // Full-strength text on a dimmer background than the title row.
                ImGui.TableNextRow();
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.4f)));
                var widths = CellWidths(columns);
                var labels = new[] { "Mechanic", "Actions", "Notes" };
                var blocks = Enumerable.Range(0, columns).Select(i => Measure(labels[i], 1f, widths[i])).ToArray();
                var headerHeight = blocks.Max(b => BlockHeight(b, 0f));
                for (var i = 0; i < columns; i++)
                {
                    ImGui.TableSetColumnIndex(i);
                    DrawTextCell(blocks[i], 1f, null, widths[i], headerHeight, HAlignOf(i), VAlign.Top, 0f);
                }
            }

            foreach (var row in sheet.Rows.Where(r => r.Enabled))
            {
                // A note row is free text across the whole table. An empty one still takes a line: only the row's tick decides whether it shows.
                if (row.IsNote)
                {
                    SpanningTextRow(row.Text, columns);
                    continue;
                }

                ImGui.TableNextRow();

                // The row is as tall as its tallest cell, so that has to be known before any cell is drawn:
                // a shorter cell is then placed inside that height by its column's up-and-down alignment.
                var widths = CellWidths(columns);
                var framePad = ImGui.GetStyle().FramePadding.Y;
                float WrapAt(int column) => cfg.AutoSize ? cfg.MaxTextWidth : widths[column];

                var mechanic = Measure(row.Mechanic, cfg.MechanicScale, WrapAt(0));
                var notes = showNotes ? Measure(row.Notes, cfg.NotesScale, WrapAt(2)) : null;
                var shown = row.Actions.Where(id => plugin.Catalog.Get(id) is not null).ToList();

                var rowHeight = Math.Max(BlockHeight(mechanic, framePad), shown.Count > 0 ? iconSize.Y : 0f);
                if (notes is not null) rowHeight = Math.Max(rowHeight, BlockHeight(notes, framePad));

                ImGui.TableSetColumnIndex(0);
                DrawTextCell(mechanic, cfg.MechanicScale, null, widths[0], rowHeight, HAlignOf(0), VAlignOf(0), framePad);

                ImGui.TableSetColumnIndex(1);
                DrawIcons(shown, iconSize, widths[1], rowHeight, HAlignOf(1), VAlignOf(1));

                if (notes is not null)
                {
                    ImGui.TableSetColumnIndex(2);
                    DrawTextCell(notes, cfg.NotesScale, null, widths[2], rowHeight, HAlignOf(2), VAlignOf(2), framePad);
                }
            }

        }

        ImGui.EndTable();
        ImGui.PopStyleVar();
    }

    /// <summary>
    /// A row of free text running across the whole table. ImGui tables have no merged cells, so the height is
    /// reserved with an empty item (zero wide, so it can't stretch the first column) and the text is drawn
    /// straight onto the draw list, wrapped at the table's width and clipped to the full row.
    /// </summary>
    private void SpanningTextRow(string text, int columns)
    {
        var scale = Math.Clamp(plugin.Config.NoteRowScale, Configuration.MinScale, Configuration.MaxScale);
        using var font = fonts.Use(scale * baseFontSize, baseFontSize);

        ImGui.TableNextRow();

        // The table's right edge is where its last column ends.
        ImGui.TableSetColumnIndex(columns - 1);
        var right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;

        ImGui.TableSetColumnIndex(0);
        var origin = ImGui.GetCursorScreenPos();
        var lines = WrapText(text, Math.Max(40f, right - origin.X));
        var lineHeight = ImGui.GetTextLineHeight();
        var height = lines.Count * lineHeight;

        ImGui.Dummy(new Vector2(0, height));

        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(origin, new Vector2(right, origin.Y + height), false);
        var color = ImGui.GetColorU32(ImGuiCol.Text);
        for (var i = 0; i < lines.Count; i++)
            draw.AddText(new Vector2(origin.X, origin.Y + i * lineHeight), color, lines[i]);
        draw.PopClipRect();
    }

    private static List<string> WrapText(string text, float width)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r", string.Empty).Split('\n'))
        {
            var line = string.Empty;
            foreach (var word in paragraph.Split(' '))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && ImGui.CalcTextSize(candidate).X > width)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }
            lines.Add(line);
        }
        return lines;
    }

    private HAlign HAlignOf(int column) => column switch
    {
        0 => plugin.Config.MechanicHAlign,
        1 => plugin.Config.MitigationHAlign,
        _ => plugin.Config.NotesHAlign,
    };

    private VAlign VAlignOf(int column) => column switch
    {
        0 => plugin.Config.MechanicVAlign,
        1 => plugin.Config.MitigationVAlign,
        _ => plugin.Config.NotesVAlign,
    };

    /// <summary>The width available inside each column's cell on the current row (read before drawing, so text can be wrapped and aligned).</summary>
    private static float[] CellWidths(int columns)
    {
        var widths = new float[columns];
        for (var i = 0; i < columns; i++)
        {
            ImGui.TableSetColumnIndex(i);
            widths[i] = ImGui.GetContentRegionAvail().X;
        }
        return widths;
    }

    /// <summary>Text broken into lines for a width, measured at its size. Drawn later by <see cref="DrawTextCell"/>.</summary>
    private sealed record TextBlock(List<string> Lines, float LineHeight, float Widest);

    private TextBlock Measure(string text, float scale, float wrapWidth)
    {
        var size = Math.Clamp(scale, Configuration.MinScale, Configuration.MaxScale) * baseFontSize;
        using var font = fonts.Use(size, baseFontSize);

        var lines = string.IsNullOrEmpty(text) ? new List<string>() : WrapText(text, Math.Max(40f, wrapWidth));
        var widest = lines.Count == 0 ? 0f : lines.Max(l => ImGui.CalcTextSize(l).X);
        return new TextBlock(lines, ImGui.GetTextLineHeight(), widest);
    }

    /// <summary>The height a text cell takes: its lines (at least one, so an empty cell keeps the usual row height) plus padding above and below.</summary>
    private static float BlockHeight(TextBlock block, float padding) => Math.Max(1, block.Lines.Count) * block.LineHeight + 2 * padding;

    /// <summary>
    /// Draws measured text in a cell: each line is placed across the cell by <paramref name="h"/>, and the whole block is
    /// placed within the row's height by <paramref name="v"/>. Drawn on the draw list, with an empty item reserving the size.
    /// </summary>
    private void DrawTextCell(TextBlock block, float scale, Vector4? color, float cellWidth, float rowHeight, HAlign h, VAlign v, float padding)
    {
        var size = Math.Clamp(scale, Configuration.MinScale, Configuration.MaxScale) * baseFontSize;
        using var font = fonts.Use(size, baseFontSize);

        var origin = ImGui.GetCursorScreenPos();
        var height = BlockHeight(block, padding);
        var top = Alignment.Offset(rowHeight, height, Alignment.Factor(v));

        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + top));
        ImGui.Dummy(new Vector2(block.Widest, height));

        var draw = ImGui.GetWindowDrawList();
        var textColor = color is { } c ? ImGui.GetColorU32(c) : ImGui.GetColorU32(ImGuiCol.Text);
        for (var i = 0; i < block.Lines.Count; i++)
        {
            var left = Alignment.Offset(cellWidth, ImGui.CalcTextSize(block.Lines[i]).X, Alignment.Factor(h));
            draw.AddText(new Vector2(origin.X + left, origin.Y + top + padding + i * block.LineHeight), textColor, block.Lines[i]);
        }
    }

    private void DrawIcons(List<uint> actions, Vector2 size, float cellWidth, float rowHeight, HAlign h, VAlign v)
    {
        if (actions.Count == 0) return;

        var origin = ImGui.GetCursorScreenPos();
        var totalWidth = actions.Count * size.X + (actions.Count - 1) * ImGui.GetStyle().ItemSpacing.X;
        ImGui.SetCursorScreenPos(new Vector2(
            origin.X + Alignment.Offset(cellWidth, totalWidth, Alignment.Factor(h)),
            origin.Y + Alignment.Offset(rowHeight, size.Y, Alignment.Factor(v))));

        var first = true;
        foreach (var id in actions)
        {
            var info = plugin.Catalog.Get(id);
            if (info is null) continue;

            if (!first) ImGui.SameLine();
            first = false;

            var wrap = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(info.Icon)).GetWrapOrEmpty();
            ImGui.Image(wrap.Handle, size);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(info.Name);
        }
    }
}