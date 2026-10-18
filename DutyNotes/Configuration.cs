using Dalamud.Configuration;
using Newtonsoft.Json;

namespace DutyNotes;

[Serializable]
public class SheetRow
{
    /// <summary>
    /// True for a note row: free text across the full width of the table, with no mechanic, skills or notes
    /// column. Its text is <see cref="Text"/>.
    /// </summary>
    public bool IsNote;

    public string Text = string.Empty;

    public string Mechanic = string.Empty;
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<uint> Actions = new();
    public string Notes = string.Empty;

    /// <summary>
    /// A row that is switched off is left out of the overlay (and of its Notes column) but stays in the editor, dimmed.
    /// A file from before this existed has every row on.
    /// </summary>
    public bool Enabled = true;

    /// <summary>A copy that can be edited without touching this row. The one place a row is copied, so a new field can't be forgotten.</summary>
    public SheetRow Clone() => new() { IsNote = IsNote, Text = Text, Mechanic = Mechanic, Actions = new List<uint>(Actions), Notes = Notes, Enabled = Enabled };
}

/// <summary>A sheet. Every node can hold rows and child nodes, so a node doubles as a folder.</summary>
[Serializable]
public class SheetNode
{
    public Guid Id = Guid.NewGuid();
    public string Name = "New sheet";

    /// <summary>Level used to filter which skills the picker offers.</summary>
    public int Level = 100;

    // Encounters (top level nodes) can be tied to a duty and a job. The overlay then switches to the
    // matching encounter when you enter that duty on that job. Phases inherit both from their encounter.

    /// <summary>ContentFinderCondition row of the chosen duty, or 0 for none.</summary>
    public uint DutyId;

    /// <summary>Zone (TerritoryType) the duty takes place in; this is what is matched against your location.</summary>
    public uint DutyTerritoryId;

    /// <summary>Name of the chosen duty, kept so it can be shown without looking it up.</summary>
    public string DutyName = string.Empty;

    /// <summary>ClassJob row of the chosen job, or 0 for any job.</summary>
    public uint JobId;


    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<SheetRow> Rows = new();

    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<SheetNode> Children = new();

    /// <summary>
    /// A phase that is switched off is skipped: it is left out of the overlay, of the full encounter and of the Previous,
    /// Next and Reset steps. (While the editor is open, a phase you have selected still shows, so you can edit it.)
    /// Only meaningful for phases. A file from before this existed has every phase on.
    /// </summary>
    public bool Enabled = true;

    /// <summary>
    /// Adds an empty row (a mechanic row, or a note row) right below the row at <paramref name="index"/>, and returns it. An index that is not a
    /// row (nothing selected, or past the end) puts it at the end.
    /// </summary>
    public SheetRow InsertRowBelow(int index, bool isNote)
        => InsertRowAt(index < 0 || index >= Rows.Count ? Rows.Count : index + 1, isNote);

    /// <summary>
    /// Adds an empty row (a mechanic row, or a note row) at a position in the list, and returns it. "Above" row 3 is position 3, "below" it is
    /// position 4. A position outside the list is brought back into it (before the first row, or at the end).
    /// </summary>
    public SheetRow InsertRowAt(int position, bool isNote)
    {
        var row = new SheetRow { IsNote = isNote };
        Rows.Insert(Math.Clamp(position, 0, Rows.Count), row);
        return row;
    }

    /// <summary>Whether there is anything to show: a mechanic row, or a note row with text in it. Rows that are switched off don't count.</summary>
    public bool HasContent() => Rows.Any(r => r.Enabled && (!r.IsNote || !string.IsNullOrWhiteSpace(r.Text)));

    /// <summary>A deep copy with fresh ids, so the copy can be edited without touching the original.</summary>
    public SheetNode Clone(string? name = null) => new()
    {
        Name = name ?? Name,
        Level = Level,
        DutyId = DutyId,
        DutyTerritoryId = DutyTerritoryId,
        DutyName = DutyName,
        JobId = JobId,
        Enabled = Enabled,
        Rows = Rows.Select(r => r.Clone()).ToList(),
        Children = Children.Select(c => c.Clone()).ToList(),
    };
}

/// <summary>A move between the phases of the current encounter.</summary>
internal enum PhaseStep { Reset, Previous, Next }

internal enum OverlayChange { None, Show, Hide }

/// <summary>The point of the overlay that stays fixed when it resizes itself to fit its content.</summary>
public enum OverlayAnchor
{
    TopLeft, Top, TopRight,
    Left, Center, Right,
    BottomLeft, Bottom, BottomRight,
}

/// <summary>Where the content of a table column sits inside its cells, across.</summary>
public enum HAlign { Left, Center, Right }

/// <summary>Where the content of a table column sits inside its cells, up and down (matters when a row is taller than the content).</summary>
public enum VAlign { Top, Center, Bottom }

/// <summary>The arithmetic behind alignment, kept here so it can be tested without the game.</summary>
internal static class Alignment
{
    public static float Factor(HAlign a) => a switch { HAlign.Center => 0.5f, HAlign.Right => 1f, _ => 0f };
    public static float Factor(VAlign a) => a switch { VAlign.Center => 0.5f, VAlign.Bottom => 1f, _ => 0f };

    /// <summary>How far to move content of a size into a space: none, half of the spare room, or all of it. Never negative.</summary>
    public static float Offset(float space, float content, float factor) => Math.Max(0f, (space - content) * factor);
}

/// <summary>
/// The starting values of the display settings. A fresh install uses them, and so do the Reset buttons in the
/// Settings tab, so the two can't drift apart.
/// </summary>
internal static class Defaults
{
    // Behavior
    public const bool AutoSwitch = true;
    public const bool AutoShowOverlay = true;
    public const bool AutoHideOverlay = true;
    public const bool HideDuringCutscenes = false;

    // Layout
    public const bool ShowFullEncounter = true;
    public const bool ShowControls = true;
    public const bool ShowHeaders = true;
    public const bool AutoSize = false;
    public const OverlayAnchor Anchor = OverlayAnchor.TopLeft;
    public const float MaxTextWidth = 280f;
    public const float ColumnPadding = 10f;
    public const HAlign MechanicHAlign = HAlign.Left;
    public const VAlign MechanicVAlign = VAlign.Top;
    public const HAlign MitigationHAlign = HAlign.Left;
    public const VAlign MitigationVAlign = VAlign.Top;
    public const HAlign NotesHAlign = HAlign.Left;
    public const VAlign NotesVAlign = VAlign.Top;

    // Style
    public const float BackgroundAlpha = 0.6f;
    public const float IconSize = 32f;
    public const float TitleScale = 1.15f;
    public const float MechanicScale = 1f;
    public const float NotesScale = 1f;
    public const float NoteRowScale = 1f;
}
[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    // Replace, not Auto: with Dalamud's default of populating the pre-created list, every load duplicated the entries.
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<SheetNode> Roots = new();

    /// <summary>The node selected in the editor's tree.</summary>
    public Guid SelectedId = Guid.Empty;

    // Where the overlay is while you play: an encounter, and the phase you are on within it. This is separate from
    // the editor's selection, which only drives the overlay while the editor is open.
    public Guid ActiveEncounterId = Guid.Empty;
    public Guid ActivePhaseId = Guid.Empty;

    public bool OverlayVisible = true;
    public bool LockOverlay = false;
    public float BackgroundAlpha = Defaults.BackgroundAlpha;
    public float IconSize = Defaults.IconSize;

    /// <summary>Text scale for encounter/phase titles, mechanic names and notes in the overlay.</summary>
    public float TitleScale = Defaults.TitleScale;
    public float MechanicScale = Defaults.MechanicScale;

    /// <summary>Horizontal padding inside table cells, i.e. the gap between columns (pixels).</summary>
    public float ColumnPadding = Defaults.ColumnPadding;
    public float NotesScale = Defaults.NotesScale;
    public float NoteRowScale = Defaults.NoteRowScale;

    public const float MinScale = 0.5f, MaxScale = 3f;
    public const float MinIcon = 16f, MaxIcon = 96f;
    public bool OnlyCurrentJob = true;
    public bool OnlyDefensive = true;
    public bool ShowHeaders = Defaults.ShowHeaders;

    /// <summary>Switch to the encounter matching your duty and job when either changes.</summary>
    public bool AutoSwitch = Defaults.AutoSwitch;

    /// <summary>When auto-switch finds a matching encounter, also open the overlay if it is hidden.</summary>
    public bool AutoShowOverlay = Defaults.AutoShowOverlay;

    /// <summary>
    /// When you leave the duty (or there is no sheet for your job), hide the overlay again, but only if it was
    /// auto-switch that opened it. An overlay you opened yourself is left alone.
    /// </summary>
    public bool AutoHideOverlay = Defaults.AutoHideOverlay;

    /// <summary>
    /// Hide the overlay during cutscenes. Off by default, which keeps it on screen: Dalamud's own default is to
    /// hide plugin windows in cutscenes, so with this off the plugin turns that off for its windows.
    /// </summary>
    public bool HideDuringCutscenes = Defaults.HideDuringCutscenes;

    /// <summary>
    /// True: the overlay shows the whole encounter (its own rows, then every phase), with the current phase
    /// highlighted. False: it shows only the current phase. Independent of what is selected.
    /// </summary>
    public bool ShowFullEncounter = Defaults.ShowFullEncounter;

    /// <summary>Show the Reset / Previous / Next buttons under the overlay.</summary>
    public bool ShowControls = Defaults.ShowControls;

    public bool AutoSize = Defaults.AutoSize;
    public OverlayAnchor Anchor = Defaults.Anchor;

    /// <summary>With auto-size on, mechanic and notes text wrap at this width (pixels) so the overlay can't grow forever.</summary>
    public float MaxTextWidth = Defaults.MaxTextWidth;

    // Where each column's content sits in its cells.
    public HAlign MechanicHAlign = Defaults.MechanicHAlign;
    public VAlign MechanicVAlign = Defaults.MechanicVAlign;
    public HAlign MitigationHAlign = Defaults.MitigationHAlign;
    public VAlign MitigationVAlign = Defaults.MitigationVAlign;
    public HAlign NotesHAlign = Defaults.NotesHAlign;
    public VAlign NotesVAlign = Defaults.NotesVAlign;

    /// <summary>Version 1 stored a single flat list of rows. Kept only so it can be migrated.</summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<SheetRow> Rows = new();

    /// <summary>
    /// Settings saved by the plugin under its old name (MitigationSheet) record class and assembly names such as
    /// "MitigationSheet.SheetNode, MitigationSheet". Rewrites them to the current ones so the file can be read.
    /// </summary>
    internal static string RenameLegacyTypes(string json) => System.Text.RegularExpressions.Regex.Replace(
        json, "\"\\$type\":\\s*\"MitigationSheet\\.(\\w+), MitigationSheet\"", "\"$$type\": \"DutyNotes.$1, DutyNotes\"");

    /// <summary>
    /// The id of the single "Limit Break" skill. Not a game Action row (the game has a different limit break per job and
    /// tier); it is a number of our own, far outside the game's ids, and it is saved in sheets and export text, so it must never change.
    /// </summary>
    public const uint LimitBreakId = 4_000_000_001u;

    // For one day the plugin offered each job's limit break as its own skill, using these Action rows. They are folded into the one entry.
    private static readonly HashSet<uint> EarlierLimitBreakIds = new() { 197, 198, 199, 4240, 4241, 17105, 206, 207, 208, 4247, 4248, 24859 };

    private static void FoldLimitBreaks(SheetNode node)
    {
        foreach (var row in node.Rows)
        {
            if (row.IsNote || !row.Actions.Any(EarlierLimitBreakIds.Contains)) continue;
            row.Actions = row.Actions.Select(id => EarlierLimitBreakIds.Contains(id) ? LimitBreakId : id).Distinct().ToList();
        }
        foreach (var child in node.Children) FoldLimitBreaks(child);
    }

    public void Migrate()
    {
        if (Rows.Count > 0 && Roots.Count == 0)
        {
            Roots.Add(new SheetNode { Name = "Encounter 1", Children = { new SheetNode { Name = "Phase 1", Rows = Rows } } });
            Rows = new();
        }

        Roots = Roots.DistinctBy(n => n.Id).ToList();

        // Only two levels are supported (encounter > phase): hoist anything deeper up to phase level.
        foreach (var root in Roots)
        {
            var phases = new List<SheetNode>();
            foreach (var child in root.Children)
            {
                phases.Add(child);
                phases.AddRange(Flatten(child.Children));
                child.Children = new();
            }
            // Heal configs saved by a version that duplicated phases on every load: keep the first of each id.
            root.Children = phases.DistinctBy(n => n.Id).ToList();
        }

        foreach (var root in Roots) FoldLimitBreaks(root);

        // No encounters is a valid state (the editor and the overlay show an empty message). Nothing is selected then.
        if (Find(SelectedId) is null)
            SelectedId = Roots.Count > 0 ? Roots[0].Id : Guid.Empty;
    }

    // ---- Resetting display settings to their defaults ------------------------------------------------
    // Each Reset returns a closure that puts the previous values back, for undo.

    private static bool Same(float a, float b) => MathF.Abs(a - b) < 0.0005f;

    public bool IsBehaviorDefault =>
        AutoSwitch == Defaults.AutoSwitch && AutoShowOverlay == Defaults.AutoShowOverlay
        && AutoHideOverlay == Defaults.AutoHideOverlay && HideDuringCutscenes == Defaults.HideDuringCutscenes;

    public Action ResetBehavior()
    {
        var before = (AutoSwitch, AutoShowOverlay, AutoHideOverlay, HideDuringCutscenes);
        AutoSwitch = Defaults.AutoSwitch;
        AutoShowOverlay = Defaults.AutoShowOverlay;
        AutoHideOverlay = Defaults.AutoHideOverlay;
        HideDuringCutscenes = Defaults.HideDuringCutscenes;
        return () => (AutoSwitch, AutoShowOverlay, AutoHideOverlay, HideDuringCutscenes) = before;
    }

    /// <summary>
    /// What auto-switch should do with the overlay's visibility after a zone or job change.
    /// <paramref name="matched"/>: a sheet for this duty and job exists. <paramref name="visible"/>: the overlay is
    /// showing now. <paramref name="openedByUs"/>: auto-switch is what opened it.
    /// </summary>
    internal OverlayChange OverlayChangeFor(bool matched, bool visible, bool openedByUs)
    {
        if (!AutoSwitch) return OverlayChange.None;
        if (matched) return AutoShowOverlay && !visible ? OverlayChange.Show : OverlayChange.None;
        return AutoHideOverlay && openedByUs && visible ? OverlayChange.Hide : OverlayChange.None;
    }

    public bool IsLayoutDefault =>
        ShowFullEncounter == Defaults.ShowFullEncounter && ShowControls == Defaults.ShowControls
        && ShowHeaders == Defaults.ShowHeaders && AutoSize == Defaults.AutoSize && Anchor == Defaults.Anchor
        && Same(MaxTextWidth, Defaults.MaxTextWidth) && Same(ColumnPadding, Defaults.ColumnPadding)
        && MechanicHAlign == Defaults.MechanicHAlign && MechanicVAlign == Defaults.MechanicVAlign
        && MitigationHAlign == Defaults.MitigationHAlign && MitigationVAlign == Defaults.MitigationVAlign
        && NotesHAlign == Defaults.NotesHAlign && NotesVAlign == Defaults.NotesVAlign;

    public Action ResetLayout()
    {
        var before = (ShowFullEncounter, ShowControls, ShowHeaders, AutoSize, Anchor, MaxTextWidth, ColumnPadding,
            MechanicHAlign, MechanicVAlign, MitigationHAlign, MitigationVAlign, NotesHAlign, NotesVAlign);
        ShowFullEncounter = Defaults.ShowFullEncounter;
        ShowControls = Defaults.ShowControls;
        ShowHeaders = Defaults.ShowHeaders;
        AutoSize = Defaults.AutoSize;
        Anchor = Defaults.Anchor;
        MaxTextWidth = Defaults.MaxTextWidth;
        ColumnPadding = Defaults.ColumnPadding;
        MechanicHAlign = Defaults.MechanicHAlign; MechanicVAlign = Defaults.MechanicVAlign;
        MitigationHAlign = Defaults.MitigationHAlign; MitigationVAlign = Defaults.MitigationVAlign;
        NotesHAlign = Defaults.NotesHAlign; NotesVAlign = Defaults.NotesVAlign;
        return () => (ShowFullEncounter, ShowControls, ShowHeaders, AutoSize, Anchor, MaxTextWidth, ColumnPadding,
            MechanicHAlign, MechanicVAlign, MitigationHAlign, MitigationVAlign, NotesHAlign, NotesVAlign) = before;
    }

    public bool IsStyleDefault =>
        Same(BackgroundAlpha, Defaults.BackgroundAlpha) && Same(IconSize, Defaults.IconSize)
        && Same(TitleScale, Defaults.TitleScale) && Same(MechanicScale, Defaults.MechanicScale)
        && Same(NotesScale, Defaults.NotesScale) && Same(NoteRowScale, Defaults.NoteRowScale);

    public Action ResetStyle()
    {
        var before = (BackgroundAlpha, IconSize, TitleScale, MechanicScale, NotesScale, NoteRowScale);
        BackgroundAlpha = Defaults.BackgroundAlpha;
        IconSize = Defaults.IconSize;
        TitleScale = Defaults.TitleScale;
        MechanicScale = Defaults.MechanicScale;
        NotesScale = Defaults.NotesScale;
        NoteRowScale = Defaults.NoteRowScale;
        return () => (BackgroundAlpha, IconSize, TitleScale, MechanicScale, NotesScale, NoteRowScale) = before;
    }
    /// <summary>The encounter the overlay is on. Falls back to the first one if the stored one is gone.</summary>
    public SheetNode? ActiveEncounter => Roots.FirstOrDefault(r => r.Id == ActiveEncounterId) ?? Roots.FirstOrDefault();

    /// <summary>
    /// The phase the overlay shows while you play: the one you are on within the active encounter. A phase that is switched
    /// off is never it (the first phase that is on stands in), and it is null if the encounter has no phase that is on.
    /// </summary>
    public SheetNode? ActivePhase
    {
        get
        {
            var on = ActiveEncounter?.Children.Where(c => c.Enabled).ToList();
            if (on is null || on.Count == 0) return null;
            return on.FirstOrDefault(c => c.Id == ActivePhaseId) ?? on[0];
        }
    }

    /// <summary>
    /// Where the navigation points, even at a phase that is switched off (the editor can select one). The Previous and Next
    /// steps start from here. Null if the encounter has no phases.
    /// </summary>
    internal SheetNode? CurrentPhase
    {
        get
        {
            var phases = ActiveEncounter?.Children;
            if (phases is null || phases.Count == 0) return null;
            return phases.FirstOrDefault(c => c.Id == ActivePhaseId) ?? phases.FirstOrDefault(c => c.Enabled) ?? phases[0];
        }
    }

    /// <summary>Puts the overlay on an encounter, at its first phase that is on.</summary>
    public void SetActiveEncounter(SheetNode encounter)
    {
        ActiveEncounterId = encounter.Id;
        ActivePhaseId = encounter.Children.FirstOrDefault(c => c.Enabled)?.Id ?? Guid.Empty;
    }

    /// <summary>Follows the editor's selection: the overlay ends up on the encounter (and phase) you were editing.</summary>
    public void SetActiveFromSelection()
    {
        var selected = Selected;
        if (selected is null) return;

        var encounter = Roots.FirstOrDefault(r => r == selected || r.Children.Contains(selected));
        if (encounter is null) return;

        if (encounter.Id != ActiveEncounterId) SetActiveEncounter(encounter);
        if (selected != encounter) ActivePhaseId = selected.Id;
    }

    /// <summary>
    /// The phase a step leads to from the one you are on, or null if the active encounter has no phase that is on.
    /// Phases that are switched off are skipped.
    /// <list type="bullet">
    /// <item><b>Next</b> goes to the following phase that is on and stops at the last.</item>
    /// <item><b>Previous</b> goes to the phase that is on before and stops at the first.</item>
    /// <item><b>Reset</b> goes to the first phase that is on.</item>
    /// </list>
    /// The result can be the phase you are already on, which is how the buttons know to grey out.
    /// </summary>
    internal SheetNode? PhaseTarget(PhaseStep step, bool fromEncounter = false)
    {
        var phases = ActiveEncounter?.Children;
        var on = phases?.Where(p => p.Enabled).ToList();
        if (phases is null || on is null || on.Count == 0) return null;

        // Looking at the encounter itself (selected in the editor) rather than at a phase: the way in is the
        // first phase that is on, and there is nothing before it.
        if (fromEncounter) return step == PhaseStep.Previous ? null : on[0];

        // Start from where the navigation is, even if that phase is off (the editor can select one), and stay
        // there when there is no phase that is on further along.
        var current = CurrentPhase!;
        var index = phases.IndexOf(current);
        return step switch
        {
            PhaseStep.Reset => on[0],
            PhaseStep.Next => on.FirstOrDefault(p => phases.IndexOf(p) > index) ?? current,
            _ => on.LastOrDefault(p => phases.IndexOf(p) < index) ?? current,
        };
    }
    public SheetNode? Selected => Find(SelectedId);

    public SheetNode? Find(Guid id) => Find(Roots, id);

    private static SheetNode? Find(List<SheetNode> nodes, Guid id)
    {
        foreach (var n in nodes)
        {
            if (n.Id == id) return n;
            if (Find(n.Children, id) is { } hit) return hit;
        }
        return null;
    }

    /// <summary>The list that directly contains <paramref name="node"/>, or null if it is not in the tree.</summary>
    public List<SheetNode>? ContainingList(SheetNode node) => ContainingList(Roots, node);

    /// <summary>Moves the item at <paramref name="from"/> so that it ends up at index <paramref name="to"/>, the others shifting along.
    /// Returns false, changing nothing, when either index is outside the list or they are the same.</summary>
    public static bool MoveWithin<T>(List<T> list, int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= list.Count || to >= list.Count) return false;

        var item = list[from];
        list.RemoveAt(from);
        list.Insert(to, item);
        return true;
    }

    private static List<SheetNode>? ContainingList(List<SheetNode> nodes, SheetNode node)
    {
        if (nodes.Contains(node)) return nodes;
        foreach (var n in nodes)
            if (ContainingList(n.Children, node) is { } hit) return hit;
        return null;
    }

    /// <summary>
    /// Deletes an encounter or a phase and moves the selection. Returns what undoes it, or null if the sheet is not in the tree.
    /// The selection goes to the sheet that took its place in the list (the next one, or the one before if it was last). When a
    /// phase was the last one in its encounter, that is its own encounter, never some other encounter. Deleting the last
    /// encounter is allowed and leaves nothing selected.
    /// </summary>
    internal Action? RemoveNode(SheetNode node)
    {
        var list = ContainingList(node);
        if (list is null) return null;

        // The encounter a phase belongs to, found before it is removed.
        var parent = Roots.FirstOrDefault(r => r.Children.Contains(node));

        var index = list.IndexOf(node);
        list.RemoveAt(index);
        SelectedId = list.Count > 0 ? list[Math.Min(index, list.Count - 1)].Id
            : parent is not null ? parent.Id
            : Roots.Count > 0 ? Roots[0].Id : Guid.Empty;

        return () =>
        {
            list.Insert(Math.Min(index, list.Count), node);
            SelectedId = node.Id;
        };
    }

    public string PathOf(SheetNode node)
    {
        var parts = new List<string>();
        return Trace(Roots, node, parts) ? string.Join(" > ", parts) : node.Name;
    }

    private static bool Trace(List<SheetNode> nodes, SheetNode target, List<string> parts)
    {
        foreach (var n in nodes)
        {
            parts.Add(n.Name);
            if (n == target || Trace(n.Children, target, parts)) return true;
            parts.RemoveAt(parts.Count - 1);
        }
        return false;
    }

    public IEnumerable<SheetNode> Flatten() => Flatten(Roots);

    private static IEnumerable<SheetNode> Flatten(List<SheetNode> nodes)
    {
        foreach (var n in nodes)
        {
            yield return n;
            foreach (var c in Flatten(n.Children)) yield return c;
        }
    }
}
