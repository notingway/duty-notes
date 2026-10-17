using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace DutyNotes;

public sealed record DutyInfo(uint Id, string Name, uint TerritoryId, uint TypeId, string TypeName, int Expansion, int Level);

/// <summary>
/// The duties you could pick for an encounter, from the same game data Party Finder uses.
/// Only the kinds of content a duty sheet makes sense for are included.
/// </summary>
public sealed class DutyCatalog
{
    // Dungeons, Trials, Raids, Deep Dungeons, Ultimate Raids, Save the Queen, V&C Dungeon Finder,
    // Chaotic Alliance Raid, Occult Crescent.
    private static readonly HashSet<uint> ContentTypes = new() { 2, 4, 5, 21, 28, 29, 30, 37, 38 };

    public IReadOnlyList<DutyInfo> All { get; }

    /// <summary>Expansion names indexed by expansion number (0 = A Realm Reborn).</summary>
    public IReadOnlyList<string> Expansions { get; }

    /// <summary>The content types that actually appear in <see cref="All"/>, as (id, name, Party Finder icon).</summary>
    public IReadOnlyList<(uint Id, string Name, uint Icon)> Types { get; }

    private readonly Dictionary<uint, DutyInfo> byId;

    public DutyCatalog(IDataManager data)
    {
        Expansions = data.GetExcelSheet<ExVersion>().Select(e => e.Name.ToString()).ToList();
        var typeSheet = data.GetExcelSheet<ContentType>();

        var list = new List<DutyInfo>();
        foreach (var d in data.GetExcelSheet<ContentFinderCondition>())
        {
            var name = d.Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;

            // The data stores "the Epic of Alexander" in lowercase because it is used mid-sentence in game
            // text; the game capitalizes the first letter when it shows the name on its own, so do the same.
            name = char.ToUpper(name[0]) + name[1..];
            if (!ContentTypes.Contains(d.ContentType.RowId)) continue;
            if (d.TerritoryType.RowId == 0) continue;

            var typeName = typeSheet.TryGetRow(d.ContentType.RowId, out var t) ? t.Name.ToString() : string.Empty;
            var expansion = d.TerritoryType.ValueNullable is { } territory ? (int)territory.ExVersion.RowId : 0;

            list.Add(new DutyInfo(d.RowId, name, d.TerritoryType.RowId, d.ContentType.RowId, typeName, expansion, d.ClassJobLevelRequired));
        }

        // Newest expansion first, then highest level, then by name.
        All = list.OrderByDescending(d => d.Expansion).ThenByDescending(d => d.Level).ThenBy(d => d.Name).ToList();
        byId = All.ToDictionary(d => d.Id);
        // The icon Party Finder shows for the category.
        uint IconOf(uint typeId) => typeSheet.TryGetRow(typeId, out var type) ? (uint)type.IconDutyFinder : 0;
        Types = All.GroupBy(d => d.TypeId).Select(g => (g.Key, g.First().TypeName, IconOf(g.Key))).OrderBy(t => t.Key).ToList();
    }

    public DutyInfo? Get(uint id) => byId.GetValueOrDefault(id);

    /// <summary>The Party Finder icon of a content type, or 0 if it has none.</summary>
    public uint TypeIcon(uint typeId) => Types.FirstOrDefault(t => t.Id == typeId).Icon;

    public string ExpansionName(int expansion) => expansion >= 0 && expansion < Expansions.Count ? Expansions[expansion] : "?";
}
