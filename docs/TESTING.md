# Testing

Two kinds of checking: automated tests for the logic that doesn't need the game, and a manual checklist for everything
that does. The plugin draws with ImGui inside the game, so most of what you *see* can only be checked in game.

## Automated tests

```powershell
dotnet test DutyNotes.Tests -c Release
```

The tests use **xunit**, so the first build downloads three NuGet packages (`Microsoft.NET.Test.Sdk`, `xunit` and `xunit.runner.visualstudio`).
They also need the Dalamud files on disk: the project references `Dalamud.dll` and `Newtonsoft.Json.dll` from your XIVLauncher install
(`%APPDATA%\XIVLauncher\addon\Hooks\dev\`, or set `/p:DalamudLibPath=...`), because `Configuration.cs` implements Dalamud's
`IPluginConfiguration`. The command exits with 0 when everything passes and 1 otherwise. For a readable list of every test, add
`--logger "console;verbosity=normal"`, and to run some of them, `--filter "FullyQualifiedName~Navigation"` (any part of a class or test name).

**How a test is written.** A test is a method marked `[Fact]`, in a class that inherits `CheckBase`. It calls `Check("what should be true", condition)`
as many times as it likes. A failed check does not stop the test: the test fails at the end and lists every label that failed, so one run
shows everything that is wrong. Each test builds its own data, so they run in any order and in parallel.

At the time of writing: **68 tests, 277 checks, all passing.** (This is the one place the count is recorded; update it here when you add tests.
`dotnet test` reports the tests; the checks are the `Check` calls.)

| Test class | What it checks |
| --- | --- |
| `NavigationTests` (stepping between phases) | Next, Previous and Reset (including the ends and encounters without phases), fallbacks for deleted ids, how the editor's selection drives the navigation, stepping from the encounter view |
| `SettingsTests` (defaults, resets, overlay visibility) | The defaults, each section's Reset and its undo, "is this section at its defaults", the show and hide decisions of auto-select, sheet helpers (`HasContent`, `Clone`) |
| `SerializationTests` (saving and loading settings) | Loading the way Dalamud does, repeated, with no duplication (a regression test for a real bug); the one-time rewrite of the old type names; older and odd files; healing deep or duplicated trees |
| `SettingsTests` also covers inserting a row below another | Below the first, a middle and the last row; an empty mechanic or note row, switched on; an empty sheet; an index past the end or negative; the returned row is the one in the list |
| `SaveSchedulerTests` (when a pending save is written) | Nothing is pending until something changes; a change is due after the quiet time; a burst of changes is one save; a run of changes that never pauses is still saved at the longest wait; a save clears what was pending and the next change starts a fresh wait |
| `LayoutSettlerTests` (the overlay catching up with new content) | A window is shown by default; a restart hides it for a minimum number of frames and until its size repeats (or not at all, for the quick kind); a width that arrives a frame late does not show it too early; a size that never settles, or a window that is never drawn, is shown after at most 12 frames; a restart starts a fresh count |
| `ScreenPlacementTests` (keeping a window reachable) | A window that is already reachable is left alone; one above the top, or too far off any side, is moved back so its title bar is on the screen and 80 pixels of it show; a second screen keeps its own origin; tiny, huge and unknown-size cases are handled |
| `HelpTests` (the command list) | The Help tab's list is well formed, matches the commands `Plugin.OnCommand` handles (it reads `Plugin.cs`), and matches the README |
| `SharingTests` (export and import) | Round trip, what this game version doesn't know, broken text, which message each kind of header gets (a newer version versus not ours), and hostile text (a gzip bomb, type-name tricks, oversized counts, control characters) |

**Not covered, because it needs the game:** everything drawn with ImGui, Dalamud services (data, textures, client state), the game's
data catalogs (`ActionCatalog`, `DutyCatalog`, `JobCatalog`, `FontCache`), the clipboard, and the file dialogs.

### Adding a test

Put new logic that is worth testing in a file with no Dalamud services or ImGui (`Configuration.cs`, `Sharing.cs`,
or a new pure file added to `DutyNotes.Tests.csproj`), then add a `[Fact]` to a test class (or a new class that inherits `CheckBase`) and
make its checks with `Check("what should be true", condition)`. Give each test its own data, never data left over from another test. For
a bug that was found in game, reproduce it in a test first.

## Manual checklist (needs the game)

Run through these after a change in the area. "Dev plugin" means: build, copy to the dev folder, reload from Installed Dev Plugins.

### Confirmed in game by the owner

Everything the plugin did up to the SDK switch, and everything added since, was run through in game and passed (the last full pass was on 18 October 2026).

- **Loading and settings:** the plugin loads as a dev plugin and its settings were copied over from the old name (**Mitigation Sheet** to **Duty Notes**). Typing in text boxes works. (A mid-session Dalamud update once made typing go to the game until the game was restarted.) Dragged sliders and typing are kept (settings are written after a short pause, and at once after a click). Each Settings section's Reset restores its defaults, is greyed out at defaults, and Undo / Ctrl+Z takes it back.
- **Auto-select:** entering the matching duty on the matching job selects the encounter and opens the overlay; leaving the duty hides an overlay that auto-select opened and keeps one you opened yourself; hiding during cutscenes works on and off; level follows the duty, **Use duty level (N)** appears when you override it, and the duty type dropdown works.
- **The overlay:** the shared table, the column headers under each phase title, the translucent title bars, note rows (full width, wrapping, no extra column), sharp scaled text, auto-size with the anchor point staying put, the right-click menu (and Ctrl + right-click on a locked overlay), Escape not closing it, and it coming back where it was after closing with X.
- **Editing:** dragging actions (reordering, between rows, right-click to remove) and rows (with the yellow insertion line); Limit Break; the tick that switches a phase or a row off (the overlay, the full encounter, the Notes column and Previous / Next / Reset respect it); column alignment on the 3 x 3 grids; the editor's buttons under the name (Duplicate, Export, Delete, Ctrl + click to delete without asking; Move up and Move down have been replaced by dragging); Duplicate, delete, undo and import leave a valid selection; deleting phases keeps the selection in their own encounter.
- **Phases:** the phase buttons under the overlay (also while the editor is open), Previous / Next greyed out at the ends with a tooltip.
- **Sharing:** Export and Import through the paste window.
- **Help tab:** the commands, the macro icons, and the copy icons.
- **Row controls:** the right-click menu on a row (add a mechanic or note row above or below, duplicate, delete), the banded rows, the duplicate and delete icons after a divider, and the add buttons at the bottom.
- **Dragging a row:** by its background (no grip icon), with a yellow outline on the dragged row, no tooltip, the yellow insertion line, and every box, tick and icon in the row still clickable; right-click on the background opens the row menu.
- **Stepping between phases:** with auto-size on, Next and Previous swap the phase with no visible flicker (the overlay is hidden, background included, for the two frames it takes to settle).
- **Startup and position:** the overlay starts hidden every time the game starts, its position is remembered between sessions (by Dalamud), and a saved position that is off the screen is moved back on it when the overlay opens (the log says so).
- **Delete row:** a note row's delete button says Delete row, the same as a mechanic row's. (The row buttons have since become icons: see the unconfirmed table.)
- **No encounters:** deleting every encounter, with the empty-state messages, Undo, Import into the empty list, and the list staying empty after a restart.

### Built, but not yet confirmed in game

Nothing at the moment: everything in the plugin has been run in game and passed. When something new is built, add it here as a row (the feature and what to check), and move it up to the list above once the owner has confirmed it.

| Feature | What to check |
| --- | --- |
| **Actions column title** | The overlay's column header (with column headers on) reads **Mechanic / Actions / Notes**, and the Settings > Layout labels (the Show headers help, the column spacing help and the three alignment grids) say Actions instead of Mitigation. Saved alignment settings still apply (the saved names did not change) |
| **Row layout** | A mechanic row reads: Mechanic (label and box), Actions (label and the action icons with the + button, which is exactly as tall as an icon button and level with it, on a backdrop in the same colour and rounding as the boxes, as wide as they are), Notes, then a last line with **Show row**, a divider, Duplicate and Delete. A note row: Note (with its (?)) and the text box, then the same last line. The boxes all start in one column. A row that is switched off dims its fields but not its last line. The space between the Notes box and the last line equals the space between the last line and the bottom of the band. A new row still puts the cursor in its first box; right-click on the background, dragging by the background and the outline still work; a narrow window does not cut the controls |
| **Tick labels** | The tick in a selected phase's sheet pane reads **Show phase**, and every row's tick reads **Show row**, as labelled checkboxes like Show overlay at the top; the tick in the list stays a bare checkbox. The tooltips (they start with the same words) still say what unticking does. Check that the longer row line still fits a narrow editor window and that the divider and icons follow it |
| **Phase controls in the sheet pane** | A selected phase shows, under its name and a line, its own tick, a divider, then Duplicate and Delete icons (an encounter has Duplicate, Export and Delete, no tick). The tick is the same setting as the one in the list: ticking one updates the other at once. Duplicate puts a copy (`<name> (copy)`, same rows, same on/off) right below the phase and selects it. The Delete tooltip says `this phase` for a phase and `this encounter` for an encounter, and does not mention Undo; Ctrl + click still deletes without asking |
| **Phase lines in the tree** | A phase is a plain line right after its tick (no empty arrow space between them). Clicking selects it, the selected one is highlighted, a phase that is off is dim, and dragging a phase to reorder it (outline and line) still works. Encounters keep their arrow and open and close as before |
| **Overlay with an encounter selected** | With the editor open and an encounter (not a phase) selected, the overlay shows that encounter's first phase that is on, or all its phases with **Show full encounter** on, instead of \\No rows yet\\. Selecting a phase shows that phase; with the full encounter on it shows every phase that is on (and the selected one even if it is off). Playing is unchanged apart from nothing being pinned above the phase. An encounter with no phases, or only empty ones, still says `No rows yet` |
| **Use duty level button** | Next to **Use my level** there is always a **Use duty level** button. With no duty chosen it is greyed out (its tooltip says to choose a duty). With a duty it shows the duty's level in brackets, and clicking it sets the level to that, also after **Use my level** or a typed level, and also when the level already matches (nothing changes) |
| **Sheet pane layout** | The name box and the Level box have their labels to their left (**Encounter** for an encounter, **Phase** for a phase), lined up with the Duty and Job rows. For an encounter the order is name, Duty, Job, Level, the hint line, a line, then the Duplicate / Export / Delete icons (the hint is above the line, the icons below it); for a phase the **Show phase** tick, a divider, Duplicate and Delete sit on the name's line, to the right of the box, then a line, then the rows. The Duty and Job buttons moved right a little (the label column is wider). Export's status message still shows by the icons |
| **Rows in phases only** | **+ Encounter** creates an encounter with `Phase 1` and selects that phase, so you can add rows at once. Selecting an encounter shows only its settings (name, icons, duty, job, level) and a line saying to pick a phase; there is no row list or Add row buttons. A phase still has the row list. Importing samples/example-encounter.txt gives 3 phases, the first starting with a note row. Import of old text with rows on the encounter makes a first phase with its name (automated test) |
| **Icons under a sheet's name** | An encounter shows three icons under its name: Duplicate (the same icon as a row's), Export and Delete (red when hovered), each with a tooltip saying what it does; a phase shows only Delete, under its Name. They do what the buttons did: Duplicate copies the encounter below it, Export copies the text to the clipboard (the status line says so), Delete asks first, and Ctrl + click deletes without asking. The icon sizes and spacing match the row icons |
| **Dragging in the tree** | Drag an encounter by its name: the yellow outline covers the encounter and its phases (when they are shown). Hovering the encounter or any of its phases, or the small gaps between them, counts as hovering it as one block (the line must not blink off or move while the pointer travels over it), and a yellow line shows where the dragged encounter will land: above that encounter when moving up, below its last phase when moving down. Releasing moves it; the order is kept after a reload and Undo / Ctrl+Z puts it back. A phase moves the same way inside its own encounter only; over another encounter's phase, or an encounter, no line shows and nothing happens. A plain click still selects, the arrow still opens and closes, the phase tick still works. A phase's **Delete** sits under its Name (not beside it). The auto-select tooltip says to reorder by dragging. Move up / Move down are gone |
| **Dragging an action** | The icon being dragged is outlined in yellow where it sits (the icon following the pointer stays). Over another icon a yellow vertical line shows where it will land: after that icon when moving later in the same row, before it when moving earlier or from another row. Over the **+** button the line is before it (the action goes to the end). Releasing puts it there; the old yellow box on the target icon is gone. Dropping an action the other row already has does nothing |
| **Banded rows, no lines between** | In a sheet's row list every row has the same lighter band behind it, with a small even gap (about four pixels) between one band and the next, and no line between rows. The row's boxes and buttons sit inside the band, with a little space at its left, right, top and bottom (they do not touch its edges). One line above the first row and one below the last, then the **Add mechanic row** and **Add note row** buttons. A sheet with no rows shows "No rows yet." and the buttons, without the line below |
| **Reload keeps the overlay** | With the overlay showing, reload the plugin (Installed Dev Plugins): it is still showing, where it was. With it hidden, reload: it stays hidden. Quit the game and start it again: it is hidden, as before. (The first reload after installing this build hides it once, because the build already running did not leave the marker.) |
When something fails, check the Dalamud log first (`%APPDATA%\XIVLauncher\dalamud.log`; search for `DutyNotes`, `Switched to`, and
`ImGui assertion failed`). Auto-select writes a `Switched to "<name>" for territory N, job N` line.
