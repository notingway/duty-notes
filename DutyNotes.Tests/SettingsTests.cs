namespace DutyNotes.Tests;

public sealed class SettingsTests : CheckBase
{
    /// <summary>A configuration with something changed in every section, so a reset has something to do.</summary>
    private static Configuration Changed() => new()
    {
        IconSize = 60f, MechanicScale = 2.77f, BackgroundAlpha = 0.1f, NoteRowScale = 2f,
        AutoSize = true, ColumnPadding = 30f, ShowHeaders = false,
        AutoSwitch = false, AutoHideOverlay = false, HideDuringCutscenes = true,
        MechanicHAlign = HAlign.Right, MechanicVAlign = VAlign.Center,
        MitigationHAlign = HAlign.Center, MitigationVAlign = VAlign.Bottom,
        NotesHAlign = HAlign.Center, NotesVAlign = VAlign.Center,
    };

    [Fact]
    public void MovingWithinAList()
    {
        List<string> Abc() => new() { "a", "b", "c" };
        string Join(List<string> l) => string.Join("", l);

        var l1 = Abc(); var moved1 = Configuration.MoveWithin(l1, 0, 2);
        Check("first to last", moved1 && Join(l1) == "bca");
        var l2 = Abc(); var moved2 = Configuration.MoveWithin(l2, 2, 0);
        Check("last to first", moved2 && Join(l2) == "cab");
        var l3 = Abc(); var moved3 = Configuration.MoveWithin(l3, 1, 2);
        Check("one step down", moved3 && Join(l3) == "acb");
        var l4 = Abc(); var moved4 = Configuration.MoveWithin(l4, 1, 0);
        Check("one step up", moved4 && Join(l4) == "bac");
        var l5 = Abc();
        Check("same place changes nothing", !Configuration.MoveWithin(l5, 1, 1) && Join(l5) == "abc");
        Check("outside the list changes nothing", !Configuration.MoveWithin(l5, 3, 0) && !Configuration.MoveWithin(l5, 0, 3) && !Configuration.MoveWithin(l5, -1, 1) && Join(l5) == "abc");
        Check("an empty list is fine", !Configuration.MoveWithin(new List<string>(), 0, 0));
    }

    [Fact]
    public void ADefaultConfigurationIsAtItsDefaults()
    {
        var fresh = new Configuration();
        Check("a fresh install is at its defaults in every section",
              fresh.IsBehaviorDefault && fresh.IsLayoutDefault && fresh.IsStyleDefault);
        Check("defaults: column headers on, auto-size off, icon size 32",
              fresh.ShowHeaders && !fresh.AutoSize && fresh.IconSize == 32f);
        Check("defaults: auto-select, auto open and auto hide on; cutscene hiding off",
              fresh.AutoSwitch && fresh.AutoShowOverlay && fresh.AutoHideOverlay && !fresh.HideDuringCutscenes);
        Check("defaults: note rows are the same size as notes", fresh.NoteRowScale == fresh.NotesScale);
        Check("defaults: every column is aligned left and top, as the overlay always looked",
              fresh.MechanicHAlign == HAlign.Left && fresh.MechanicVAlign == VAlign.Top
              && fresh.MitigationHAlign == HAlign.Left && fresh.MitigationVAlign == VAlign.Top
              && fresh.NotesHAlign == HAlign.Left && fresh.NotesVAlign == VAlign.Top);
    }

    [Fact]
    public void ChangedSettingsAreDetectedInEverySection()
    {
        var c = Changed();
        Check("changed settings are detected in every section",
              !c.IsStyleDefault && !c.IsLayoutDefault && !c.IsBehaviorDefault);
    }

    [Fact]
    public void StyleResetAndItsUndo()
    {
        // A reset only touches its own section, and undo puts the old values back exactly.
        var c = Changed();
        var undoStyle = c.ResetStyle();
        Check("Style reset restores style, including the note row size", c.IsStyleDefault && c.IconSize == 32f && c.NoteRowScale == 1f);
        Check("Style reset leaves the other sections alone", c.AutoSize && c.ColumnPadding == 30f && !c.AutoSwitch);
        undoStyle();
        Check("undo restores the old style values", c.IconSize == 60f && c.MechanicScale == 2.77f && c.BackgroundAlpha == 0.1f && c.NoteRowScale == 2f);
    }

    [Fact]
    public void LayoutResetAndItsUndo()
    {
        var c = Changed();
        var undoLayout = c.ResetLayout();
        Check("Layout reset restores layout", c.IsLayoutDefault && !c.AutoSize && c.ShowHeaders && c.ColumnPadding == 10f);
        Check("Layout reset restores the column alignments",
              c.MechanicHAlign == HAlign.Left && c.MechanicVAlign == VAlign.Top && c.MitigationHAlign == HAlign.Left
              && c.MitigationVAlign == VAlign.Top && c.NotesHAlign == HAlign.Left && c.NotesVAlign == VAlign.Top);
        Check("Layout reset leaves style alone", c.IconSize == 60f);
        undoLayout();
        Check("undo restores the old layout values", c.AutoSize && c.ColumnPadding == 30f && !c.ShowHeaders);
        Check("undo restores the old column alignments",
              c.MechanicHAlign == HAlign.Right && c.MechanicVAlign == VAlign.Center && c.MitigationHAlign == HAlign.Center
              && c.MitigationVAlign == VAlign.Bottom && c.NotesHAlign == HAlign.Center && c.NotesVAlign == VAlign.Center);
    }

    [Fact]
    public void ChangingOnlyAnAlignmentIsAChangeToTheLayoutSection()
    {
        var onlyAlignment = new Configuration { NotesVAlign = VAlign.Bottom };
        Check("changing only an alignment is a change to the Layout section", !onlyAlignment.IsLayoutDefault && onlyAlignment.IsStyleDefault);
    }

    [Fact]
    public void BehaviorResetAndItsUndo()
    {
        var c = Changed();
        var undoBehavior = c.ResetBehavior();
        Check("Behavior reset restores behavior, cutscene option included",
              c.IsBehaviorDefault && c.AutoSwitch && c.AutoHideOverlay && !c.HideDuringCutscenes);
        undoBehavior();
        Check("undo restores the old behavior values", !c.AutoSwitch && !c.AutoHideOverlay && c.HideDuringCutscenes);
    }

    [Fact]
    public void AlignmentOffsets()
    {
        Check("left and top move nothing", Alignment.Offset(100, 40, Alignment.Factor(HAlign.Left)) == 0 && Alignment.Offset(100, 40, Alignment.Factor(VAlign.Top)) == 0);
        Check("center moves half of the spare room", Alignment.Offset(100, 40, Alignment.Factor(HAlign.Center)) == 30f && Alignment.Offset(50, 20, Alignment.Factor(VAlign.Center)) == 15f);
        Check("right and bottom move all of the spare room", Alignment.Offset(100, 40, Alignment.Factor(HAlign.Right)) == 60f && Alignment.Offset(50, 20, Alignment.Factor(VAlign.Bottom)) == 30f);
        Check("content as big as the space, or bigger, never moves (and never goes negative)",
              Alignment.Offset(40, 40, 1f) == 0 && Alignment.Offset(30, 40, 0.5f) == 0 && Alignment.Offset(0, 40, 1f) == 0);
    }

    [Fact]
    public void AutoSelectOpensAndHidesTheOverlay()
    {
        var c = new Configuration(); // auto-select, auto open and auto hide all on

        Check("entering a duty with a sheet opens a hidden overlay", c.OverlayChangeFor(matched: true, visible: false, openedByUs: false) == OverlayChange.Show);
        Check("...and does nothing if it is already up", c.OverlayChangeFor(true, true, false) == OverlayChange.None);
        Check("leaving hides an overlay the plugin opened", c.OverlayChangeFor(false, true, true) == OverlayChange.Hide);
        Check("leaving never hides an overlay you opened yourself", c.OverlayChangeFor(false, true, false) == OverlayChange.None);
        Check("leaving does nothing if it is already hidden", c.OverlayChangeFor(false, false, true) == OverlayChange.None);

        c.AutoHideOverlay = false;
        Check("auto hide off: it stays up when you leave", c.OverlayChangeFor(false, true, true) == OverlayChange.None);
        c.AutoHideOverlay = true; c.AutoShowOverlay = false;
        Check("auto open off: it is not opened", c.OverlayChangeFor(true, false, false) == OverlayChange.None);
        c.AutoShowOverlay = true; c.AutoSwitch = false;
        Check("auto-select off: nothing happens at all",
              c.OverlayChangeFor(true, false, false) == OverlayChange.None && c.OverlayChangeFor(false, true, true) == OverlayChange.None);
    }

    [Fact]
    public void AnyRowThatIsOnIsContent()
    {
        // Whether a row shows is decided by its tick alone, not by whether it has anything written in it.
        var node = new SheetNode { Name = "t" };
        Check("a sheet with no rows has no content", !node.HasContent() && !new SheetNode().HasContent());
        node.Rows.Add(new SheetRow { IsNote = true, Text = "  " });
        Check("an empty note row that is on counts as content", node.HasContent());
        Check("an empty mechanic row that is on counts as content", new SheetNode { Rows = { new SheetRow() } }.HasContent());
        node.Rows.Add(new SheetRow { IsNote = true, Text = "Stack on the marker" });
        Check("a filled note row counts as content", node.HasContent());
    }

    [Fact]
    public void RowsThatAreSwitchedOffDoNotCount()
    {
        // Rows can be switched off: they no longer count, and a copy keeps the switch.
        var offRows = new SheetNode();
        offRows.Rows.Add(new SheetRow { Mechanic = "Raidwide", Enabled = false });
        offRows.Rows.Add(new SheetRow { IsNote = true, Text = "careful", Enabled = false });
        Check("a sheet whose rows are all off has nothing to show", !offRows.HasContent());
        offRows.Rows.Add(new SheetRow { Mechanic = "Stack" });
        Check("a row that is on brings the sheet back", offRows.HasContent());
        Check("a new row is on", new SheetRow().Enabled && new SheetRow { IsNote = true }.Enabled);
        var rowCopy = offRows.Rows[0].Clone();
        Check("a copy of a row keeps everything, including that it is off",
              !rowCopy.Enabled && rowCopy.Mechanic == "Raidwide" && !ReferenceEquals(rowCopy, offRows.Rows[0]));
        Check("a copy of a sheet keeps which rows are off", offRows.Clone().Rows.Select(r => r.Enabled).SequenceEqual(new[] { false, false, true }));
    }

    [Fact]
    public void ARowCanBeInsertedBelowAnotherOne()
    {
        SheetNode Three() => new() { Rows = { new SheetRow { Mechanic = "A" }, new SheetRow { Mechanic = "B" }, new SheetRow { Mechanic = "C" } } };
        string Order(SheetNode n) => string.Join(",", n.Rows.Select(r => r.IsNote ? "note" : r.Mechanic == "" ? "new" : r.Mechanic));

        var first = Three(); first.InsertRowBelow(0, false);
        var middle = Three(); middle.InsertRowBelow(1, false);
        var last = Three(); last.InsertRowBelow(2, false);
        Check("below the first row goes second", Order(first) == "A,new,B,C");
        Check("below a middle row goes right after it", Order(middle) == "A,B,new,C");
        Check("below the last row goes last", Order(last) == "A,B,C,new");

        var added = Three().InsertRowBelow(0, isNote: false);
        Check("it is an empty mechanic row, switched on", !added.IsNote && added.Mechanic == "" && added.Notes == "" && added.Actions.Count == 0 && added.Enabled);
        var note = Three().InsertRowBelow(1, isNote: true);
        Check("or an empty note row, switched on", note.IsNote && note.Text == "" && note.Enabled);

        var nothing = new SheetNode(); nothing.InsertRowBelow(0, false);
        var pastEnd = Three(); pastEnd.InsertRowBelow(99, false);
        var negative = Three(); negative.InsertRowBelow(-1, false);
        Check("in an empty sheet it is the only row", nothing.Rows.Count == 1);
        Check("an index past the end, or negative, adds it at the end", Order(pastEnd) == "A,B,C,new" && Order(negative) == "A,B,C,new");

        var returned = Three();
        var row = returned.InsertRowBelow(0, false);
        Check("the row that is returned is the one in the list, so an undo can find it", ReferenceEquals(returned.Rows[1], row));
    }

    [Fact]
    public void ARowCanBeInsertedAtAnyPosition()
    {
        SheetNode Three() => new() { Rows = { new SheetRow { Mechanic = "A" }, new SheetRow { Mechanic = "B" }, new SheetRow { Mechanic = "C" } } };
        string Order(SheetNode n) => string.Join(",", n.Rows.Select(r => r.IsNote ? "note" : r.Mechanic == "" ? "new" : r.Mechanic));

        var aboveFirst = Three(); aboveFirst.InsertRowAt(0, false);
        var aboveMiddle = Three(); aboveMiddle.InsertRowAt(1, false);
        var belowLast = Three(); belowLast.InsertRowAt(3, false);
        Check("above the first row goes first", Order(aboveFirst) == "new,A,B,C");
        Check("above a middle row goes right before it", Order(aboveMiddle) == "A,new,B,C");
        Check("below the last row (position 3) goes last", Order(belowLast) == "A,B,C,new");

        var beforeStart = Three(); beforeStart.InsertRowAt(-5, false);
        var afterEnd = Three(); afterEnd.InsertRowAt(99, false);
        Check("a position before the list is brought to the start", Order(beforeStart) == "new,A,B,C");
        Check("a position after the list is brought to the end", Order(afterEnd) == "A,B,C,new");

        var note = Three().InsertRowAt(1, isNote: true);
        Check("it can be a note row", note.IsNote && note.Text == "" && note.Enabled);
        var inEmpty = new SheetNode(); inEmpty.InsertRowAt(0, false);
        Check("in an empty sheet it is the only row", inEmpty.Rows.Count == 1);
    }

    [Fact]
    public void ACloneIsADeepCopyWithFreshIds()
    {
        // Editing the copy must never touch the original.
        var original = new SheetNode { Name = "TEA", Level = 80, DutyId = 694, JobId = 21 };
        original.Rows.Add(new SheetRow { Mechanic = "Raidwide", Actions = { 7531 }, Notes = "n" });
        original.Rows.Add(new SheetRow { IsNote = true, Text = "a note" });
        original.Children.Add(new SheetNode { Name = "Phase 1", Rows = { new SheetRow { Mechanic = "m", Actions = { 3551 } } } });

        var copy = original.Clone("copy");
        Check("a clone keeps everything, note rows included",
              copy.Name == "copy" && copy.Level == 80 && copy.DutyId == 694 && copy.JobId == 21
              && copy.Rows.Count == 2 && copy.Rows[1].IsNote && copy.Rows[1].Text == "a note" && copy.Children.Count == 1);
        Check("a clone has new ids all the way down", copy.Id != original.Id && copy.Children[0].Id != original.Children[0].Id);
        copy.Rows[0].Actions.Add(1); copy.Children[0].Rows[0].Mechanic = "changed";
        Check("editing a clone leaves the original alone", original.Rows[0].Actions.Count == 1 && original.Children[0].Rows[0].Mechanic == "m");
    }
}
