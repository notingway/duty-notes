using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace DutyNotes;

public sealed record ActionInfo(uint Id, string Name, ushort Icon, string Job, int Level, uint CategoryId);

/// <summary>A skill and every version it upgrades into (e.g. Raw Intuition -> Bloodwhetting).</summary>
public sealed class SkillFamily
{
    /// <summary>Ascending by unlock level.</summary>
    public required List<ActionInfo> Versions { get; init; }
    public bool Defensive { get; init; }
    public string Job => Versions[0].Job;

    /// <summary>The version a character of this level actually has, or null if none is unlocked yet.</summary>
    public ActionInfo? At(int level)
    {
        ActionInfo? result = null;
        foreach (var v in Versions)
            if (v.Level <= level) result = v;
        return result;
    }
}

/// <summary>Player/role actions from the game data that can be picked as mitigation.</summary>
public sealed partial class ActionCatalog
{
    private readonly Dictionary<uint, ActionInfo> byId = new();
    private readonly CategoryLookup categories;
    private readonly Dictionary<(uint category, string job), bool> usableCache = new();

    // Limit breaks are not player actions in the game data (no unlock level, no class category, many rows with the same
    // generic icon), and which one you have depends on your job. They are offered as one skill, "Limit Break", with a
    // fixed id of its own (Configuration.LimitBreakId) and the game's generic limit break icon.
    private const string LimitBreakName = "Limit Break";
    private const ushort LimitBreakIcon = 103;

    public IReadOnlyList<SkillFamily> Families { get; }

    public ActionCatalog(IDataManager data)
    {
        categories = new CategoryLookup(data.GetExcelSheet<ClassJobCategory>());

        var list = new List<ActionInfo>();
        foreach (var a in data.GetExcelSheet<Lumina.Excel.Sheets.Action>())
        {
            // The sheet is full of unused/legacy rows with the same names. The real, usable
            // skills are the ones that have an unlock level and a class category.
            if (a.IsPvP || a.Icon == 0) continue;
            if (!a.IsPlayerAction && !a.IsRoleAction) continue;
            if (a.ClassJobLevel == 0 || a.ClassJobCategory.RowId == 0) continue;

            var name = a.Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;

            // Role actions are not tied to a single job, they use the category only.
            var job = a.IsRoleAction || a.ClassJob.ValueNullable is not { } cj
                ? "Role"
                : cj.Abbreviation.ToString();

            var info = new ActionInfo(a.RowId, name, a.Icon, job, a.ClassJobLevel, a.ClassJobCategory.RowId);
            list.Add(info);
            byId[a.RowId] = info;
        }

        var limitBreak = new ActionInfo(Configuration.LimitBreakId, LimitBreakName, LimitBreakIcon, "Role", 1, 0);
        list.Add(limitBreak);
        byId[limitBreak.Id] = limitBreak;

        Families = BuildFamilies(list, data);
    }

    /// <summary>
    /// Traits describe upgrades as "Upgrades X to Y." Use that to chain the versions of a skill together,
    /// so searching for any version finds the one you have at your level.
    /// </summary>
    private static List<SkillFamily> BuildFamilies(List<ActionInfo> actions, IDataManager data)
    {
        var parent = Enumerable.Range(0, actions.Count).ToArray();
        int Root(int i) => parent[i] == i ? i : parent[i] = Root(parent[i]);
        void Union(int a, int b) => parent[Root(a)] = Root(b);

        var byName = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < actions.Count; i++)
        {
            if (!byName.TryGetValue(actions[i].Name, out var l)) byName[actions[i].Name] = l = new();
            l.Add(i);
        }

        // Skills sharing a name (rare duplicates) are the same skill.
        foreach (var l in byName.Values)
            for (var k = 1; k < l.Count; k++) Union(l[0], l[k]);

        var transients = data.GetExcelSheet<TraitTransient>();
        foreach (var trait in data.GetExcelSheet<Trait>())
        {
            if (!transients.TryGetRow(trait.RowId, out var tt)) continue;
            foreach (Match m in UpgradeRegex().Matches(tt.Description.ToString()))
            {
                if (byName.TryGetValue(m.Groups["a"].Value.Trim(), out var from)
                    && byName.TryGetValue(m.Groups["b"].Value.Trim(), out var to))
                    Union(from[0], to[0]);
            }
        }

        return Enumerable.Range(0, actions.Count)
            .GroupBy(Root)
            .Select(g =>
            {
                var versions = g.Select(i => actions[i]).OrderBy(v => v.Level).ThenBy(v => v.Id).ToList();
                return new SkillFamily
                {
                    Versions = versions,
                    Defensive = versions.Any(v => DefensiveSkills.Includes(v.Name)),
                };
            })
            .OrderBy(f => f.Job).ThenBy(f => f.Versions[0].Level).ThenBy(f => f.Versions[0].Name)
            .ToList();
    }

    [GeneratedRegex(@"Upgrades (?<a>.+?) to (?<b>.+?)(?:\.|\r|\n|$)")]
    private static partial Regex UpgradeRegex();

    public ActionInfo? Get(uint id) => byId.GetValueOrDefault(id);

    /// <summary>Whether the job with this abbreviation (e.g. "WAR") can use the action.</summary>
    public bool UsableBy(ActionInfo info, string jobAbbreviation)
    {
        if (info.Id == Configuration.LimitBreakId) return true; // every job has a limit break

        var key = (info.CategoryId, jobAbbreviation);
        if (usableCache.TryGetValue(key, out var cached)) return cached;
        return usableCache[key] = categories.Includes(info.CategoryId, jobAbbreviation);
    }

    /// <summary>Reads the per-job boolean columns (WAR, PLD, ...) of ClassJobCategory by name.</summary>
    private sealed class CategoryLookup
    {
        private readonly Lumina.Excel.ExcelSheet<ClassJobCategory> sheet;
        private readonly Dictionary<string, System.Reflection.PropertyInfo?> props = new();

        public CategoryLookup(Lumina.Excel.ExcelSheet<ClassJobCategory> sheet) => this.sheet = sheet;

        public bool Includes(uint categoryId, string job)
        {
            if (!sheet.TryGetRow(categoryId, out var row)) return false;
            if (!props.TryGetValue(job, out var prop))
                props[job] = prop = typeof(ClassJobCategory).GetProperty(job);
            return prop?.PropertyType == typeof(bool) && (bool)prop.GetValue(row)!;
        }
    }
}
