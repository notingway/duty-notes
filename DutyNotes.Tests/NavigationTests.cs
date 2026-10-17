using static DutyNotes.Tests.T;

namespace DutyNotes.Tests;

internal static class NavigationTests
{
    public static void Run()
    {
        var tea = new SheetNode { Name = "TEA" };
        var phases = Enumerable.Range(1, 4).Select(i => new SheetNode { Name = $"Phase {i}" }).ToList();
        tea.Children.AddRange(phases);
        var solo = new SheetNode { Name = "Solo" };
        var ucob = new SheetNode { Name = "UCoB" };
        ucob.Children.Add(new SheetNode { Name = "Twintania" });
        var cfg = new Configuration { Roots = { tea, solo, ucob } };

        string Step(PhaseStep s, bool fromEncounter = false) => cfg.PhaseTarget(s, fromEncounter)?.Name ?? "null";
        void Go(PhaseStep s) { if (cfg.PhaseTarget(s) is { } t) cfg.ActivePhaseId = t.Id; }

        // Nothing stored yet: the first encounter, at its first phase.
        Check("default encounter is the first one", cfg.ActiveEncounter?.Name == "TEA");
        Check("default phase is its first", cfg.ActivePhase?.Name == "Phase 1");

        cfg.SetActiveEncounter(tea);
        Check("Next from phase 1 goes to phase 2", Step(PhaseStep.Next) == "Phase 2");
        Go(PhaseStep.Next); Go(PhaseStep.Next); Go(PhaseStep.Next);
        Check("three Nexts land on phase 4", cfg.ActivePhase?.Name == "Phase 4");
        Check("Next on the last phase stays put", Step(PhaseStep.Next) == "Phase 4");
        Check("Previous goes back one", Step(PhaseStep.Previous) == "Phase 3");
        Check("Reset goes to the first phase", Step(PhaseStep.Reset) == "Phase 1");
        Go(PhaseStep.Reset);
        Check("Previous on the first phase stays put", Step(PhaseStep.Previous) == "Phase 1");

        // An encounter with no phases has nothing to step through.
        cfg.SetActiveEncounter(solo);
        Check("no phases: there is no active phase", cfg.ActivePhase is null);
        Check("no phases: Next, Previous and Reset do nothing",
              Step(PhaseStep.Next) == "null" && Step(PhaseStep.Previous) == "null" && Step(PhaseStep.Reset) == "null");

        // Choosing an encounter starts it at its first phase.
        cfg.SetActiveEncounter(tea); cfg.ActivePhaseId = phases[2].Id;
        cfg.SetActiveEncounter(ucob);
        Check("switching encounter starts at its first phase", cfg.ActivePhase?.Name == "Twintania");

        // Stored ids that no longer exist fall back instead of breaking.
        cfg.ActiveEncounterId = Guid.NewGuid();
        Check("a deleted active encounter falls back to the first", cfg.ActiveEncounter?.Name == "TEA");
        cfg.SetActiveEncounter(tea); cfg.ActivePhaseId = Guid.NewGuid();
        Check("a deleted active phase falls back to the first", cfg.ActivePhase?.Name == "Phase 1");

        // The editor's selection drives the navigation.
        cfg.SetActiveEncounter(tea);
        cfg.SelectedId = phases[2].Id; cfg.SetActiveFromSelection();
        Check("selecting a phase in the editor makes it the active phase", cfg.ActivePhase?.Name == "Phase 3");
        cfg.SelectedId = ucob.Id; cfg.SetActiveFromSelection();
        Check("selecting another encounter switches to it, at its first phase",
              cfg.ActiveEncounter?.Name == "UCoB" && cfg.ActivePhase?.Name == "Twintania");
        cfg.SetActiveEncounter(tea); cfg.ActivePhaseId = phases[1].Id;
        cfg.SelectedId = tea.Id; cfg.SetActiveFromSelection();
        Check("selecting the same encounter keeps the phase you are on", cfg.ActivePhase?.Name == "Phase 2");

        // With the encounter itself selected in the editor, no phase is "current".
        cfg.SetActiveEncounter(tea); cfg.ActivePhaseId = phases[1].Id;
        Check("from the encounter view, Next goes to the first phase", Step(PhaseStep.Next, fromEncounter: true) == "Phase 1");
        Check("from the encounter view, Reset goes to the first phase", Step(PhaseStep.Reset, fromEncounter: true) == "Phase 1");
        Check("from the encounter view, Previous has nowhere to go", Step(PhaseStep.Previous, fromEncounter: true) == "null");

        // Phases that are switched off are skipped (phases 2 and 3 off: 1 and 4 remain).
        phases[1].Enabled = false; phases[2].Enabled = false;
        cfg.SetActiveEncounter(tea);
        Check("Next skips phases that are off", Step(PhaseStep.Next) == "Phase 4");
        Go(PhaseStep.Next);
        Check("Previous skips phases that are off", Step(PhaseStep.Previous) == "Phase 1");
        Check("Next on the last phase that is on stays put", Step(PhaseStep.Next) == "Phase 4");
        Go(PhaseStep.Reset);
        Check("Reset goes to the first phase that is on", cfg.ActivePhase?.Name == "Phase 1");

        phases[0].Enabled = false;
        cfg.SetActiveEncounter(tea);
        Check("an encounter starts at its first phase that is on", cfg.ActivePhase?.Name == "Phase 4");
        Check("Reset goes to the first phase that is on, not the first phase", Step(PhaseStep.Reset) == "Phase 4");
        Check("from the encounter view, Next goes to the first phase that is on", Step(PhaseStep.Next, fromEncounter: true) == "Phase 4");

        // The phase you are on gets switched off: the overlay moves to one that is on.
        phases[0].Enabled = true; phases[3].Enabled = true;
        cfg.SetActiveEncounter(tea); cfg.ActivePhaseId = phases[3].Id;
        phases[3].Enabled = false;
        Check("switching off the phase you are on moves the overlay to one that is on", cfg.ActivePhase?.Name == "Phase 1");

        // The editor can select a phase that is off. It becomes the current position, but is never the overlay's phase while playing.
        cfg.SelectedId = phases[1].Id; cfg.SetActiveFromSelection();
        Check("a selected phase that is off is where the steps start from", cfg.CurrentPhase?.Name == "Phase 2");
        Check("...but while playing it is not shown (a phase that is on stands in)", cfg.ActivePhase?.Name == "Phase 1");
        Check("Next from a phase that is off goes to the next one that is on, or stays", Step(PhaseStep.Next) == "Phase 2");
        Check("Previous from a phase that is off goes to the one before that is on", Step(PhaseStep.Previous) == "Phase 1");

        // Everything off: nothing to step through, and no phase to show.
        foreach (var p in phases) p.Enabled = false;
        cfg.SetActiveEncounter(tea);
        Check("all phases off: no active phase and nowhere to step",
              cfg.ActivePhase is null && Step(PhaseStep.Next) == "null" && Step(PhaseStep.Reset) == "null");
        foreach (var p in phases) p.Enabled = true;
        Check("a copy keeps which phases are off", phases[1].Clone().Enabled && !new SheetNode { Enabled = false }.Clone().Enabled);

        // Deleting: the selection stays in the same encounter (the bug: deleting the last phase jumped to the top encounter).
        var top = new SheetNode { Name = "Top" };
        var bottom = new SheetNode { Name = "Bottom" };
        var b1 = new SheetNode { Name = "B1" }; var b2 = new SheetNode { Name = "B2" }; var b3 = new SheetNode { Name = "B3" };
        bottom.Children.AddRange(new[] { b1, b2, b3 });
        top.Children.Add(new SheetNode { Name = "T1" });
        var del = new Configuration { Roots = { top, bottom } };

        del.RemoveNode(b2);
        Check("deleting a phase in the middle selects the one that took its place", del.Selected == b3 && bottom.Children.Count == 2);
        del.RemoveNode(b3);
        Check("deleting the last phase of a list selects the one before it", del.Selected == b1);
        del.RemoveNode(b1);
        Check("deleting the only phase left selects its own encounter, not the top one", del.Selected == bottom && bottom.Children.Count == 0);

        var undoEncounter = del.RemoveNode(top);
        Check("deleting an encounter selects the one that took its place", del.Selected == bottom && del.Roots.Count == 1);
        undoEncounter!();
        Check("undo puts an encounter back where it was, and selects it", del.Roots[0] == top && del.Selected == top);

        var c1 = new SheetNode { Name = "C1" }; var c2 = new SheetNode { Name = "C2" };
        bottom.Children.AddRange(new[] { c1, c2 });
        var undoPhase = del.RemoveNode(c2);
        Check("deleting the last of several phases selects the one before it", del.Selected == c1);
        undoPhase!();
        Check("undo puts a phase back where it was, and selects it", bottom.Children.SequenceEqual(new[] { c1, c2 }) && del.Selected == c2);

        var onlyOne = new Configuration { Roots = { new SheetNode { Name = "Only" } } };
        var undoOnly = onlyOne.RemoveNode(onlyOne.Roots[0]);
        Check("deleting the only encounter is allowed and leaves nothing selected", onlyOne.Roots.Count == 0 && onlyOne.Selected is null);
        undoOnly!();
        Check("undo brings the only encounter back and selects it", onlyOne.Roots.Count == 1 && onlyOne.Selected == onlyOne.Roots[0]);
        Check("deleting something that is not in the tree does nothing", new Configuration { Roots = { top } }.RemoveNode(new SheetNode()) is null);

        // Tree helpers.
        Check("ContainingList finds a phase's list", cfg.ContainingList(phases[0]) == tea.Children);
        Check("Find locates a nested sheet", cfg.Find(phases[3].Id) == phases[3]);
    }
}
