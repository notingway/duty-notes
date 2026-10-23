using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace DutyNotes;

/// <summary>
/// The card shown when you hover a skill in the overlay: the skill as it is at the encounter's level, not at yours. Outside a duty the game
/// only ever describes your max-level skill, which hides what a skill does at the level a duty syncs you to. The description is answered
/// for that level (<see cref="SyncedText"/>), and the recast and charges are lowered or raised by the traits learned by then.
/// </summary>
public sealed class ActionTooltip
{
    // Widths and sizes at 100% UI scale.
    private const float WrapWidth = 360f;
    private const float IconSize = 40f;
    private const float StatSpacing = 14f;

    private sealed record Card(string Category, string Job, int Level, bool NotLearned, string Cast, string Recast, int Charges, string? Range,
        string? Radius, int Mp, ReadOnlySeString Description);

    private readonly IDataManager data;
    private readonly ISeStringEvaluator evaluator;
    private readonly ActionCatalog catalog;
    private readonly JobCatalog jobs;
    private readonly Dictionary<(uint Action, int Level, uint Job), Card?> cards = new();

    // What traits do to skills, by skill name: (trait level, the class or job that learns it, 0 for any, the new value).
    private readonly Dictionary<string, List<(int Level, uint Job, float Seconds)>> recastTraits = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<(int Level, uint Job, int Charges)>> chargeTraits = new(StringComparer.OrdinalIgnoreCase);

    public ActionTooltip(IDataManager data, ISeStringEvaluator evaluator, ActionCatalog catalog, JobCatalog jobs)
    {
        this.data = data;
        this.evaluator = evaluator;
        this.catalog = catalog;
        this.jobs = jobs;

        var transients = data.GetExcelSheet<TraitTransient>();
        foreach (var trait in data.GetExcelSheet<Trait>())
        {
            if (trait.Level == 0 || !transients.TryGetRow(trait.RowId, out var tt)) continue;
            var text = tt.Description.ExtractText();
            foreach (var (action, seconds) in SyncedText.RecastsSetBy(text))
                Add(recastTraits, action, (trait.Level, trait.ClassJob.RowId, seconds));
            foreach (var (action, charges) in SyncedText.ChargesSetBy(text))
                Add(chargeTraits, action, (trait.Level, trait.ClassJob.RowId, charges));
        }
    }

    private static void Add<T>(Dictionary<string, List<T>> map, string key, T value)
    {
        if (!map.TryGetValue(key, out var list)) map[key] = list = new();
        list.Add(value);
    }

    /// <summary>Call while the skill's icon is hovered.</summary>
    /// <param name="level">The encounter's level.</param>
    /// <param name="encounterJob">The encounter's job (ClassJob row), or 0 for any.</param>
    public void Draw(ActionInfo info, int level, uint encounterJob)
    {
        var job = JobFor(info, encounterJob);
        var key = (info.Id, level, job);
        if (!cards.TryGetValue(key, out var card)) cards[key] = card = Build(info, level, job);

        // The limit break is not a real skill, and a skill the game data has no row for has nothing more to say than its name.
        if (card is null)
        {
            ImGui.SetTooltip(info.Name);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        ImGui.BeginTooltip();

        // Header: the icon, then the name over what kind of skill it is.
        var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(info.Icon)).GetWrapOrEmpty();
        ImGui.Image(icon.Handle, new Vector2(IconSize * scale));
        ImGui.SameLine();
        ImGui.BeginGroup();
        var textHeight = ImGui.GetTextLineHeightWithSpacing() + ImGui.GetTextLineHeight();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + Math.Max(0f, (IconSize * scale - textHeight) / 2f));
        ImGui.TextUnformatted(info.Name);
        ImGui.TextDisabled(card.Category.Length > 0 ? $"{card.Category}, {card.Job}" : card.Job);
        ImGui.EndGroup();

        // The numbers, in one line of label and value pairs, like the game's tooltip has them at the top.
        ImGui.Spacing();
        var first = true;
        void Stat(string label, string value)
        {
            if (!first) ImGui.SameLine(0f, StatSpacing * scale);
            first = false;
            ImGui.TextDisabled(label);
            ImGui.SameLine(0f, 4f * scale);
            ImGui.TextUnformatted(value);
        }
        Stat("Cast", card.Cast);
        Stat("Recast", card.Recast);
        if (card.Charges > 1) Stat("Charges", card.Charges.ToString(CultureInfo.InvariantCulture));
        if (card.Range is not null) Stat("Range", card.Range);
        if (card.Radius is not null) Stat("Radius", card.Radius);
        if (card.Mp > 0) Stat("MP", card.Mp.ToString(CultureInfo.InvariantCulture));

        if (!card.Description.IsEmpty)
        {
            ImGui.Separator();
            ImGuiHelpers.SeStringWrapped(card.Description.Data.Span, new SeStringDrawParams { WrapWidth = WrapWidth * scale });
        }

        // Footer: which level and job this is for, so the card can't be mistaken for your own skill.
        ImGui.Separator();
        if (card.NotLearned)
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + WrapWidth * scale);
            ImGui.TextColored(ImGuiColors.DalamudOrange, $"Not learned until level {card.Level}, above the encounter's level {level}. Shown as at level {card.Level}.");
            ImGui.PopTextWrapPos();
        }
        else
        {
            ImGui.TextDisabled($"As at level {card.Level}, on {card.Job}");
        }

        ImGui.EndTooltip();
    }

    /// <summary>
    /// The job to describe the skill for: the encounter's, if it can use the skill; else yours, if you can; else the first job that can.
    /// It matters for role actions (Reprisal lasts longer for tanks) and for the traits of the job that owns a skill.
    /// </summary>
    private uint JobFor(ActionInfo info, uint encounterJob)
    {
        if (encounterJob != 0 && catalog.UsableBy(info, jobs.Abbreviation(encounterJob))) return encounterJob;

        var current = Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        if (current != 0 && catalog.UsableBy(info, jobs.Abbreviation(current))) return current;

        foreach (var (_, ids) in JobCatalog.Groups)
            foreach (var id in ids)
                if (catalog.UsableBy(info, jobs.Abbreviation(id))) return id;
        return 0;
    }

    private Card? Build(ActionInfo info, int level, uint job)
    {
        if (!data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(info.Id, out var action)) return null;

        // A skill above the encounter's level can't be used there; describe it as it is when it is learned, and say so.
        var notLearned = info.Level > level;
        var shownAt = notLearned ? info.Level : level;

        // A trait learned by the class a job starts as (Marauder for Warrior) counts for the job too.
        var parent = data.GetExcelSheet<ClassJob>().TryGetRow(job, out var cj) ? cj.ClassJobParent.RowId : 0;
        bool Applies(int traitLevel, uint traitJob) => traitLevel <= shownAt && (traitJob == 0 || traitJob == job || traitJob == parent);

        var recast = action.Recast100ms / 10f;
        if (recastTraits.TryGetValue(info.Name, out var recasts))
            foreach (var t in recasts.Where(t => Applies(t.Level, t.Job)))
                recast = Math.Min(recast, t.Seconds);

        int charges = action.MaxCharges;
        if (chargeTraits.TryGetValue(info.Name, out var chargeList))
            foreach (var t in chargeList.Where(t => Applies(t.Level, t.Job)))
                charges = Math.Max(charges, t.Charges);

        ReadOnlySeString description = default;
        if (data.GetExcelSheet<ActionTransient>().TryGetRow(info.Id, out var transient))
        {
            // What is left after our answers (colours, switches on anything else) is filled in by Dalamud, as the game would.
            var synced = SyncedText.AtLevel(transient.Description.AsSpan(), shownAt, job);
            description = evaluator.Evaluate(synced);
        }

        // Range -1 is melee range, which the game shows as 3 yalms.
        string? range = action.Range switch { > 0 => $"{action.Range}y", -1 => "3y", _ => null };
        string? radius = action.EffectRange > 0 ? $"{action.EffectRange}y" : null;

        // Cost type 3 is MP, stored in hundreds.
        var mp = action.PrimaryCostType == 3 ? action.PrimaryCostValue * 100 : 0;

        return new Card(
            Category: action.ActionCategory.ValueNullable?.Name.ExtractText() ?? string.Empty,
            Job: job != 0 ? jobs.Abbreviation(job) : info.Job,
            Level: shownAt,
            NotLearned: notLearned,
            Cast: action.Cast100ms == 0 ? "Instant" : Seconds(action.Cast100ms / 10f),
            Recast: Seconds(recast),
            Charges: charges,
            Range: range,
            Radius: radius,
            Mp: mp,
            Description: description);
    }

    private static string Seconds(float s) => s.ToString("0.##", CultureInfo.InvariantCulture) + "s";
}
