using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace DutyNotes;

/// <summary>The jobs you can pick for an encounter, grouped by role the way the game lists them.</summary>
public sealed class JobCatalog
{
    /// <summary>ClassJob rows by role. Base classes and Blue Mage are left out on purpose.</summary>
    public static readonly (string Role, uint[] Ids)[] Groups =
    {
        ("Tanks", new uint[] { 19, 21, 32, 37 }),               // PLD WAR DRK GNB
        ("Healers", new uint[] { 24, 28, 33, 40 }),             // WHM SCH AST SGE
        ("Melee", new uint[] { 20, 22, 30, 34, 39, 41 }),       // MNK DRG NIN SAM RPR VPR
        ("Physical ranged", new uint[] { 23, 31, 38 }),         // BRD MCH DNC
        ("Casters", new uint[] { 25, 27, 35, 42 }),             // BLM SMN RDM PCT
    };

    private readonly Dictionary<uint, string> abbreviations = new();

    public JobCatalog(IDataManager data)
    {
        foreach (var job in data.GetExcelSheet<ClassJob>())
        {
            var abbreviation = job.Abbreviation.ToString();
            if (string.IsNullOrWhiteSpace(abbreviation)) continue;
            abbreviations[job.RowId] = abbreviation;
        }
    }

    /// <summary>Three letter abbreviation such as "WAR", or an empty string for no job.</summary>
    public string Abbreviation(uint jobId) => abbreviations.GetValueOrDefault(jobId, string.Empty);

    /// <summary>The job icon the game uses in lists (the plain, unframed one).</summary>
    public static uint IconId(uint jobId) => 62100 + jobId;
}
