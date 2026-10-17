using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace DutyNotes;

/// <summary>What came out of an import: the encounter, or why there isn't one, plus anything that was dropped.</summary>
public sealed class ImportResult
{
    public SheetNode? Encounter { get; init; }
    public string? Error { get; init; }

    /// <summary>Skills in the text that don't exist in this game version and were left out.</summary>
    public int DroppedSkills { get; init; }

    /// <summary>The encounter named a duty this game version doesn't know, so it was imported without one.</summary>
    public bool DutyCleared { get; init; }

    public int Phases { get; init; }
    public int Rows { get; init; }
}

/// <summary>
/// Turns an encounter into a piece of text that can be pasted anywhere (chat, Discord, a file) and back again.
///
/// The text is "DutyNotes:1:" followed by gzipped JSON in base64. It uses its own small format and not the
/// settings classes, so the settings can change without breaking text that other people already have. Importing
/// treats the text as untrusted: sizes are capped, every value is checked and clamped, and the JSON is read without
/// any type information, so it can only ever produce the plain data defined below.
/// </summary>
public static class Sharing
{
    private const string Prefix = "DutyNotes:1:";
    private const string NewerVersion = "That text was made by a newer version of Duty Notes. Update the plugin to import it.";

    // Limits for imported text. Far above anything real, far below anything that could hurt.
    private const int MaxTextLength = 400_000;
    private const int MaxJsonBytes = 1_000_000;
    private const int MaxPhases = 60;
    private const int MaxRowsPerSheet = 400;
    private const int MaxRowsTotal = 4000;
    private const int MaxSkillsPerRow = 24;
    private const int MaxNameLength = 128;
    private const int MaxMechanicLength = 128;
    private const int MaxNotesLength = 256;
    private const int MaxTextRowLength = 1000;

    // ---- The wire format -----------------------------------------------------------------------

    internal sealed class SheetDto
    {
        [JsonProperty("n")] public string? Name;
        [JsonProperty("l")] public int Level = 100;
        [JsonProperty("d")] public uint Duty;
        [JsonProperty("j")] public uint Job;
        [JsonProperty("r")] public List<RowDto>? Rows;
        [JsonProperty("p")] public List<SheetDto>? Phases;

        // A phase that is switched off ("x": true). Stored as "off", not "on", because export leaves out default values and the
        // default of a bool is false: text without the key (all text from before it existed) has every phase on, and only a phase
        // that is off is mentioned.
        [JsonProperty("x")] public bool Off;
    }

    internal sealed class RowDto
    {
        // A note row has "t" (its text); a mechanic row has the rest.
        [JsonProperty("t")] public string? Text;
        [JsonProperty("m")] public string? Mechanic;
        [JsonProperty("s")] public List<uint>? Skills;
        [JsonProperty("o")] public string? Notes;

        // A row that is switched off ("x": true); stored as "off" for the same reason as on a phase (see SheetDto.Off).
        [JsonProperty("x")] public bool Off;
    }

    private sealed class PackDto
    {
        [JsonProperty("v")] public int Version = 1;
        [JsonProperty("e")] public SheetDto? Encounter;
    }

    // ---- Export --------------------------------------------------------------------------------

    public static string Export(SheetNode encounter)
    {
        var pack = new PackDto { Encounter = ToDto(encounter, includePhases: true) };
        var json = JsonConvert.SerializeObject(pack, Formatting.None, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
        });

        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            gzip.Write(Encoding.UTF8.GetBytes(json));

        return Prefix + Convert.ToBase64String(output.ToArray());
    }

    internal static SheetDto ToDto(SheetNode node, bool includePhases) => new()
    {
        Name = node.Name,
        Level = node.Level,
        Duty = node.DutyId,
        Job = node.JobId,
        Off = !node.Enabled,
        Rows = node.Rows.Select(r => r.IsNote
            ? new RowDto { Text = r.Text, Off = !r.Enabled }
            : new RowDto { Mechanic = r.Mechanic, Skills = r.Actions.ToList(), Notes = r.Notes, Off = !r.Enabled }).ToList(),
        Phases = includePhases ? node.Children.Select(c => ToDto(c, includePhases: false)).ToList() : null,
    };

    // ---- Import --------------------------------------------------------------------------------

    /// <param name="skillExists">Whether a skill id exists in this game version.</param>
    /// <param name="findDuty">The zone and name for a duty id, or null if this game version has no such duty.</param>
    /// <param name="jobIsKnown">Whether a job id is one the plugin offers.</param>
    public static ImportResult Import(
        string text,
        Func<uint, bool> skillExists,
        Func<uint, (uint Territory, string Name)?> findDuty,
        Func<uint, bool> jobIsKnown)
    {
        text = (text ?? string.Empty).Trim();
        if (text.Length == 0) return Fail("The clipboard is empty.");
        if (text.Length > MaxTextLength) return Fail("That text is too long to be a Duty Notes encounter.");
        if (!text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // "DutyNotes:<number>:" with a version this plugin doesn't know is Duty Notes text from a newer plugin,
            // not text from somewhere else, and the person needs to be told to update, not that it looks wrong.
            if (Regex.Match(text, @"^DutyNotes:(\d+):", RegexOptions.CultureInvariant) is { Success: true } header
                && (!int.TryParse(header.Groups[1].Value, out var version) || version > 1))
                return Fail(NewerVersion);

            return Fail("That doesn't look like Duty Notes text. It should start with \"DutyNotes:1:\".");
        }

        byte[] compressed;
        try
        {
            compressed = Convert.FromBase64String(text[Prefix.Length..]);
        }
        catch (FormatException)
        {
            return Fail("The text is damaged (it may have been cut off when it was copied).");
        }

        string json;
        try
        {
            json = Decompress(compressed);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
        {
            return Fail("The text is damaged or isn't a Duty Notes encounter.");
        }

        PackDto? pack;
        try
        {
            // Plain settings: no type names are read, so the JSON can only fill in the classes above.
            pack = JsonConvert.DeserializeObject<PackDto>(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None });
        }
        catch (JsonException)
        {
            return Fail("The text is damaged or isn't a Duty Notes encounter.");
        }

        if (pack?.Encounter is null) return Fail("There is no encounter in that text.");
        if (pack.Version != 1) return Fail(NewerVersion);

        var phases = pack.Encounter.Phases ?? new List<SheetDto>();
        if (phases.Count > MaxPhases) return Fail("That encounter has too many phases to be real.");

        var totalRows = (pack.Encounter.Rows?.Count ?? 0) + phases.Sum(p => p.Rows?.Count ?? 0);
        if (totalRows > MaxRowsTotal) return Fail("That encounter has too many rows to be real.");
        if ((pack.Encounter.Rows?.Count ?? 0) > MaxRowsPerSheet || phases.Any(p => (p.Rows?.Count ?? 0) > MaxRowsPerSheet))
            return Fail("A sheet in that encounter has too many rows to be real.");

        var converter = new Converter(skillExists, findDuty, jobIsKnown);
        var encounter = converter.Build(pack.Encounter, isEncounter: true);
        foreach (var phase in phases) encounter.Children.Add(converter.Build(phase, isEncounter: false));

        return new ImportResult
        {
            Encounter = encounter,
            DroppedSkills = converter.DroppedSkills,
            DutyCleared = converter.DutyCleared,
            Phases = encounter.Children.Count,
            Rows = encounter.Rows.Count + encounter.Children.Sum(c => c.Rows.Count),
        };
    }

    /// <summary>
    /// Builds real sheets from untrusted data: every value is checked, clamped or cut, and things this game version
    /// doesn't know (skills, duties, jobs) are dropped. Used by import.
    /// </summary>
    internal sealed class Converter
    {
        private readonly Func<uint, bool> skillExists;
        private readonly Func<uint, (uint Territory, string Name)?> findDuty;
        private readonly Func<uint, bool> jobIsKnown;

        public Converter(Func<uint, bool> skillExists, Func<uint, (uint Territory, string Name)?> findDuty, Func<uint, bool> jobIsKnown)
        {
            this.skillExists = skillExists;
            this.findDuty = findDuty;
            this.jobIsKnown = jobIsKnown;
        }

        public int DroppedSkills { get; private set; }
        public int DutiesCleared { get; private set; }
        public bool DutyCleared => DutiesCleared > 0;

        public SheetNode Build(SheetDto dto, bool isEncounter)
        {
            var node = new SheetNode
            {
                Name = Clip(dto.Name, MaxNameLength) is { Length: > 0 } name ? name : (isEncounter ? "Imported encounter" : "Phase"),
                Level = Math.Clamp(dto.Level, 1, 100),
                Enabled = isEncounter || !dto.Off,
            };

            // Duty and job only apply to an encounter. The zone comes from this game's own data, never from the text.
            if (isEncounter)
            {
                if (dto.Duty != 0)
                {
                    if (findDuty(dto.Duty) is { } duty)
                    {
                        node.DutyId = dto.Duty;
                        node.DutyTerritoryId = duty.Territory;
                        node.DutyName = duty.Name;
                    }
                    else
                    {
                        DutiesCleared++;
                    }
                }

                if (dto.Job != 0 && jobIsKnown(dto.Job)) node.JobId = dto.Job;
            }

            foreach (var row in dto.Rows ?? new List<RowDto>())
            {
                if (row.Text is not null)
                {
                    node.Rows.Add(new SheetRow { IsNote = true, Text = Clip(row.Text, MaxTextRowLength), Enabled = !row.Off });
                    continue;
                }

                var skills = new List<uint>();
                foreach (var id in (row.Skills ?? new List<uint>()).Take(MaxSkillsPerRow))
                {
                    if (skillExists(id) && !skills.Contains(id)) skills.Add(id);
                    else DroppedSkills++;
                }

                node.Rows.Add(new SheetRow
                {
                    Mechanic = Clip(row.Mechanic, MaxMechanicLength),
                    Actions = skills,
                    Notes = Clip(row.Notes, MaxNotesLength),
                    Enabled = !row.Off,
                });
            }

            return node;
        }
    }

    private static ImportResult Fail(string error) => new() { Error = error };

    /// <summary>Strips control characters other than line breaks, and cuts the text to a length.</summary>
    internal static string Clip(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var cleaned = new string(value.Where(c => c == '\n' || !char.IsControl(c)).ToArray());
        return cleaned.Length > max ? cleaned[..max] : cleaned;
    }

    /// <summary>Unzips, refusing to produce more than <see cref="MaxJsonBytes"/> (a tiny zip can expand to gigabytes).</summary>
    private static string Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        var buffer = new byte[16 * 1024];
        int read;
        while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + read > MaxJsonBytes) throw new InvalidOperationException("Too large once unpacked.");
            output.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }
}
