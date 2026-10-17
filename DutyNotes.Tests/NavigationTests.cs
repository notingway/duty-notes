namespace DutyNotes.Tests;

public sealed class NavigationTests : CheckBase
{
    /// <summary>Three encounters: TEA with four phases, one with none, and UCoB with one. A new one for every test.</summary>
    private sealed class World
    {
        public readonly SheetNode Tea = new() { Name = "TEA" };
        public readonly SheetNode Solo = new() { Name = "Solo" };
        public readonly SheetNode Ucob = new() { Name = "UCoB" };
        public readonly List<SheetNode> Phases;
        public readonly Configuration Cfg;

        public World()
        {
            Phases = Enumerable.Range(1, 4).Select(i => new SheetNode { Name = $"Phase {i}" }).ToList();
            Tea.Children.AddRange(Phases);
            Ucob.Children.Add(new SheetNode { Name = "Twintania" });
            Cfg = new Configuration { Roots = { Tea, Solo, Ucob } };
        }

        public string Step(PhaseStep s, bool fromEncounter = false) => Cfg.PhaseTarget(s, fromEncounter)?.Name ?? "null";
        public void Go(PhaseStep s) { if (Cfg.PhaseTarget(s) is { } t) Cfg.ActivePhaseId = t.Id; }
    }

    [Fact]
    public void NextPreviousAndResetStepThroughThePhases()
    {
        var w = new World();
        var cfg = w.Cfg;

        // Nothing stored yet: the first encounter, at its first phase.
        Check("default encounter is the first one", cfg.ActiveEncounter?.Name == "TEA");
        Check("default phase is its first", cfg.ActivePhase?.Name == "Phase 1");

        cfg.SetActiveEncounter(w.Tea);
        Check("Next from phase 1 goes to phase 2", w.Step(PhaseStep.Next) == "Phase 2");
        w.Go(PhaseStep.Next); w.Go(PhaseStep.Next); w.Go(PhaseStep.Next);
        Check("three Nexts land on phase 4", cfg.ActivePhase?.Name == "Phase 4");
        Check("Next on the last phase stays put", w.Step(PhaseStep.Next) == "Phase 4");
        Check("Previous goes back one", w.Step(PhaseStep.Previous) == "Phase 3");
        Check("Reset goes to the first phase", w.Step(PhaseStep.Reset) == "Phase 1");
        w.Go(PhaseStep.Reset);
        Check("Previous on the first phase stays put", w.Step(PhaseStep.Previous) == "Phase 1");
    }

    [Fact]
    public void AnEncounterWithNoPhasesHasNothingToStepThrough()
    {
        var w = new World();
        w.Cfg.SetActiveEncounter(w.Solo);
        Check("no phases: there is no active phase", w.Cfg.ActivePhase is null);
        Check("no phases: Next, Previous and Reset do nothing",
              w.Step(PhaseStep.Next) == "null" && w.Step(PhaseStep.Previous) == "null" && w.Step(PhaseStep.Reset) == "null");
    }

    [Fact]
    public void ChoosingAnEncounterStartsItAtItsFirstPhase()
    {
        var w = new World();
        w.Cfg.SetActiveEncounter(w.Tea); w.Cfg.ActivePhaseId = w.Phases[2].Id;
        w.Cfg.SetActiveEncounter(w.Ucob);
        Check("switching encounter starts at its first phase", w.Cfg.ActivePhase?.Name == "Twintania");
    }

    [Fact]
    public void StoredIdsThatNoLongerExistFallBack()
    {
        var w = new World();
        w.Cfg.ActiveEncounterId = Guid.NewGuid();
        Check("a deleted active encounter falls back to the first", w.Cfg.ActiveEncounter?.Name == "TEA");
        w.Cfg.SetActiveEncounter(w.Tea); w.Cfg.ActivePhaseId = Guid.NewGuid();
        Check("a deleted active phase falls back to the first", w.Cfg.ActivePhase?.Name == "Phase 1");
    }

    [Fact]
    public void TheEditorsSelectionDrivesTheNavigation()
    {
        var w = new World();
        var cfg = w.Cfg;
        cfg.SetActiveEncounter(w.Tea);
        cfg.SelectedId = w.Phases[2].Id; cfg.SetActiveFromSelection();
        Check("selecting a phase in the editor makes it the active phase", cfg.ActivePhase?.Name == "Phase 3");
        cfg.SelectedId = w.Ucob.Id; cfg.SetActiveFromSelection();
        Check("selecting another encounter switches to it, at its first phase",
              cfg.ActiveEncounter?.Name == "UCoB" && cfg.ActivePhase?.Name == "Twintania");
        cfg.SetActiveEncounter(w.Tea); cfg.ActivePhaseId = w.Phases[1].Id;
        cfg.SelectedId = w.Tea.Id; cfg.SetActiveFromSelection();
        Check("selecting the same encounter keeps the phase you are on", cfg.ActivePhase?.Name == "Phase 2");
    }

    [Fact]
    public void WithTheEncounterItselfSelectedNoPhaseIsCurrent()
    {
        var w = new World();
        w.Cfg.SetActiveEncounter(w.Tea); w.Cfg.ActivePhaseId = w.Phases[1].Id;
        Check("from the encounter view, Next goes to the first phase", w.Step(PhaseStep.Next, fromEncounter: true) == "Phase 1");
        Check("from the encounter view, Reset goes to the first phase", w.Step(PhaseStep.Reset, fromEncounter: true) == "Phase 1");
        Check("from the encounter view, Previous has nowhere to go", w.Step(PhaseStep.Previous, fromEncounter: true) == "null");
    }

    [Fact]
    public void PhasesThatAreSwitchedOffAreSkipped()
    {
        var w = new World();
        var cfg = w.Cfg;

        // Phases 2 and 3 off: 1 and 4 remain.
        w.Phases[1].Enabled = false; w.Phases[2].Enabled = false;
        cfg.SetActiveEncounter(w.Tea);
        Check("Next skips phases that are off", w.Step(PhaseStep.Next) == "Phase 4");
        w.Go(PhaseStep.Next);
        Check("Previous skips phases that are off", w.Step(PhaseStep.Previous) == "Phase 1");
        Check("Next on the last phase that is on stays put", w.Step(PhaseStep.Next) == "Phase 4");
        w.Go(PhaseStep.Reset);
        Check("Reset goes to the first phase that is on", cfg.ActivePhase?.Name == "Phase 1");

        w.Phases[0].Enabled = false;
        cfg.SetActiveEncounter(w.Tea);
        Check("an encounter starts at its first phase that is on", cfg.ActivePhase?.Name == "Phase 4");
        Check("Reset goes to the first phase that is on, not the first phase", w.Step(PhaseStep.Reset) == "Phase 4");
        Check("from the encounter view, Next goes to the first phase that is on", w.Step(PhaseStep.Next, fromEncounter: true) == "Phase 4");
    }

    [Fact]
    public void SwitchingOffThePhaseYouAreOnMovesTheOverlay()
    {
        var w = new World();
        var cfg = w.Cfg;
        w.Phases[1].Enabled = false; w.Phases[2].Enabled = false;

        // The phase you are on gets switched off: the overlay moves to one that is on.
        cfg.SetActiveEncounter(w.Tea); cfg.ActivePhaseId = w.Phases[3].Id;
        w.Phases[3].Enabled = false;
        Check("switching off the phase you are on moves the overlay to one that is on", cfg.ActivePhase?.Name == "Phase 1");

        // The editor can select a phase that is off. It becomes the current position, but is never the overlay's phase while playing.
        cfg.SelectedId = w.Phases[1].Id; cfg.SetActiveFromSelection();
        Check("a selected phase that is off is where the steps start from", cfg.CurrentPhase?.Name == "Phase 2");
        Check("...but while playing it is not shown (a phase that is on stands in)", cfg.ActivePhase?.Name == "Phase 1");
        Check("Next from a phase that is off goes to the next one that is on, or stays", w.Step(PhaseStep.Next) == "Phase 2");
        Check("Previous from a phase that is off goes to the one before that is on", w.Step(PhaseStep.Previous) == "Phase 1");
    }

    [Fact]
    public void WithEveryPhaseOffThereIsNothingToStepThrough()
    {
        var w = new World();
        foreach (var p in w.Phases) p.Enabled = false;
        w.Cfg.SetActiveEncounter(w.Tea);
        Check("all phases off: no active phase and nowhere to step",
              w.Cfg.ActivePhase is null && w.Step(PhaseStep.Next) == "null" && w.Step(PhaseStep.Reset) == "null");
        foreach (var p in w.Phases) p.Enabled = true;
        Check("a copy keeps which phases are off", w.Phases[1].Clone().Enabled && !new SheetNode { Enabled = false }.Clone().Enabled);
    }

    /// <summary>Two encounters: Top with one phase, and Bottom with the phases b1, b2 and b3.</summary>
    private static (Configuration cfg, SheetNode top, SheetNode bottom, SheetNode b1, SheetNode b2, SheetNode b3) DeleteWorld()
    {
        var top = new SheetNode { Name = "Top" };
        var bottom = new SheetNode { Name = "Bottom" };
        var b1 = new SheetNode { Name = "B1" }; var b2 = new SheetNode { Name = "B2" }; var b3 = new SheetNode { Name = "B3" };
        bottom.Children.AddRange(new[] { b1, b2, b3 });
        top.Children.Add(new SheetNode { Name = "T1" });
        return (new Configuration { Roots = { top, bottom } }, top, bottom, b1, b2, b3);
    }

    [Fact]
    public void DeletingPhasesKeepsTheSelectionInTheSameEncounter()
    {
        // The bug: deleting the last phase jumped to the top encounter.
        var (del, _, bottom, b1, b2, b3) = DeleteWorld();

        del.RemoveNode(b2);
        Check("deleting a phase in the middle selects the one that took its place", del.Selected == b3 && bottom.Children.Count == 2);
        del.RemoveNode(b3);
        Check("deleting the last phase of a list selects the one before it", del.Selected == b1);
        del.RemoveNode(b1);
        Check("deleting the only phase left selects its own encounter, not the top one", del.Selected == bottom && bottom.Children.Count == 0);
    }

    [Fact]
    public void DeletingAnEncounterAndUndoingIt()
    {
        var (del, top, bottom, _, _, _) = DeleteWorld();

        var undoEncounter = del.RemoveNode(top);
        Check("deleting an encounter selects the one that took its place", del.Selected == bottom && del.Roots.Count == 1);
        undoEncounter!();
        Check("undo puts an encounter back where it was, and selects it", del.Roots[0] == top && del.Selected == top);
    }

    [Fact]
    public void UndoingTheDeleteOfAPhase()
    {
        var (del, _, bottom, b1, b2, b3) = DeleteWorld();
        del.RemoveNode(b1); del.RemoveNode(b2); del.RemoveNode(b3);

        var c1 = new SheetNode { Name = "C1" }; var c2 = new SheetNode { Name = "C2" };
        bottom.Children.AddRange(new[] { c1, c2 });
        var undoPhase = del.RemoveNode(c2);
        Check("deleting the last of several phases selects the one before it", del.Selected == c1);
        undoPhase!();
        Check("undo puts a phase back where it was, and selects it", bottom.Children.SequenceEqual(new[] { c1, c2 }) && del.Selected == c2);
    }

    [Fact]
    public void DeletingTheOnlyEncounterIsAllowed()
    {
        var onlyOne = new Configuration { Roots = { new SheetNode { Name = "Only" } } };
        var undoOnly = onlyOne.RemoveNode(onlyOne.Roots[0]);
        Check("deleting the only encounter is allowed and leaves nothing selected", onlyOne.Roots.Count == 0 && onlyOne.Selected is null);
        undoOnly!();
        Check("undo brings the only encounter back and selects it", onlyOne.Roots.Count == 1 && onlyOne.Selected == onlyOne.Roots[0]);

        var (_, top, _, _, _, _) = DeleteWorld();
        Check("deleting something that is not in the tree does nothing", new Configuration { Roots = { top } }.RemoveNode(new SheetNode()) is null);
    }

    [Fact]
    public void TreeHelpersFindSheets()
    {
        var w = new World();
        Check("ContainingList finds a phase's list", w.Cfg.ContainingList(w.Phases[0]) == w.Tea.Children);
        Check("Find locates a nested sheet", w.Cfg.Find(w.Phases[3].Id) == w.Phases[3]);
    }
}
