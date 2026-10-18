using Newtonsoft.Json;

namespace DutyNotes.Tests;

/// <summary>
/// Dalamud saves settings with type names recorded and loads them by filling the lists that already exist.
/// That combination once doubled every nested list on each load, and renaming the plugin changed the recorded
/// type names, so both are covered here.
/// </summary>
public sealed class SerializationTests : CheckBase
{
    // How Dalamud reads and writes plugin settings.
    private static readonly JsonSerializerSettings Dalamud = new()
    {
        TypeNameHandling = TypeNameHandling.Objects,
        ObjectCreationHandling = ObjectCreationHandling.Auto,
        Formatting = Formatting.Indented,
    };

    [Fact]
    public void RepeatedLoadsDontDuplicate()
    {
        var c = new Configuration { Roots = { new SheetNode { Name = "TEA" } } };
        c.Migrate();
        for (var i = 1; i <= 4; i++)
            c.Roots[0].Children.Add(new SheetNode { Name = $"Phase {i}", Rows = { new SheetRow { Mechanic = "m", Actions = { 1, 2, 3 } } } });

        var json = JsonConvert.SerializeObject(c, Dalamud);
        for (var load = 1; load <= 3; load++)
        {
            c = (Configuration)JsonConvert.DeserializeObject(json, Dalamud)!;
            c.Migrate();
            var phase = c.Roots[0].Children[0];
            Check($"load {load}: still 1 encounter, 4 phases, 1 row, 3 skills",
                  c.Roots.Count == 1 && c.Roots[0].Children.Count == 4 && phase.Rows.Count == 1 && phase.Rows[0].Actions.Count == 3,
                  $"roots={c.Roots.Count} phases={c.Roots[0].Children.Count} rows={phase.Rows.Count} skills={phase.Rows[0].Actions.Count}");
            json = JsonConvert.SerializeObject(c, Dalamud);
        }
    }

    [Fact]
    public void LegacyNamesAreRewritten()
    {
        // A trimmed settings file as the plugin wrote it under its old name, MitigationSheet.
        const string legacy = """
        {
          "$type": "MitigationSheet.Configuration, MitigationSheet",
          "Roots": [
            {
              "$type": "MitigationSheet.SheetNode, MitigationSheet",
              "Id": "cf23804a-7ca4-44e5-aa2a-8fced9e97a76",
              "Name": "TEA WAR MT",
              "Level": 80,
              "Rows": [],
              "Children": [
                {
                  "$type": "MitigationSheet.SheetNode, MitigationSheet",
                  "Id": "1bdee563-64ed-420f-88b5-8668089758c1",
                  "Name": "Phase 1",
                  "Level": 80,
                  "Rows": [
                    { "$type": "MitigationSheet.SheetRow, MitigationSheet", "Mechanic": "One", "Actions": [ 7531, 44 ], "Notes": "" }
                  ],
                  "Children": []
                }
              ]
            }
          ],
          "IconSize": 36.0
        }
        """;

        var rewritten = Configuration.RenameLegacyTypes(legacy);
        Check("no old type name is left", !rewritten.Contains("MitigationSheet"));
        Check("all four type names were rewritten", rewritten.Split("DutyNotes.").Length - 1 == 4);

        var c = (Configuration)JsonConvert.DeserializeObject(rewritten, Dalamud)!;
        c.Migrate();
        Check("the rewritten file loads with its content intact",
              c.Roots.Count == 1 && c.Roots[0].Name == "TEA WAR MT" && c.Roots[0].Children.Count == 1
              && c.Roots[0].Children[0].Rows[0].Actions.SequenceEqual(new uint[] { 7531, 44 }) && c.IconSize == 36f);
        Check("rewriting an already current file changes nothing", Configuration.RenameLegacyTypes(rewritten) == rewritten);
    }

    [Fact]
    public void FilesFromBeforeASettingExistedStillLoad()
    {
        // A file from before a setting existed: the missing setting takes its default.
        var older = (Configuration)JsonConvert.DeserializeObject(
            "{ \"$type\": \"DutyNotes.Configuration, DutyNotes\", \"AutoSwitch\": true }", Dalamud)!;
        Check("a file from before newer settings existed gets their defaults",
              older.AutoShowOverlay && older.AutoHideOverlay && older.NoteRowScale == 1f);
        Check("a file from before column alignment existed gets left and top",
              older.MechanicHAlign == HAlign.Left && older.MitigationVAlign == VAlign.Top && older.NotesHAlign == HAlign.Left && older.IsLayoutDefault);

        // The removed backup feature left settings in older files; they are ignored, and nothing else is disturbed.
        var withBackupKeys = (Configuration)JsonConvert.DeserializeObject(
            "{ \"$type\": \"DutyNotes.Configuration, DutyNotes\", \"AutoSwitch\": false, \"AutoBackup\": true, \"AutoBackupFolder\": \"D:\\\\x\", \"AutoBackupKeep\": 7 }", Dalamud)!;
        Check("settings left by the removed backup feature are ignored", !withBackupKeys.AutoSwitch && withBackupKeys.IsStyleDefault);

        // Fields that were removed from the plugin (such as the old free notes) are simply ignored.
        var withRemoved = (Configuration)JsonConvert.DeserializeObject(
            "{ \"$type\": \"DutyNotes.Configuration, DutyNotes\", \"Roots\": [ { \"$type\": \"DutyNotes.SheetNode, DutyNotes\", \"Name\": \"x\", " +
            "\"FreeNotes\": \"old text\", \"FreeNotesBelow\": true, \"Rows\": [], \"Children\": [] } ] }", Dalamud)!;
        Check("fields the plugin no longer has are ignored", withRemoved.Roots.Count == 1 && withRemoved.Roots[0].Name == "x");
    }

    [Fact]
    public void ConfigurationsWithNoEncountersAreValid()
    {
        // A fresh install, or a file whose encounters were all deleted, has none, and that is fine: nothing is selected.
        var empty = new Configuration();
        empty.Migrate();
        Check("an empty configuration stays empty, with nothing selected", empty.Roots.Count == 0 && empty.Selected is null && empty.SelectedId == Guid.Empty);
        Check("an empty configuration has no active encounter or phase", empty.ActiveEncounter is null && empty.ActivePhase is null && empty.CurrentPhase is null);

        var staleSelection = new Configuration { SelectedId = Guid.NewGuid() };
        staleSelection.Migrate();
        Check("a selection that points at nothing is cleared when there are no encounters", staleSelection.SelectedId == Guid.Empty);

        // A file from the first version kept a flat list of rows; it still becomes one encounter.
        var flat = new Configuration();
        flat.Rows.Add(new SheetRow { Mechanic = "old row" });
        flat.Migrate();
        Check("a flat list of rows from the first version becomes one encounter with one phase", flat.Roots.Count == 1 && flat.Roots[0].Rows.Count == 0 && flat.Roots[0].Children.Count == 1 && flat.Roots[0].Children[0].Rows.Count == 1 && flat.Rows.Count == 0);
    }

    [Fact]
    public void DeepTreesAreFlattened()
    {
        // Only two levels exist (encounter > phase). An older file with deeper nesting is hoisted up on load.
        var c = new Configuration();
        var root = new SheetNode { Name = "Root" };
        var phase = new SheetNode { Name = "Phase" };
        phase.Children.Add(new SheetNode { Name = "Too deep" });
        root.Children.Add(phase);
        c.Roots.Add(root);
        c.Migrate();
        Check("anything nested deeper than a phase becomes a phase",
              c.Roots[0].Children.Select(n => n.Name).SequenceEqual(new[] { "Phase", "Too deep" }) && c.Roots[0].Children.All(n => n.Children.Count == 0));
    }

    [Fact]
    public void TheSameNodeListedTwiceIsKeptOnce()
    {
        // The old duplication bug left the same phase in a list twice; loading heals that.
        var dup = new Configuration();
        var e = new SheetNode { Name = "E" };
        var p = new SheetNode { Name = "P" };
        e.Children.Add(p); e.Children.Add(p);
        dup.Roots.Add(e); dup.Roots.Add(e);
        dup.Migrate();
        Check("the same node listed twice is kept once", dup.Roots.Count == 1 && dup.Roots[0].Children.Count == 1);
    }

    [Fact]
    public void EarlierPerJobLimitBreaksAreFoldedIntoOne()
    {
        // The plugin briefly offered each limit break as its own skill; they are folded into the one "Limit Break" entry.
        var lb = new Configuration();
        var lbEncounter = new SheetNode { Name = "LB" };
        lbEncounter.Rows.Add(new SheetRow { Mechanic = "Raidwide", Actions = { 7531, 4240, 197 } });
        lbEncounter.Rows.Add(new SheetRow { IsNote = true, Text = "note" });
        var lbPhase = new SheetNode { Name = "P1" };
        lbPhase.Rows.Add(new SheetRow { Mechanic = "Tank buster", Actions = { 17105, 7535 } });
        lbEncounter.Children.Add(lbPhase);
        lb.Roots.Add(lbEncounter);
        lb.Migrate();
        Check("earlier per-job limit breaks become the one Limit Break, in the same place, once",
              lbEncounter.Rows[0].Actions.SequenceEqual(new[] { 7531u, Configuration.LimitBreakId })
              && lbPhase.Rows[0].Actions.SequenceEqual(new[] { Configuration.LimitBreakId, 7535u }));
        lb.Migrate();
        Check("folding is repeatable", lbEncounter.Rows[0].Actions.SequenceEqual(new[] { 7531u, Configuration.LimitBreakId }));
    }
}
