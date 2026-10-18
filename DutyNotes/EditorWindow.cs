using System.Numerics;
using System.Runtime.CompilerServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace DutyNotes;

public sealed class EditorWindow : Window
{
    private const int MaxPickerResults = 150;
    private const int MaxLevel = 100;

    private readonly Plugin plugin;
    private readonly HelpTab help = new();
    private string filter = string.Empty;
    private int pickerRow = -1;
    private bool dirty;

    public EditorWindow(Plugin plugin) : base("Duty Notes - Editor###mitsheet-editor")
    {
        this.plugin = plugin;
        Size = new Vector2(820, 480);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    /// <summary>Closing the editor writes any edit that was still waiting for its quiet moment.</summary>
    public override void OnClose() => plugin.FlushPendingSave();

    public override void Draw()
    {
        var cfg = plugin.Config;
        dirty = false;

        DrawQuickToggles(cfg);

        // Editing sheets and tweaking the overlay's look are separate jobs, so they get a tab each
        // instead of sharing (and squeezing) one window.
        if (ImGui.BeginTabBar("##editor-tabs"))
        {
            if (ImGui.BeginTabItem("Sheets"))
            {
                DrawSheetsTab(cfg);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Settings"))
            {
                DrawSettingsTab(cfg);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Help"))
            {
                help.Draw();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }


        // Selecting something in the tree also moves the overlay's navigation there, so closing the editor
        // leaves it on the encounter (and phase) you were working on. The first frame only records the selection.
        if (lastSelectedId is { } previous && previous != cfg.SelectedId)
        {
            cfg.SetActiveFromSelection();
            dirty = true;
        }
        lastSelectedId = cfg.SelectedId;

        // A change made while an item is being dragged or typed into arrives every frame, so it waits for a quiet moment. A click is a
        // single deliberate change, and is saved at once.
        if (dirty)
        {
            if (ImGui.IsAnyItemActive()) plugin.SaveSoon();
            else plugin.Save();
        }
    }

    private Guid? lastSelectedId;

    private void DrawSheetsTab(Configuration cfg)
    {
        if (ImGui.BeginChild("##tree", new Vector2(220 * ImGuiHelpers.GlobalScale, 0), true))
            DrawTreePane(cfg);
        ImGui.EndChild();

        ImGui.SameLine();

        if (ImGui.BeginChild("##sheet", new Vector2(0, 0)))
        {
            if (cfg.Selected is { } node) DrawSheetPane(cfg, node);
            else if (cfg.Roots.Count == 0)
            {
                ImGui.TextUnformatted("No encounters yet.");
                ImGui.TextDisabled("Add one with + Encounter on the left, or paste one with Import.");
            }
            else ImGui.TextDisabled("Select a sheet on the left.");
        }
        ImGui.EndChild();
    }

    /// <summary>The two switches you flip often, always visible above the tabs.</summary>
    private void DrawQuickToggles(Configuration cfg)
    {
        var overlayOn = cfg.OverlayVisible;
        if (ImGui.Checkbox("Show overlay", ref overlayOn))
        {
            cfg.OverlayVisible = overlayOn;
            plugin.SetOverlayOpen(overlayOn);
            dirty = true;
        }
        ImGui.SameLine();
        dirty |= ImGui.Checkbox("Lock overlay", ref cfg.LockOverlay);
        ImGui.SameLine();
        HelpMarker("Locked: the overlay is click-through and its title bar is hidden.\nUnlock it to move, resize or switch sheets from the overlay itself.\n" +
                   "Right-click the overlay for a menu with these switches; hold Ctrl while right-clicking if it is locked.");

        ImGui.SameLine();
        dirty |= ImGui.Checkbox("Show full encounter", ref cfg.ShowFullEncounter);
        ImGui.SameLine();
        HelpMarker("On: the overlay shows the whole encounter, every phase one under the other.\n" +
                   "Off: it shows the encounter's own rows plus the phase you are on, and the Reset / Previous / Next\n" +
                   "buttons appear under it\n" +
                   "(also /dutynotes reset, prev and next).\n" +
                   "While this editor is open the overlay shows what is selected here instead.\n" +
                   "Also available as /dutynotes full.");

        // Undo for deletions (also Ctrl+Z while this window is focused and no text box is being edited).
        ImGui.SameLine();
        ImGui.BeginDisabled(undoStack.Count == 0);
        if (ImGui.Button("Undo")) Undo();
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(undoStack.Count == 0 ? "Nothing to undo" : $"Undo: {undoStack[^1].label}");

        if (undoStack.Count > 0 && !ImGui.IsAnyItemActive()
            && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)
            && ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Z, false))
            Undo();
    }

    // Widths of the parts of a settings row, in unscaled pixels (multiplied by the UI scale when used).
    private const float LabelWidth = 120f;
    private const float SliderWidth = 130f;
    private const float InputWidth = 60f;

    /// <summary>Layout settings, then Style settings, one under the other. Scrolls if the window is short.</summary>
    private void DrawSettingsTab(Configuration cfg)
    {
        if (!ImGui.BeginChild("##settings-scroll", new Vector2(0, 0))) { ImGui.EndChild(); return; }

        DrawBehaviorGroup(cfg);
        ImGui.Spacing();
        DrawLayoutGroup(cfg);
        ImGui.Spacing();
        DrawStyleGroup(cfg);

        ImGui.EndChild();
    }

    /// <summary>
    /// A section heading with a Reset button on its right. The button is greyed out while the section already
    /// has its default values, and a reset can be undone like a deletion.
    /// </summary>
    private void GroupHeader(string text, bool isDefault, Func<Action> reset)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.45f, 1f), text);

        const string label = "Reset";
        var width = ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - width);

        ImGui.BeginDisabled(isDefault);
        if (ImGui.SmallButton(label + "##" + text))
        {
            PushUndo($"Reset {text} settings", reset());
            dirty = true;
        }
        ImGui.EndDisabled();

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(isDefault
                ? $"The {text} settings already have their default values"
                : $"Restore the {text} settings to their defaults\n(Undo or Ctrl+Z takes it back)");

        ImGui.Separator();
    }

    private void DrawBehaviorGroup(Configuration cfg)
    {
        GroupHeader("Behavior", cfg.IsBehaviorDefault, cfg.ResetBehavior);

        dirty |= ImGui.Checkbox("Auto-select encounter", ref cfg.AutoSwitch);
        ImGui.SameLine();
        HelpMarker("When you enter a duty, or change job, the overlay selects the encounter set to that duty and job.\n" +
                   "An encounter set to your exact job wins over one set to \"Any job\".\n" +
                   "If several match, the one highest in the Sheets list is used (reorder with Move up / Move down).\n" +
                   "It only reacts to a change, so picking a sheet yourself sticks until the next one.");

        // These two belong to auto-select: they only mean something while it is on, so they are greyed out
        // (keeping their ticks) otherwise.
        ImGui.Indent();
        ImGui.BeginDisabled(!cfg.AutoSwitch);

        dirty |= ImGui.Checkbox("Auto open", ref cfg.AutoShowOverlay);
        ImGui.SameLine();
        HelpMarker("When an encounter is selected this way, open the overlay too if it is hidden.");

        // Only an overlay that auto-select itself opened gets hidden again, so it needs Auto open.
        ImGui.BeginDisabled(!cfg.AutoShowOverlay);
        dirty |= ImGui.Checkbox("Auto hide", ref cfg.AutoHideOverlay);
        ImGui.SameLine();
        HelpMarker("When you leave the duty (or switch to a job it has no sheet for), hide the overlay again.\n" +
                   "Only if the plugin opened it: an overlay you opened yourself stays up.");
        ImGui.EndDisabled();

        ImGui.EndDisabled();

        // Not tied to auto-select, so it stays available whatever the above is set to.
        dirty |= ImGui.Checkbox("Hide during cutscenes", ref cfg.HideDuringCutscenes);
        ImGui.SameLine();
        HelpMarker("Off: the overlay stays on screen during cutscenes.\n" +
                   "On: it is hidden while a cutscene plays, the way Dalamud hides plugin windows by default.\n" +
                   "This covers all of this plugin's windows.");

        ImGui.Unindent();
    }
    private void DrawLayoutGroup(Configuration cfg)
    {
        GroupHeader("Layout", cfg.IsLayoutDefault, cfg.ResetLayout);


        dirty |= ImGui.Checkbox("Show phase buttons", ref cfg.ShowControls);
        ImGui.SameLine();
        if (cfg.ShowFullEncounter)
        {
            // Ticked but not on screen would look like a bug, so say why.
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled("(hidden while the whole encounter is showing)");
            ImGui.SameLine();
        }
        HelpMarker("Show Reset, Previous and Next buttons under the overlay for moving between phases.\n" +
                   "They only appear when the overlay shows a single phase, not the whole encounter.\n" +
                   "The same actions are available as chat commands, for macros:\n" +
                   "/dutynotes reset, /dutynotes prev, /dutynotes next");

        dirty |= ImGui.Checkbox("Show column headers", ref cfg.ShowHeaders);
        ImGui.SameLine();
        HelpMarker("Show Mechanic / Mitigation / Notes labels under every phase title.");

        dirty |= ImGui.Checkbox("Auto-size to content", ref cfg.AutoSize);
        ImGui.SameLine();
        HelpMarker("The overlay resizes itself to fit its tables.\nText wraps at the max text width instead of the window edge.");

        // The options that only apply to auto-size stay in place but greyed out when it is off,
        // so the panel doesn't jump around when you toggle it.
        ImGui.BeginDisabled(!cfg.AutoSize);

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Anchor");
        ImGui.SameLine();
        HelpMarker("The point of the overlay that stays put when it grows or shrinks.\nTop left: grows right and down. Bottom right: grows left and up.");
        DrawAnchorPicker(cfg);

        dirty |= FloatControl("Max text width", ref cfg.MaxTextWidth, 100f, 800f, "%.0f",
            "With auto-size on, mechanic and notes text wrap at this width in pixels.\nKeeps long notes from stretching the overlay.");

        ImGui.EndDisabled();

        dirty |= FloatControl("Column spacing", ref cfg.ColumnPadding, 0f, 40f, "%.0f",
            "Horizontal gap between the Mechanic, Mitigation and Notes columns, in pixels.");

        ImGui.Spacing();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Column alignment");
        ImGui.SameLine();
        HelpMarker("Where the content of each column sits in its cells: pick a dot.\n" +
                   "Left, center or right across; top, center or bottom up and down. Up and down shows when a row is taller\n" +
                   "than a cell's content (a tall icon, or a long note beside a short mechanic).\n" +
                   "Column headers follow the column's across setting.");
        dirty |= DrawAlignmentGrids(cfg);
    }

    /// <summary>
    /// One 3x3 grid of dots per column, like the anchor picker: the dot is where the column's content sits in its cells
    /// (left, center, right across; top, center, bottom down).
    /// </summary>
    private static bool DrawAlignmentGrids(Configuration cfg)
    {
        var changed = false;
        var scale = ImGuiHelpers.GlobalScale;
        var start = ImGui.GetCursorPos();
        var step = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y;
        var labelHeight = ImGui.GetTextLineHeightWithSpacing();
        var columnWidth = step * 3 + 28 * scale;
        var names = new[] { "Mechanic", "Mitigation", "Notes" };

        for (var column = 0; column < 3; column++)
        {
            var h = column switch { 0 => cfg.MechanicHAlign, 1 => cfg.MitigationHAlign, _ => cfg.NotesHAlign };
            var v = column switch { 0 => cfg.MechanicVAlign, 1 => cfg.MitigationVAlign, _ => cfg.NotesVAlign };
            var x = start.X + column * columnWidth;

            ImGui.SetCursorPos(new Vector2(x, start.Y));
            ImGui.TextUnformatted(names[column]);

            for (var i = 0; i < 9; i++)
            {
                var across = (HAlign)(i % 3);
                var down = (VAlign)(i / 3);

                ImGui.SetCursorPos(new Vector2(x + i % 3 * step, start.Y + labelHeight + i / 3 * step));
                if (ImGui.RadioButton($"##align{column}-{i}", h == across && v == down))
                {
                    switch (column)
                    {
                        case 0: cfg.MechanicHAlign = across; cfg.MechanicVAlign = down; break;
                        case 1: cfg.MitigationHAlign = across; cfg.MitigationVAlign = down; break;
                        default: cfg.NotesHAlign = across; cfg.NotesVAlign = down; break;
                    }
                    changed = true;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{names[column]}: {across}, {down}");
            }
        }

        // Moving the cursor past the last item must be followed by an item, or ImGui asserts on the boundary change.
        ImGui.SetCursorPos(new Vector2(start.X, start.Y + labelHeight + step * 2 + ImGui.GetFrameHeight()));
        ImGui.Dummy(Vector2.Zero);
        return changed;
    }
    private void DrawStyleGroup(Configuration cfg)
    {
        GroupHeader("Style", cfg.IsStyleDefault, cfg.ResetStyle);

        dirty |= FloatControl("Background", ref cfg.BackgroundAlpha, 0f, 1f, "%.2f",
            "Opacity of the overlay background.\n0 is fully transparent, 1 is solid.");
        dirty |= FloatControl("Icon size", ref cfg.IconSize, Configuration.MinIcon, Configuration.MaxIcon, "%.0f",
            "Size of the skill icons, in pixels.\nThis also sets the minimum row height.");
        dirty |= FloatControl("Title text", ref cfg.TitleScale, Configuration.MinScale, Configuration.MaxScale, "%.2f",
            "Text size of the encounter and phase titles.\n1 is the normal UI text size.");
        dirty |= FloatControl("Mechanic text", ref cfg.MechanicScale, Configuration.MinScale, Configuration.MaxScale, "%.2f",
            "Text size of the mechanic names.\n1 is the normal UI text size.");
        dirty |= FloatControl("Notes text", ref cfg.NotesScale, Configuration.MinScale, Configuration.MaxScale, "%.2f",
            "Text size of the notes.\n1 is the normal UI text size.");
        dirty |= FloatControl("Note row text", ref cfg.NoteRowScale, Configuration.MinScale, Configuration.MaxScale, "%.2f",
            "Text size of note rows, the full-width free text rows.");
    }

    /// <summary>A 3x3 grid of radio buttons laid out like the overlay's corners, edges and center.</summary>
    private void DrawAnchorPicker(Configuration cfg)
    {
        var start = ImGui.GetCursorPos();
        var step = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y;

        for (var i = 0; i < 9; i++)
        {
            ImGui.SetCursorPos(start + new Vector2(i % 3 * step, i / 3 * step));
            if (ImGui.RadioButton($"##anchor{i}", cfg.Anchor == (OverlayAnchor)i))
            {
                cfg.Anchor = (OverlayAnchor)i;
                dirty = true;
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(((OverlayAnchor)i).ToString());
        }

        // Moving the cursor past the last item must be followed by an item, or ImGui asserts on the boundary change.
        ImGui.SetCursorPos(start + new Vector2(0, step * 2 + ImGui.GetFrameHeight()));
        ImGui.Dummy(Vector2.Zero);
    }

    /// <summary>A slider with a number box next to it, so the value can be dragged or typed.</summary>
    private static bool FloatControl(string label, ref float value, float min, float max, string format, string tooltip)
    {
        var changed = false;

        // Label, slider, number box. The labels share a fixed-width column so the sliders line up.
        var scale = ImGuiHelpers.GlobalScale;
        var left = ImGui.GetCursorPosX();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);

        // SetCursorPosX, not SameLine(offset): SameLine's offset gets the enclosing group's own x added,
        // which pushed everything in the right-hand settings group off screen.
        ImGui.SameLine();
        ImGui.SetCursorPosX(left + LabelWidth * scale);
        ImGui.SetNextItemWidth(SliderWidth * scale);
        changed |= ImGui.SliderFloat($"##{label}-slider", ref value, min, max, format, ImGuiSliderFlags.AlwaysClamp);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(InputWidth * scale);
        changed |= ImGui.InputFloat($"##{label}-input", ref value, 0f, 0f, format);
        // Typing is unclamped so partial input like "0" on the way to "0.8" isn't rewritten; fix it on commit.
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            value = Math.Clamp(value, min, max);
            changed = true;
        }

        ImGui.SameLine();
        HelpMarker(tooltip);

        return changed;
    }

    /// <summary>A dim "(?)" that shows the text when hovered, so only the marker carries the tooltip.</summary>
    private static void HelpMarker(string text)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(text);
    }

    // ---- Tree of sheets -------------------------------------------------------------------

    private void DrawTreePane(Configuration cfg)
    {
        if (ImGui.Button("+ Encounter"))
        {
            var node = new SheetNode { Name = "New encounter" };
            cfg.Roots.Add(node);
            cfg.SelectedId = node.Id;
            dirty = true;
        }

        // Two levels only: encounters, and phases inside an encounter.
        ImGui.SameLine();
        // With a phase selected, the new phase goes into that phase's encounter.
        var phaseParent = cfg.Roots.FirstOrDefault(r => r.Id == cfg.SelectedId || r.Children.Any(c => c.Id == cfg.SelectedId));
        ImGui.BeginDisabled(phaseParent is null);
        if (ImGui.Button("+ Phase") && phaseParent is not null)
        {
            var node = new SheetNode { Name = $"Phase {phaseParent.Children.Count + 1}", Level = phaseParent.Level };
            phaseParent.Children.Add(node);
            cfg.SelectedId = node.Id;
            dirty = true;
        }
        ImGui.EndDisabled();

        // Import opens a box to paste an exported encounter into. (Export, Delete and the others act on the selected
        // sheet, so they sit under its name on the right.)
        ImGui.SameLine();
        if (ImGui.Button("Import"))
        {
            importText = string.Empty;
            importError = null;
            ImGui.OpenPopup("Import an encounter##import");
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Add an encounter from text, as copied with Export. Opens a box to paste it into.");

        DrawImportPopup(cfg);

        ImGui.Separator();
        DrawTree(cfg, cfg.Roots);
    }

    // ---- Import and export ---------------------------------------------------------------------

    private string status = string.Empty;
    private bool statusIsError;
    private double statusUntil;

    private void SetStatus(string text, bool isError)
    {
        status = text;
        statusIsError = isError;
        statusUntil = ImGui.GetTime() + 12;
    }

    /// <summary>The result of the last import or export, shown under the buttons for a few seconds.</summary>
    private void DrawStatus()
    {
        if (status.Length == 0 || ImGui.GetTime() > statusUntil) return;

        var color = statusIsError ? new Vector4(1f, 0.45f, 0.4f, 1f) : new Vector4(0.55f, 0.9f, 0.55f, 1f);
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextWrapped(status);
        ImGui.PopStyleColor();
    }

    private void ExportToClipboard(SheetNode encounter)
    {
        var text = Sharing.Export(encounter);
        ImGui.SetClipboardText(text);
        SetStatus($"Copied \"{encounter.Name}\" to the clipboard ({text.Length} characters). Paste it anywhere to share it.", isError: false);
    }

    private string importText = string.Empty;
    private string? importError;

    /// <summary>The paste window: you see the text before it is imported, and a problem shows next to it.</summary>
    private void DrawImportPopup(Configuration cfg)
    {
        var open = true;
        if (!ImGui.BeginPopupModal("Import an encounter##import", ref open, ImGuiWindowFlags.AlwaysAutoResize)) return;

        ImGui.TextUnformatted("Paste the text made by Export (it starts with DutyNotes:1:).");
        ImGui.InputTextMultiline("##import-text", ref importText, 400_000, new Vector2(420 * ImGuiHelpers.GlobalScale, 120 * ImGuiHelpers.GlobalScale));

        if (ImGui.Button("Paste from clipboard")) importText = ImGui.GetClipboardText() ?? string.Empty;
        ImGui.SameLine();
        ImGui.TextDisabled(importText.Trim().Length == 0 ? "Nothing pasted yet" : $"{importText.Trim().Length} characters");

        if (importError is not null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.45f, 0.4f, 1f));
            ImGui.TextWrapped(importError);
            ImGui.PopStyleColor();
        }

        ImGui.Spacing();
        ImGui.BeginDisabled(importText.Trim().Length == 0);
        if (ImGui.Button("Import"))
        {
            if (ImportText(cfg, importText, out importError))
            {
                importText = string.Empty;
                ImGui.CloseCurrentPopup();
            }
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    /// <summary>Adds the encounter in the text. Returns false, with the reason, when nothing was imported.</summary>
    private bool ImportText(Configuration cfg, string text, out string? error)
    {
        var result = Sharing.Import(
            text,
            skillExists: id => plugin.Catalog.Get(id) is not null,
            findDuty: id => plugin.Duties.Get(id) is { } duty ? (duty.TerritoryId, duty.Name) : null,
            jobIsKnown: id => JobCatalog.Groups.Any(group => group.Ids.Contains(id)));

        if (result.Encounter is not { } encounter)
        {
            error = result.Error ?? "Nothing was imported.";
            return false;
        }

        error = null;

        // It goes in right after the encounter you are on, or at the end.
        var after = cfg.Selected is { } picked ? cfg.Roots.FirstOrDefault(r => r == picked || r.Children.Contains(picked)) : null;
        cfg.Roots.Insert(after is null ? cfg.Roots.Count : cfg.Roots.IndexOf(after) + 1, encounter);
        cfg.SelectedId = encounter.Id;
        dirty = true;

        PushUndo($"Imported \"{encounter.Name}\"", () =>
        {
            cfg.Roots.Remove(encounter);
            if (cfg.Find(cfg.SelectedId) is null && cfg.Roots.Count > 0) cfg.SelectedId = cfg.Roots[0].Id;
        });

        var message = $"Imported \"{encounter.Name}\": {result.Phases} phase(s), {result.Rows} row(s).";
        if (result.DroppedSkills > 0) message += $" {result.DroppedSkills} skill(s) that don't exist in this game version were left out.";
        if (result.DutyCleared) message += " Its duty isn't known in this game version, so pick one.";
        SetStatus(message, isError: false);
        return true;
    }

    private void DrawTree(Configuration cfg, List<SheetNode> nodes)
    {
        foreach (var node in nodes)
        {
            var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.DefaultOpen;
            if (node.Children.Count == 0) flags |= ImGuiTreeNodeFlags.Leaf;
            if (node.Id == cfg.SelectedId) flags |= ImGuiTreeNodeFlags.Selected;

            ImGui.PushID(node.Id.ToString());

            // A phase has a tick: unticked, it is skipped by the overlay, the full encounter and the Previous / Next buttons.
            var isPhase = !cfg.Roots.Contains(node);
            if (isPhase)
            {
                dirty |= ImGui.Checkbox("##on", ref node.Enabled);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(node.Enabled ? "On: shown in the overlay.\nUntick to skip this phase." : "Off: skipped by the overlay, the full encounter and the Previous / Next buttons.\nStill shown here, and in the overlay while you are editing it.");
                ImGui.SameLine();
            }

            // A phase that is off looks dim, so it is clear it is skipped.
            if (isPhase && !node.Enabled) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            var open = ImGui.TreeNodeEx(node.Name + "###node", flags);
            if (isPhase && !node.Enabled) ImGui.PopStyleColor();
            if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
            {
                cfg.SelectedId = node.Id;
                dirty = true;
            }

            if (open)
            {
                DrawTree(cfg, node.Children);
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
    }

    /// <summary>Deletes a sheet (an encounter, or a phase with its encounter kept) and selects a neighbour. Undo puts it back.</summary>
    private void DeleteNode(Configuration cfg, SheetNode node)
    {
        if (cfg.RemoveNode(node) is not { } undo) return;

        PushUndo($"Deleted \"{node.Name}\"", undo);
        dirty = true;
    }

    private void DrawDeleteConfirm(Configuration cfg)
    {
        if (!ImGui.BeginPopup("##confirm-delete")) return;

        var node = cfg.Selected;
        if (node is null)
        {
            ImGui.CloseCurrentPopup();
        }
        else
        {
            ImGui.Text($"Delete \"{node.Name}\"" + (node.Children.Count > 0 ? $" and its {node.Children.Count} phase(s)?" : "?"));
            if (ImGui.Button("Delete"))
            {
                DeleteNode(cfg, node);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    // ---- Selected sheet -------------------------------------------------------------------

    private void DrawSheetPane(Configuration cfg, SheetNode node)
    {
        ImGui.SetNextItemWidth(220);
        dirty |= ImGui.InputText("Name", ref node.Name, 64);

        // Everything that acts on this sheet, on one line under its name.
        var isEncounter = cfg.Roots.Contains(node);

        ImGui.BeginDisabled(cfg.ContainingList(node) is not { } siblings || siblings.IndexOf(node) <= 0);
        if (ImGui.Button("Move up")) MoveNode(cfg, node, -1);
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(cfg.ContainingList(node) is not { } sib2 || sib2.IndexOf(node) >= sib2.Count - 1);
        if (ImGui.Button("Move down")) MoveNode(cfg, node, 1);
        ImGui.EndDisabled();

        if (isEncounter)
        {
            ImGui.SameLine();
            if (ImGui.Button("Duplicate"))
            {
                var copy = node.Clone(node.Name + " (copy)");
                cfg.Roots.Insert(cfg.Roots.IndexOf(node) + 1, copy);
                cfg.SelectedId = copy.Id;
                dirty = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Copy this encounter with all its phases and rows,\nfor example to make a version for another job.");
        }

        // Export copies a whole encounter, so only an encounter has the button.
        if (isEncounter)
        {
            ImGui.SameLine();
            if (ImGui.Button("Export")) ExportToClipboard(node);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Copy this encounter, with all its phases and rows, to the clipboard as text you can share.");
        }

        ImGui.SameLine();
        if (ImGui.Button("Delete"))
        {
            // Ctrl + click skips the question (Undo still brings it back).
            if (ImGui.GetIO().KeyCtrl) DeleteNode(cfg, node);
            else ImGui.OpenPopup("##confirm-delete");
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Delete this sheet, with everything in it. Undo brings it back.\nCtrl + click deletes without asking.");
        DrawDeleteConfirm(cfg);

        // The result of the last import or export.
        DrawStatus();

        if (isEncounter) DrawEncounterLinks(node);

        if (isEncounter) DrawLevelAndFilter(cfg, node);
        ImGui.Separator();

        if (ImGui.BeginChild("##rows", new Vector2(0, 0)))
            DrawRows(node);
        ImGui.EndChild();
    }

    // ---- Duty and job of an encounter ----------------------------------------------------------

    private string dutyFilter = string.Empty;
    private uint dutyType;          // 0 = all content types
    private int dutyExpansion = -1; // -1 = all expansions

    /// <summary>The "Duty" and "Job" rows of an encounter, with their pickers.</summary>
    private void DrawEncounterLinks(SheetNode node)
    {
        var scale = ImGuiHelpers.GlobalScale;
        const float labelX = 48f;

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Duty");
        ImGui.SameLine(labelX * scale);
        // Show the catalog's name so a duty saved before the capitalization fix doesn't stay lowercase.
        var dutyLabel = plugin.Duties.Get(node.DutyId)?.Name ?? node.DutyName;
        if (ImGui.Button(node.DutyId == 0 ? "Choose duty..." : dutyLabel + "##duty"))
        {
            dutyFilter = string.Empty;
            ImGui.OpenPopup("##duty-picker");
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("The duty this sheet is for. When you enter it, the overlay switches to this sheet\n(if Auto-select encounter is on in Settings).");
        if (node.DutyId != 0)
        {
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Times))
            {
                node.DutyId = 0;
                node.DutyTerritoryId = 0;
                node.DutyName = string.Empty;
                dirty = true;
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Clear the duty");
        }

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Job");
        ImGui.SameLine(labelX * scale);
        if (node.JobId != 0)
        {
            var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(JobCatalog.IconId(node.JobId))).GetWrapOrEmpty();
            var side = ImGui.GetFrameHeight();
            ImGui.Image(icon.Handle, new Vector2(side));
            ImGui.SameLine();
        }
        if (ImGui.Button((node.JobId == 0 ? "Any job" : plugin.Jobs.Abbreviation(node.JobId)) + "##job"))
            ImGui.OpenPopup("##job-picker");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("The job this sheet is for. It decides which sheet is chosen when you enter the duty,\nand which job's skills the skill picker offers. \"Any job\" matches everyone.");

        DrawDutyPicker(node);
        DrawJobPicker(node);
    }

    private void DrawDutyPicker(SheetNode node)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetNextWindowSize(new Vector2(540 * scale, 440 * scale), ImGuiCond.Appearing);
        if (!ImGui.BeginPopup("##duty-picker")) return;

        if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##duty-filter", "Search duties (e.g. 'Epic', 'Savage', 'Eden')", ref dutyFilter, 64);

        // Content types with the icons Party Finder uses. The closed box shows the selected type's icon and name,
        // drawn by hand (the box itself gets an empty preview) so the gap between them is exact.
        var types = plugin.Duties.Types;
        var selectedIcon = dutyType == 0 ? 0u : plugin.Duties.TypeIcon(dutyType);
        var typeName = dutyType == 0 ? "All types" : types.FirstOrDefault(t => t.Id == dutyType).Name ?? "All types";
        var iconSide = 20 * scale;

        // Measured before the box is opened: once it is open, ImGui's "last item" is its popup, not the box.
        var comboMin = ImGui.GetCursorScreenPos();
        var comboHeight = ImGui.GetFrameHeight();

        ImGui.SetNextItemWidth(220 * scale);
        var typeOpen = ImGui.BeginCombo("##duty-type", string.Empty);
        {
            var style = ImGui.GetStyle();
            var draw = ImGui.GetWindowDrawList();
            var x = comboMin.X + style.FramePadding.X;

            if (selectedIcon != 0)
            {
                var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(selectedIcon)).GetWrapOrEmpty();
                var top = new Vector2(x, comboMin.Y + (comboHeight - iconSide) / 2f);
                draw.AddImage(icon.Handle, top, top + new Vector2(iconSide));
                x += iconSide + 8 * scale;
            }

            draw.AddText(new Vector2(x, comboMin.Y + (comboHeight - ImGui.GetTextLineHeight()) / 2f), ImGui.GetColorU32(ImGuiCol.Text), typeName);
        }

        if (typeOpen)
        {
            // A hand-drawn row doesn't close the box on its own the way a Selectable does, so it is closed here.
            if (IconRow("##type-all", 0, "All types", dutyType == 0))
            {
                dutyType = 0;
                ImGui.CloseCurrentPopup();
            }

            foreach (var (id, name, iconId) in types)
            {
                if (!IconRow($"##type{id}", iconId, name, dutyType == id)) continue;
                dutyType = id;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(220 * scale);
        if (ImGui.BeginCombo("##duty-expansion", dutyExpansion < 0 ? "All expansions" : plugin.Duties.ExpansionName(dutyExpansion)))
        {
            if (ImGui.Selectable("All expansions", dutyExpansion < 0)) dutyExpansion = -1;
            for (var ex = plugin.Duties.Expansions.Count - 1; ex >= 0; ex--)
                if (ImGui.Selectable($"{plugin.Duties.Expansions[ex]}##ex{ex}", dutyExpansion == ex)) dutyExpansion = ex;
            ImGui.EndCombo();
        }

        if (ImGui.BeginChild("##duty-list", new Vector2(0, 0)))
        {
            var shown = 0;
            foreach (var duty in plugin.Duties.All)
            {
                if (dutyType != 0 && duty.TypeId != dutyType) continue;
                if (dutyExpansion >= 0 && duty.Expansion != dutyExpansion) continue;
                if (dutyFilter.Length > 0 && !duty.Name.Contains(dutyFilter, StringComparison.OrdinalIgnoreCase)) continue;

                shown++;
                if (ImGui.Selectable($"{duty.Name}  (Lv {duty.Level})##duty{duty.Id}", node.DutyId == duty.Id))
                {
                    ChooseDuty(node, duty);
                    ImGui.CloseCurrentPopup();
                }
            }

            if (shown == 0) ImGui.TextDisabled("No duties match.");
        }
        ImGui.EndChild();

        ImGui.EndPopup();
    }

    /// <summary>
    /// A list row with an icon, as tall as the icon. The highlight, icon and text are drawn by hand so they line up
    /// exactly (a plain Selectable puts the text at the top), the same way as in the skill picker.
    /// </summary>
    private static bool IconRow(string id, uint iconId, string label, bool selected)
    {
        var side = 24 * ImGuiHelpers.GlobalScale;
        var pos = ImGui.GetCursorScreenPos();
        var width = Math.Max(ImGui.GetContentRegionAvail().X, ImGui.CalcTextSize(label).X + side + 12);
        var clicked = ImGui.InvisibleButton(id, new Vector2(width, side));
        var draw = ImGui.GetWindowDrawList();

        if (selected) draw.AddRectFilled(pos, pos + new Vector2(width, side), ImGui.GetColorU32(ImGuiCol.Header));
        if (ImGui.IsItemHovered()) draw.AddRectFilled(pos, pos + new Vector2(width, side), ImGui.GetColorU32(ImGuiCol.HeaderHovered));

        if (iconId != 0)
        {
            var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
            draw.AddImage(icon.Handle, pos, pos + new Vector2(side));
        }

        draw.AddText(new Vector2(pos.X + side + 6, pos.Y + (side - ImGui.GetTextLineHeight()) / 2f), ImGui.GetColorU32(ImGuiCol.Text), label);
        return clicked;
    }

    private void ChooseDuty(SheetNode node, DutyInfo duty)
    {
        // A freshly added encounter still has its placeholder name: take the duty's name instead.
        if (node.Name == "New encounter" || node.Name.StartsWith("Encounter ", StringComparison.Ordinal))
            node.Name = duty.Name;

        // The level follows the duty (the level it is played at). It can still be changed by hand afterwards.
        node.Level = Math.Clamp(duty.Level, 1, MaxLevel);

        node.DutyId = duty.Id;
        node.DutyTerritoryId = duty.TerritoryId;
        node.DutyName = duty.Name;
        dirty = true;
    }

    private void DrawJobPicker(SheetNode node)
    {
        if (!ImGui.BeginPopup("##job-picker")) return;

        var side = new Vector2(34 * ImGuiHelpers.GlobalScale);

        if (ImGui.Selectable("Any job", node.JobId == 0, ImGuiSelectableFlags.None, new Vector2(0, 0)))
        {
            node.JobId = 0;
            dirty = true;
            ImGui.CloseCurrentPopup();
        }

        foreach (var (role, ids) in JobCatalog.Groups)
        {
            ImGui.Spacing();
            ImGui.TextDisabled(role);

            for (var k = 0; k < ids.Length; k++)
            {
                var id = ids[k];
                if (k > 0) ImGui.SameLine();

                var selected = id == node.JobId;
                if (selected) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));

                ImGui.PushID((int)id);
                var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(JobCatalog.IconId(id))).GetWrapOrEmpty();
                if (ImGui.ImageButton(icon.Handle, side))
                {
                    node.JobId = id;
                    dirty = true;
                    ImGui.CloseCurrentPopup();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(plugin.Jobs.Abbreviation(id));
                ImGui.PopID();

                if (selected) ImGui.PopStyleColor();
            }
        }

        ImGui.EndPopup();
    }

    private void MoveNode(Configuration cfg, SheetNode node, int delta)
    {
        var list = cfg.ContainingList(node);
        if (list is null) return;
        var i = list.IndexOf(node);
        var j = i + delta;
        if (j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
        dirty = true;
    }

    private void DrawLevelAndFilter(Configuration cfg, SheetNode node)
    {
        var level = node.Level;
        ImGui.SetNextItemWidth(110);
        if (ImGui.InputInt("Level", ref level, 1, 10))
        {
            node.Level = Math.Clamp(level, 1, MaxLevel);
            dirty = true;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Only skills unlocked at this level are offered in the skill picker, for this encounter and all of its phases.\nChoosing a duty sets it to the level that duty is played at; change it here to override that.");

        ImGui.SameLine();
        var player = Plugin.ObjectTable.LocalPlayer;
        ImGui.BeginDisabled(player is null);
        if (ImGui.Button("Use my level") && player is not null)
        {
            node.Level = Math.Clamp((int)player.Level, 1, MaxLevel);
            dirty = true;
        }
        ImGui.EndDisabled();

        // The level follows the chosen duty by default. Changing it by hand overrides that, and this goes back to it.
        if (plugin.Duties.Get(node.DutyId) is { } duty && node.Level != duty.Level)
        {
            ImGui.SameLine();
            if (ImGui.Button($"Use duty level ({duty.Level})"))
            {
                node.Level = Math.Clamp(duty.Level, 1, MaxLevel);
                dirty = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"The level is set by hand. {duty.Name} is played at level {duty.Level}; click to follow the duty again.");
        }
    }

    private const string RowPayload = "MITSHEET_ROW";
    private const string SkillPayload = "MITSHEET_SKILL";

    // Drag payloads carry no data of their own; what is being dragged is kept here.
    private SheetNode? dragRowNode;
    private int dragRowIndex;
    private SheetRow? dragSkillRow;
    private int dragSkillIndex;

    // Each row's height in the last frame, for the button behind it (which has to be submitted before the row's content is drawn).
    private readonly ConditionalWeakTable<SheetRow, StrongBox<float>> rowHeights = new();

    private static readonly Vector4 RowBand = new(1f, 1f, 1f, 0.08f); // the lighter band behind every row
    private const float RowSpacer = 8f;   // the height of the spacer item between two rows, before scaling
    private const float RowBandGap = 4f;  // the gap left between one row's band and the next, before scaling
    private const float RowInset = 6f;    // how far a row's content sits inside its band at the left and right, before scaling

    private void DrawRows(SheetNode node)
    {
        var inset = RowInset * ImGuiHelpers.GlobalScale;
        int? remove = null;
        int? duplicate = null;
        (int position, bool note)? insertAt = null;
        (int from, int to)? move = null;

        // A small margin above the first row, so the insertion line has room to show above it
        // (it would be clipped at the very top of the scrolling area).
        ImGui.Dummy(new Vector2(0, 4 * ImGuiHelpers.GlobalScale));

        for (var i = 0; i < node.Rows.Count; i++)
        {
            var row = node.Rows[i];
            ImGui.PushID(i);

            // The row's background is a button, the handle for dragging the row and the target of its right-click menu. It is submitted BEFORE the
            // row's content, and allows overlap, so the boxes and buttons on top of it still get their own clicks (and dragging inside a text box
            // still selects text). Its height is the row's height from the last frame, so it is missing for the first frame of a new row.
            var rowTop = ImGui.GetCursorScreenPos();
            if (rowHeights.TryGetValue(row, out var knownHeight) && knownHeight.Value > 1f)
            {
                ImGui.InvisibleButton("##band", new Vector2(ImGui.GetContentRegionAvail().X, knownHeight.Value));
                ImGui.SetItemAllowOverlap();

                if (ImGui.BeginDragDropSource())
                {
                    dragRowNode = node;
                    dragRowIndex = i;
                    ImGui.SetDragDropPayload(RowPayload, new byte[] { 1 });
                    ImGui.TextUnformatted(RowLabel(row, i));
                    ImGui.EndDragDropSource();
                }

                // Right-clicking the row's background (not a text box or a button on it) opens its menu.
                if (ImGui.BeginPopupContextItem("##rowmenu"))
                {
                    if (ImGui.MenuItem("Add mechanic row above")) insertAt = (i, false);
                    if (ImGui.MenuItem("Add mechanic row below")) insertAt = (i + 1, false);
                    if (ImGui.MenuItem("Add note row above")) insertAt = (i, true);
                    if (ImGui.MenuItem("Add note row below")) insertAt = (i + 1, true);
                    ImGui.Separator();
                    if (ImGui.MenuItem("Duplicate")) duplicate = i;
                    if (ImGui.MenuItem("Delete")) remove = i;
                    ImGui.EndPopup();
                }

                ImGui.SetCursorScreenPos(rowTop); // the content goes on top of the button, from the same place
            }

            // The whole row is one group, so it can be the drop target for a dragged row. Its content is drawn on the upper of two layers, so a
            // background can go behind it once its size is known.
            var draw = ImGui.GetWindowDrawList();
            draw.ChannelsSplit(2);
            draw.ChannelsSetCurrent(1);
            ImGui.BeginGroup();

            // The content sits inside the band: indented from its left edge, stopping short of its right edge, with the item spacing above and below.
            ImGui.Dummy(Vector2.Zero);
            ImGui.Indent(inset);

            // A tick: unticked, the row is left out of the overlay. It stays here, dimmed, so it can be ticked again.
            dirty |= ImGui.Checkbox("##on", ref row.Enabled);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(row.Enabled ? "On: shown in the overlay.\nUntick to leave this row out." : "Off: left out of the overlay.\nTick to show it again.");
            if (!row.Enabled) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);

            if (row.IsNote)
            {
                // A note row: one text box, shown across the full width of the table in the overlay.
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled("Note");
                ImGui.SameLine();
                HelpMarker("A note row is free text across the full width of the table.\nPut it wherever it applies: above the rows, between two mechanics, or below them.");

                DrawRowActions(i, ref duplicate, ref remove);

                var height = ImGui.GetTextLineHeightWithSpacing() * 3 + ImGui.GetStyle().FramePadding.Y * 2;
                FocusIfNew(row);
                dirty |= ImGui.InputTextMultiline("##text", ref row.Text, 1000, new Vector2(-inset, height));
            }
            else
            {
                // The row's own actions sit with the grip and the tick; the mechanic goes on its own line under them, like the notes and skills below it.
                DrawRowActions(i, ref duplicate, ref remove);

                ImGui.SetNextItemWidth(-inset);
                FocusIfNew(row);
                dirty |= ImGui.InputTextWithHint("##mech", "Mechanic", ref row.Mechanic, 128);

                // The editor lists mechanic, notes, then skills, even though the notes column comes last in the overlay.
                ImGui.SetNextItemWidth(-inset);
                dirty |= ImGui.InputTextWithHint("##notes", "Notes", ref row.Notes, 256);

                DrawActionRow(node, i, row);
            }

            if (!row.Enabled) ImGui.PopStyleVar();
            ImGui.Unindent(inset);
            ImGui.Dummy(Vector2.Zero);
            ImGui.EndGroup();

            // Every row gets a lighter band behind it, with a small even gap between one band and the next. The space between two rows is the
            // item spacing above and below the gap item, so the band grows past the row by enough for exactly RowBandGap to be left over.
            var rowMin = ImGui.GetItemRectMin();
            var rowMax = ImGui.GetItemRectMax();
            if (!rowHeights.TryGetValue(row, out var heightBox)) rowHeights.Add(row, heightBox = new StrongBox<float>());
            heightBox.Value = rowMax.Y - rowMin.Y;
            draw.ChannelsSetCurrent(0);
            var scale = ImGuiHelpers.GlobalScale;
            var padY = Math.Max(0f, (2 * ImGui.GetStyle().ItemSpacing.Y + RowSpacer * scale - RowBandGap * scale) / 2f);
            draw.AddRectFilled(rowMin - new Vector2(inset, padY), rowMax + new Vector2(inset, padY), ImGui.GetColorU32(RowBand), 3f * scale);
            draw.ChannelsMerge();

            // Dropping a dragged row anywhere on this row (not just on its grip) moves it here.
            // Instead of ImGui's default outline, show an insertion line where the row will end up:
            // above this row when dragging upwards, below it when dragging downwards.
            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload(RowPayload,
                    ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect);
                if (!payload.IsNull && ReferenceEquals(dragRowNode, node) && dragRowIndex != i)
                {
                    var y = dragRowIndex > i ? rowMin.Y - 2 : rowMax.Y + 2;
                    ImGui.GetWindowDrawList().AddLine(new Vector2(rowMin.X, y), new Vector2(rowMax.X, y),
                                                      ImGui.GetColorU32(new Vector4(1f, 0.85f, 0.2f, 1f)), 3f);

                    if (payload.IsDelivery()) move = (dragRowIndex, i);
                }
                ImGui.EndDragDropTarget();
            }

            // The bands tell the rows apart, so there is no line between them, only the gap.
            ImGui.Dummy(new Vector2(0, RowSpacer * ImGuiHelpers.GlobalScale));
            ImGui.PopID();
        }

        if (remove is { } r)
        {
            var removed = node.Rows[r];
            node.Rows.RemoveAt(r);
            PushUndo(DescribeRow("Deleted row", removed), () => node.Rows.Insert(Math.Min(r, node.Rows.Count), removed));
            dirty = true;
        }
        else if (insertAt is { } at)
        {
            var added = node.InsertRowAt(at.position, at.note);
            PushUndo(added.IsNote ? "Added a note row" : "Added a row", () => node.Rows.Remove(added));
            focusNewRow = added; // the cursor goes into its text box, ready to type
            dirty = true;
        }
        else if (duplicate is { } d)
        {
            var source = node.Rows[d];
            var copy = source.Clone();
            node.Rows.Insert(d + 1, copy);
            PushUndo(DescribeRow("Duplicated row", source), () => node.Rows.Remove(copy));
            dirty = true;
        }
        else if (move is { } m && m.from < node.Rows.Count)
        {
            var moved = node.Rows[m.from];
            node.Rows.RemoveAt(m.from);
            node.Rows.Insert(Math.Min(m.to, node.Rows.Count), moved);
            dirty = true;
        }

        // A line closes the list (a line above it is drawn by the sheet pane, under the encounter's settings).
        if (node.Rows.Count > 0) ImGui.Separator();
        else ImGui.TextDisabled("No rows yet.");

        if (ImGui.Button("Add mechanic row"))
        {
            node.Rows.Add(new SheetRow());
            dirty = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("A row with a mechanic, the skills to use for it, and optional notes.");

        ImGui.SameLine();
        if (ImGui.Button("Add note row"))
        {
            node.Rows.Add(new SheetRow { IsNote = true });
            dirty = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("A row of free text across the full width of the table.");

        // Rows are dragged by their background, and can also be added above or below a row, duplicated or deleted, from the menu on a right-click of it.
        if (node.Rows.Count > 0) ImGui.TextDisabled("Drag a row to reorder it. Right-click a row for more options.");
    }

    /// <summary>The icons beside a row's tick, after a small divider: duplicate it, or delete it (red when hovered, so it is not hit by accident).</summary>
    private static void DrawRowActions(int index, ref int? duplicate, ref int? remove)
    {
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("|");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Clone)) duplicate = index;
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Duplicate this row.");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash, null, null, new Vector4(0.75f, 0.2f, 0.2f, 1f))) remove = index;
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Delete this row.");
    }

    /// <summary>What to call a row in undo entries and while dragging it: its mechanic, or the start of its note.</summary>
    private static string RowText(SheetRow row)
    {
        var text = (row.IsNote ? row.Text : row.Mechanic).Trim();
        var firstLine = text.Split('\n')[0].Trim();
        return firstLine.Length > 28 ? firstLine[..28] + "..." : firstLine;
    }

    private static string RowLabel(SheetRow row, int index)
        => RowText(row) is { Length: > 0 } text ? text : (row.IsNote ? $"Note {index + 1}" : $"Row {index + 1}");

    private static string DescribeRow(string verb, SheetRow row)
        => RowText(row) is { Length: > 0 } text ? $"{verb} \"{text}\"" : verb;

    private void DrawActionRow(SheetNode node, int index, SheetRow row)
    {
        var size = new Vector2(Math.Clamp(plugin.Config.IconSize, Configuration.MinIcon, Configuration.MaxIcon));
        int? removeAt = null;
        (SheetRow src, int from, int to)? moveTo = null;

        for (var a = 0; a < row.Actions.Count; a++)
        {
            var info = plugin.Catalog.Get(row.Actions[a]);
            if (info is null) continue;

            ImGui.PushID(a);
            var wrap = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(info.Icon)).GetWrapOrEmpty();

            // Left button drags, right-click removes. The button's own click does nothing.
            ImGui.ImageButton(wrap.Handle, size);
            var hovered = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) removeAt = a;

            if (ImGui.BeginDragDropSource())
            {
                dragSkillRow = row;
                dragSkillIndex = a;
                ImGui.SetDragDropPayload(SkillPayload, new byte[] { 1 });
                ImGui.Image(wrap.Handle, size); // what follows the cursor
                ImGui.EndDragDropSource();
            }
            else if (hovered)
            {
                ImGui.SetTooltip($"{info.Name} ({info.Job}, lvl {info.Level})\nDrag to reorder or move to another row, right-click to remove");
            }

            // Dropping on an icon puts the dragged skill in that position, in this row or from another one.
            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload(SkillPayload);
                if (!payload.IsNull && dragSkillRow is { } src && !(ReferenceEquals(src, row) && dragSkillIndex == a))
                    moveTo = (src, dragSkillIndex, a);
                ImGui.EndDragDropTarget();
            }

            ImGui.PopID();
            ImGui.SameLine();
        }

        if (removeAt is { } ra)
        {
            var id = row.Actions[ra];
            row.Actions.RemoveAt(ra);
            var name = plugin.Catalog.Get(id)?.Name ?? "skill";
            PushUndo($"Removed {name}", () => row.Actions.Insert(Math.Min(ra, row.Actions.Count), id));
            dirty = true;
        }

        // Same size as the skill icons, so it reads as an empty slot waiting for the next skill.
        // The icons are image buttons, which add frame padding around the picture, so add it here too.
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus, size + ImGui.GetStyle().FramePadding * 2))
        {
            pickerRow = index;
            filter = string.Empty;
            ImGui.OpenPopup("##picker");
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add skills");

        // Dropping on the button appends the dragged skill to the end of this row, which also
        // lets you move a skill into a row that has none yet.
        if (ImGui.BeginDragDropTarget())
        {
            var payload = ImGui.AcceptDragDropPayload(SkillPayload);
            if (!payload.IsNull && dragSkillRow is { } src)
                moveTo = (src, dragSkillIndex, row.Actions.Count);
            ImGui.EndDragDropTarget();
        }

        if (moveTo is { } m)
        {
            MoveSkill(m.src, m.from, row, m.to);
            dirty = true;
        }

        if (pickerRow == index)
            DrawPicker(node, row);
    }

    private static void MoveSkill(SheetRow src, int from, SheetRow dst, int to)
    {
        if (from < 0 || from >= src.Actions.Count) return;

        var id = src.Actions[from];
        // The same skill twice in one row is never wanted.
        if (!ReferenceEquals(src, dst) && dst.Actions.Contains(id)) return;

        src.Actions.RemoveAt(from);
        dst.Actions.Insert(Math.Clamp(to, 0, dst.Actions.Count), id);
    }

    // ---- Undo ---------------------------------------------------------------------------------
    // Deleting rows, sheets and skills can be taken back. The history lasts until the plugin reloads.

    private readonly List<(string label, Action restore)> undoStack = new();

    // A row that has just been inserted: its text box takes the keyboard focus the next time it is drawn.
    private SheetRow? focusNewRow;

    private void FocusIfNew(SheetRow row)
    {
        if (!ReferenceEquals(row, focusNewRow)) return;

        focusNewRow = null;
        ImGui.SetKeyboardFocusHere();
    }

    private void PushUndo(string label, Action restore)
    {
        undoStack.Add((label, restore));
        if (undoStack.Count > 30) undoStack.RemoveAt(0);
    }

    private void Undo()
    {
        if (undoStack.Count == 0) return;
        var (_, restore) = undoStack[^1];
        undoStack.RemoveAt(undoStack.Count - 1);
        restore();
        dirty = true;
    }

    private void DrawPicker(SheetNode node, SheetRow row)
    {
        ImGui.SetNextWindowSize(new Vector2(400, 380), ImGuiCond.Appearing);
        if (!ImGui.BeginPopup("##picker")) return;

        if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##filter", "Search skill or job (e.g. 'Reprisal', 'WAR')", ref filter, 64);

        // These only affect this list, so they live here. They are remembered between sessions.
        dirty |= ImGui.Checkbox("Only this job", ref plugin.Config.OnlyCurrentJob);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Limit the list to the encounter's job, or to your current job if the encounter has none\n(plus role actions it can use). Ignored when you are not logged in.");
        ImGui.SameLine();
        dirty |= ImGui.Checkbox("Defensive + healing", ref plugin.Config.OnlyDefensive);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Hide everything that isn't a defensive cooldown or a healing skill (damage attacks, combos, buffs...).\nThe lists are curated in DefensiveSkills.cs.");

        // Right-aligned on the same line as the checkboxes.
        const string done = "Done";
        var doneWidth = ImGui.CalcTextSize(done).X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - doneWidth - ImGui.GetStyle().WindowPadding.X);
        if (ImGui.Button(done)) ImGui.CloseCurrentPopup();

        // The encounter's job if it has one (so you can plan for a job you are not playing), otherwise your own.
        var player = Plugin.ObjectTable.LocalPlayer;
        var encounter = plugin.Config.Roots.FirstOrDefault(r => r == node || r.Children.Contains(node));
        string? jobFilter = null;
        if (plugin.Config.OnlyCurrentJob)
        {
            if (encounter is { JobId: not 0 } withJob) jobFilter = plugin.Jobs.Abbreviation(withJob.JobId);
            else if (player?.ClassJob.ValueNullable is not null) jobFilter = player.ClassJob.Value.Abbreviation.ToString();
        }

        // The level is set once, on the encounter; its phases use it too.
        var level = (encounter ?? node).Level;

        ImGui.TextDisabled((jobFilter ?? "All jobs") + $", level {level}" + (plugin.Config.OnlyDefensive ? ", defensive and healing" : string.Empty));

        if (ImGui.BeginChild("##list", new Vector2(0, 0)))
        {
            var shown = 0;
            var iconSize = new Vector2(24);
            foreach (var family in plugin.Catalog.Families)
            {
                if (plugin.Config.OnlyDefensive && !family.Defensive) continue;

                // The version you actually have at this level; hide skills not unlocked yet.
                var info = family.At(level);
                if (info is null) continue;
                if (jobFilter is not null && !plugin.Catalog.UsableBy(info, jobFilter)) continue;

                // Search every version of the skill, regardless of level (Bloodwhetting finds Raw Intuition).
                ActionInfo? matchedOther = null;
                if (filter.Length > 0)
                {
                    var byName = family.Versions.FirstOrDefault(v => v.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
                    if (byName is null && !info.Job.Equals(filter, StringComparison.OrdinalIgnoreCase)) continue;
                    if (byName is not null && byName.Id != info.Id
                        && !info.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        matchedOther = byName;
                }

                if (++shown > MaxPickerResults)
                {
                    ImGui.TextDisabled("Keep typing to narrow the list...");
                    break;
                }

                var wrap = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(info.Icon)).GetWrapOrEmpty();
                // The row is exactly as tall as the icon. It is drawn by hand so the highlight, icon and
                // text line up precisely: an invisible button handles the click, the draw list does the rest.
                var pos = ImGui.GetCursorScreenPos();
                var width = ImGui.GetContentRegionAvail().X;
                var clicked = ImGui.InvisibleButton($"##{info.Id}", new Vector2(width, iconSize.Y));
                var draw = ImGui.GetWindowDrawList();

                // Skills already in this row stay highlighted; clicking one again removes it.
                var inRow = family.Versions.Any(v => row.Actions.Contains(v.Id));
                if (inRow)
                    draw.AddRectFilled(pos, pos + new Vector2(width, iconSize.Y), ImGui.GetColorU32(ImGuiCol.Header));
                if (ImGui.IsItemHovered())
                    draw.AddRectFilled(pos, pos + new Vector2(width, iconSize.Y), ImGui.GetColorU32(ImGuiCol.HeaderHovered));

                draw.AddImage(wrap.Handle, pos, pos + iconSize);

                var textColor = ImGui.GetColorU32(ImGuiCol.Text);
                var textY = pos.Y + (iconSize.Y - ImGui.GetTextLineHeight()) / 2f;
                var x = pos.X + iconSize.X + 6;

                if (matchedOther is null)
                {
                    draw.AddText(new Vector2(x, textY), textColor, info.Name);
                }
                else
                {
                    // "Vengeance (<Damnation icon>)": the version that matched the search, shown as its icon.
                    const string open = " (";
                    const string close = ")";
                    draw.AddText(new Vector2(x, textY), textColor, info.Name + open);
                    x += ImGui.CalcTextSize(info.Name + open).X;

                    var otherWrap = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(matchedOther.Icon)).GetWrapOrEmpty();
                    draw.AddImage(otherWrap.Handle, new Vector2(x, pos.Y), new Vector2(x + iconSize.X, pos.Y + iconSize.Y));
                    x += iconSize.X;
                    draw.AddText(new Vector2(x, textY), textColor, close);

                    if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Matched {matchedOther.Name} (lvl {matchedOther.Level})");
                }

                if (inRow)
                {
                    var label = "Added";
                    draw.AddText(new Vector2(pos.X + width - ImGui.CalcTextSize(label).X - 6, textY),
                                 ImGui.GetColorU32(ImGuiCol.TextDisabled), label);
                }

                // The picker stays open so several skills can be added in one go (close it with Done,
                // Escape, or by clicking outside).
                if (clicked)
                {
                    if (inRow)
                    {
                        foreach (var v in family.Versions) row.Actions.RemoveAll(a => a == v.Id);
                    }
                    else
                    {
                        row.Actions.Add(info.Id);
                    }
                    dirty = true;
                }
            }

            if (shown == 0) ImGui.TextDisabled("No skills match.");
        }
        ImGui.EndChild();

        ImGui.EndPopup();
    }
}
