using System.IO.Compression;
using System.Text;

namespace DutyNotes.Tests;

public sealed class SharingTests : CheckBase
{
    // Stand-ins for the game's data: every skill and job exists, and every duty id n is in zone n * 10.
    private static ImportResult Import(string text, Func<uint, bool>? skill = null, Func<uint, (uint, string)?>? duty = null, Func<uint, bool>? job = null)
        => Sharing.Import(text, skill ?? (_ => true), duty ?? (id => (id * 10, "Duty " + id)), job ?? (_ => true));

    /// <summary>The JSON inside an export text.</summary>
    private static string Gunzip(string text)
    {
        using var input = new MemoryStream(Convert.FromBase64String(text["DutyNotes:1:".Length..]));
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Wraps JSON the way an export does, so a test can hand-craft the contents.</summary>
    private static string Pack(string json, string prefix = "DutyNotes:1:")
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            gzip.Write(Encoding.UTF8.GetBytes(json));
        return prefix + Convert.ToBase64String(output.ToArray());
    }

    private static string Export(out SheetNode original)
    {
        original = new SheetNode { Name = "TEA WAR MT", Level = 80, DutyId = 694, DutyTerritoryId = 887, DutyName = "The Epic of Alexander (Ultimate)", JobId = 21 };
        for (var i = 1; i <= 4; i++)
        {
            var phase = new SheetNode { Name = $"Phase {i}", Level = 80 };
            if (i == 1)
            {
                phase.Rows.Add(new SheetRow { Mechanic = "Raidwide", Actions = { 7531, 7535 }, Notes = "Every pull" });
                phase.Rows.Add(new SheetRow { IsNote = true, Text = "Stack on the marker\nthen spread" });
            }
            else
            {
                phase.Rows.Add(new SheetRow { Mechanic = $"Mech {i}", Actions = { 3551, 25751 } });
                phase.Rows.Add(new SheetRow { IsNote = true, Text = $"Note {i}" });
            }
            original.Children.Add(phase);
        }
        return Sharing.Export(original);
    }

    [Fact]
    public void AnEncounterSurvivesExportAndImport()
    {
        var text = Export(out var original);
        Check("an export is short enough to paste anywhere", text.Length < 1000, text.Length + " characters");
        Check("an export carries the format and its version", text.StartsWith("DutyNotes:1:"));

        var result = Import(text);
        Check("it imports without error", result.Encounter is not null && result.Error is null, result.Error ?? "");
        var e = result.Encounter!;
        Check("name, level and job are kept", e.Name == "TEA WAR MT" && e.Level == 80 && e.JobId == 21);
        Check("the duty is kept, and its zone comes from the game's own data", e.DutyId == 694 && e.DutyTerritoryId == 6940 && e.DutyName == "Duty 694");
        Check("rows stay in order and note rows stay notes",
              e.Rows.Count == 0 && e.Children[0].Rows.Count == 2 && !e.Children[0].Rows[0].IsNote && e.Children[0].Rows[1].IsNote && e.Children[0].Rows[1].Text == "Stack on the marker\nthen spread");
        Check("mechanic rows keep their mechanic, skills and notes",
              e.Children[0].Rows[0].Mechanic == "Raidwide" && e.Children[0].Rows[0].Actions.SequenceEqual(new uint[] { 7531, 7535 }) && e.Children[0].Rows[0].Notes == "Every pull");
        Check("the 4 phases come with their rows",
              e.Children.Count == 4 && e.Children.All(c => c.Rows.Count == 2) && e.Children[2].Name == "Phase 3" && e.Children[3].Rows[1].Text == "Note 4");
        Check("the counts are reported", result.Phases == 4 && result.Rows == 8 && result.DroppedSkills == 0 && !result.DutyCleared);
        Check("every sheet gets a fresh id", e.Id != original.Id && !e.Children.Select(c => c.Id).Intersect(original.Children.Select(c => c.Id)).Any());
        Check("the import is a copy, nothing is shared with the original", !ReferenceEquals(e.Children[0].Rows[0].Actions, original.Children[0].Rows[0].Actions));

    }

    [Fact]
    public void PhasesAndRowsThatAreOffStayOffThroughExportAndImport()
    {
        var text = Export(out var original);
        var e = Import(text).Encounter!;

        // Which phases are switched off travels with the encounter. Text from before that existed has every phase on.
        Check("an encounter with every phase on imports with every phase on", e.Children.All(c => c.Enabled));
        original.Children[1].Enabled = false;
        var withOff = Import(Sharing.Export(original)).Encounter!;
        Check("a phase that is off stays off through export and import", withOff.Children.Select(c => c.Enabled).SequenceEqual(new[] { true, false, true, true }));
        var older = Import(Pack("{\"v\":1,\"e\":{\"n\":\"E\",\"p\":[{\"n\":\"A\"},{\"n\":\"B\"}]}}")).Encounter!;
        Check("text from before phases could be switched off has every phase on", older.Children.All(c => c.Enabled));
        var marked = Import(Pack("{\"v\":1,\"e\":{\"n\":\"E\",\"x\":true,\"p\":[{\"n\":\"A\",\"x\":true},{\"n\":\"B\"}]}}")).Encounter!;
        Check("only a phase can be off: the encounter itself is always on", marked.Enabled && !marked.Children[0].Enabled && marked.Children[1].Enabled);
        Check("an export with every phase on does not mention it", !Gunzip(Sharing.Export(new SheetNode { Name = "E", Children = { new SheetNode { Name = "P" } } })).Contains("\"x\""));

        // Rows can be switched off too, and that travels the same way.
        original.Children[0].Rows[0].Enabled = false;
        original.Children[2].Rows[1].Enabled = false;
        var rowsOff = Import(Sharing.Export(original)).Encounter!;
        Check("a row that is off stays off through export and import, in different phases",
              !rowsOff.Children[0].Rows[0].Enabled && rowsOff.Children[0].Rows[1].Enabled && rowsOff.Children[2].Rows[0].Enabled && !rowsOff.Children[2].Rows[1].Enabled);
        var oldRows = Import(Pack("{\"v\":1,\"e\":{\"n\":\"E\",\"r\":[{\"m\":\"A\"},{\"t\":\"note\"}]}}")).Encounter!;
        Check("text from before rows could be switched off has every row on", oldRows.Children[0].Rows.All(r => r.Enabled));
        var noteOff = Import(Pack("{\"v\":1,\"e\":{\"n\":\"E\",\"r\":[{\"t\":\"note\",\"x\":true},{\"m\":\"A\",\"x\":true},{\"m\":\"B\"}]}}")).Encounter!;
        Check("a note row and a mechanic row can both be off", !noteOff.Children[0].Rows[0].Enabled && !noteOff.Children[0].Rows[1].Enabled && noteOff.Children[0].Rows[2].Enabled);

        // Text with rows on the encounter itself becomes a first phase, named after the encounter, ahead of the phases it already has.
        var wrapped = Import(Pack("{\"v\":1,\"e\":{\"n\":\"Fight\",\"l\":70,\"r\":[{\"m\":\"A\"}],\"p\":[{\"n\":\"P2\",\"r\":[{\"m\":\"B\"}]}]}}"));
        var w = wrapped.Encounter!;
        Check("rows on the encounter become a first phase named after it", w.Rows.Count == 0 && w.Children.Count == 2 && w.Children[0].Name == "Fight" && w.Children[0].Level == 70 && w.Children[0].Rows[0].Mechanic == "A" && w.Children[1].Name == "P2");
        Check("the counts include the new phase", wrapped.Phases == 2 && wrapped.Rows == 2);
        Check("an export with every row on does not mention it",
              !Gunzip(Sharing.Export(new SheetNode { Name = "E", Rows = { new SheetRow { Mechanic = "A" } } })).Contains("\"x\""));

    }

    [Fact]
    public void PastedTextOftenArrivesInOddShapes()
    {
        var text = Export(out _);

        // Pasted text often arrives with spaces around it, or wrapped onto several lines.
        var wrapped = string.Join("\r\n", Enumerable.Range(0, (text.Length + 59) / 60).Select(i => text.Substring(i * 60, Math.Min(60, text.Length - i * 60))));
        Check("text wrapped onto several lines still imports", Import(wrapped).Encounter?.Children[0].Rows.Count == 2, Import(wrapped).Error ?? "");
        Check("spaces and blank lines around the text are ignored", Import("\n  " + text + "  \r\n\r\n").Encounter is not null);
        Check("a line break right after the prefix is accepted", Import("DutyNotes:1:\n" + text["DutyNotes:1:".Length..]).Encounter is not null);

    }

    [Fact]
    public void TheSampleInTheSamplesFolderImports()
    {
        // The sample in samples/ is real text people paste; it must keep importing.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "samples", "example-encounter.txt"))) dir = dir.Parent;
        Check("the sample file is found in samples/", dir is not null);
        if (dir is null) return;
        {
            var sample = Import(File.ReadAllText(Path.Combine(dir.FullName, "samples", "example-encounter.txt")));
            var s = sample.Encounter;
            Check("the example sample imports: 3 phases, 9 rows, nothing dropped",
                  s is not null && sample.Phases == 3 && sample.Rows == 9 && sample.DroppedSkills == 0 && !sample.DutyCleared,
                  sample.Error ?? $"{sample.Phases} phases, {sample.Rows} rows, {sample.DroppedSkills} dropped");
            Check("the example sample shows the features: a note row, an off row, an off phase and Limit Break",
                  s is not null && s.Rows.Count == 0 && s.Children[0].Rows[0].IsNote && !s.Children[1].Rows[2].Enabled && !s.Children[2].Enabled && s.Children[0].Enabled
                  && s.Children[2].Rows[0].Actions.SequenceEqual(new[] { Configuration.LimitBreakId }));
            Check("the example sample names no real duty or job", s is not null && s.DutyId == 0 && s.JobId == 0);
        }
    }

    [Fact]
    public void WhatThisGameVersionDoesNotKnowIsDroppedAndCounted()
    {
        var text = Export(out _);
        var r = Import(text, skill: id => id != 7535, duty: _ => null, job: _ => false);
        Check("skills this game version lacks are dropped and counted", r.Encounter!.Children[0].Rows[0].Actions.SequenceEqual(new uint[] { 7531 }) && r.DroppedSkills >= 1);
        Check("an unknown duty is cleared and flagged", r.Encounter.DutyId == 0 && r.Encounter.DutyTerritoryId == 0 && r.DutyCleared);
        Check("an unknown job becomes any job", r.Encounter.JobId == 0);
    }

    [Fact]
    public void BrokenTextIsRefusedWithAMessage()
    {
        var text = Export(out _);

        void Rejects(string label, string input)
        {
            var x = Import(input);
            Check("rejects " + label, x.Encounter is null && !string.IsNullOrEmpty(x.Error), x.Error ?? "NO ERROR");
        }

        Rejects("empty text", "");
        Rejects("only spaces", "   ");
        Rejects("text that isn't ours", "hello world");
        Rejects("another plugin's text", "Other:1:AAAA");
        Rejects("bad base64", "DutyNotes:1:!!!not base64!!!");
        Rejects("text cut off in the middle", text[..(text.Length / 2)]);
        Rejects("valid base64 that isn't gzip", "DutyNotes:1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("just text")));
        Rejects("gzip that isn't JSON", Pack("this is not json"));
        Rejects("JSON of the wrong shape", Pack("[1,2,3]"));
        Rejects("JSON with no encounter", Pack("{\"v\":1}"));
        Rejects("a newer format version", Pack("{\"v\":2,\"e\":{\"n\":\"x\"}}"));
        Rejects("oversized text", "DutyNotes:1:" + new string('A', 500_000));

        // A version number this plugin doesn't know means a newer plugin made it; anything else that merely
        // resembles the header is simply not our text. The two get different messages.
        void Says(string label, string input, string fragment)
        {
            var x = Import(input);
            Check(label, x.Encounter is null && x.Error is not null && x.Error.Contains(fragment), x.Error ?? "NO ERROR");
        }

        Says("text with a newer header version says to update", "DutyNotes:2:AAAA", "newer version");
        Says("...even for a far-future version", "DutyNotes:57:AAAA", "newer version");
        Says("...even for a number too big to parse", "DutyNotes:99999999999999999999:AAAA", "newer version");
        Says("version 0 is not a real version, so it is not our text", "DutyNotes:0:AAAA", "doesn't look like Duty Notes text");
        Says("a header without a number is not our text", "DutyNotes:x:AAAA", "doesn't look like Duty Notes text");
        Says("a header without its closing colon is not our text", "DutyNotes:1AAAA", "doesn't look like Duty Notes text");
        Says("a lowercase header is not our text", "dutynotes:1:AAAA", "doesn't look like Duty Notes text");
    }

    [Fact]
    public void HostileTextIsRefusedOrCutDown()
    {
        void Rejects(string label, string input)
        {
            var x = Import(input);
            Check("rejects " + label, x.Encounter is null && !string.IsNullOrEmpty(x.Error), x.Error ?? "NO ERROR");
        }

        Rejects("a gzip bomb (tiny, expands to 50 MB)", Pack(new string(' ', 50_000_000)));
        Rejects("200 phases", Pack("{\"v\":1,\"e\":{\"n\":\"x\",\"p\":[" + string.Join(",", Enumerable.Repeat("{\"n\":\"p\"}", 200)) + "]}}"));
        Rejects("5000 rows", Pack("{\"v\":1,\"e\":{\"n\":\"x\",\"r\":[" + string.Join(",", Enumerable.Repeat("{\"m\":\"a\"}", 5000)) + "]}}"));

        var typeTrick = Import(Pack("{\"$type\":\"System.IO.FileInfo, System.Private.CoreLib\",\"v\":1,\"e\":{\"$type\":\"System.Diagnostics.Process, System.Diagnostics.Process\",\"n\":\"safe\"}}"));
        Check("type-name tricks are ignored, not obeyed", typeTrick.Encounter is not null && typeTrick.Encounter.Name == "safe", typeTrick.Error ?? "");

        var evil = Import(Pack("{\"v\":1,\"e\":{\"n\":\"" + new string('N', 5000) + "\",\"l\":9999,\"r\":[{\"m\":\"a\\u0007\\u001bb\\u0000c\",\"s\":["
                               + string.Join(",", Enumerable.Range(1, 100)) + "],\"o\":\"" + new string('x', 5000) + "\"},{\"t\":\"" + new string('T', 5000) + "\"}]}}"));
        var ev = evil.Encounter!;
        Check("a long name is cut to 128", ev.Name.Length == 128);
        Check("the level is clamped to 100", ev.Level == 100);
        Check("control characters are stripped", ev.Children[0].Rows[0].Mechanic == "abc", ev.Children[0].Rows[0].Mechanic);
        Check("skills per row are capped at 24", ev.Children[0].Rows[0].Actions.Count == 24, ev.Children[0].Rows[0].Actions.Count.ToString());
        Check("long notes and note rows are cut", ev.Children[0].Rows[0].Notes.Length == 256 && ev.Children[0].Rows[1].Text.Length == 1000);

        var dup = Import(Pack("{\"v\":1,\"e\":{\"n\":\"d\",\"r\":[{\"m\":\"a\",\"s\":[5,5,5,6]}]}}"));
        Check("the same skill twice in one row is kept once", dup.Encounter!.Children[0].Rows[0].Actions.SequenceEqual(new uint[] { 5, 6 }));
        Check("a missing name gets a placeholder", Import(Pack("{\"v\":1,\"e\":{\"r\":[]}}")).Encounter!.Name == "Imported encounter");
    }
}
