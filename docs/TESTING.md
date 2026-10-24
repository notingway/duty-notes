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

At the time of writing: **78 tests, 317 checks, all passing.** (This is the one place the count is recorded; update it here when you add tests.
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
| `AnchorTrackerTests` (the overlay's anchor point) | The anchor stays within a pixel through grow, shrink and in-between sizes with fractional pixels, for six anchors; the top-left anchor never moves the window; a dragged window's new place becomes the anchor, but a window that ImGui pushes back onto the screen (no mouse button held) goes back to its anchor; changing the anchor setting leaves the window where it is; holding and reset |
| `HelpTests` (the command list) | The Help tab's list is well formed, matches the commands `Plugin.OnCommand` handles (it reads `Plugin.cs`), and matches the README |
| `SyncedTextTests` (skill descriptions at a level) | The level and job switches pick the right branch (Thrill of Battle, Intervention, Reprisal, copied from the game data); colours and switches on anything else are kept; with no job, job switches are left for the game; the trait texts that set a recast or a number of charges, and ones that don't |
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

Everything the plugin did up to the SDK switch, and everything added since, was run through in game and passed (the last full pass was on 24 October 2026).

- **Loading and settings:** the plugin loads as a dev plugin and its settings were copied over from the old name (**Mitigation Sheet** to **Duty Notes**). Typing in text boxes works. (A mid-session Dalamud update once made typing go to the game until the game was restarted.) Dragged sliders and typing are kept (settings are written after a short pause, and at once after a click). Each Settings section's Reset restores its defaults, is greyed out at defaults, and Undo / Ctrl+Z takes it back.
- **Auto-select:** entering the matching duty on the matching job selects the encounter and opens the overlay; leaving the duty hides an overlay that auto-select opened and keeps one you opened yourself; hiding during cutscenes works on and off; level follows the duty, and the duty type dropdown works.
- **The overlay:** the shared table, the column headers under each phase title, the translucent title bars, note rows (full width, wrapping, no extra column), sharp scaled text, auto-size with the anchor point staying put, the right-click menu (and Ctrl + right-click on a locked overlay), Escape not closing it, and it coming back where it was after closing with X.
- **Editing:** dragging actions (reordering, between rows, right-click to remove) and rows (with the yellow insertion line); Limit Break; the tick that switches a phase or a row off (the overlay, the full encounter, the Notes column and Previous / Next / Reset respect it); column alignment on the 3 x 3 grids; the editor's buttons under the name (Duplicate, Export, Delete, Ctrl + click to delete without asking; Move up and Move down have been replaced by dragging); Duplicate, delete, undo and import leave a valid selection; deleting phases keeps the selection in their own encounter.
- **Phases:** the phase buttons under the overlay (also while the editor is open), Previous / Next greyed out at the ends with a tooltip.
- **Sharing:** Export and Import through the paste window.
- **Help tab:** the commands, the macro icons, and the copy icons.
- **Row controls:** the right-click menu on a row (add a mechanic or note row above or below, duplicate, delete), the banded rows, the duplicate and delete icons after a divider, and the add buttons at the bottom.
- **Dragging a row:** by its background (no grip icon), with a yellow outline on the dragged row, no tooltip, the yellow insertion line, and every box, tick and icon in the row still clickable; right-click on the background opens the row menu.
- **Stepping between phases:** with auto-size on, Next and Previous swap the phase with no visible flicker (the overlay is hidden, background included, for the two frames it takes to settle).
- **Startup and position:** the overlay starts hidden every time the game starts, its position is remembered between sessions (by Dalamud), and a saved position that is off the screen is moved back on it when the overlay opens (the log says so).
- **Delete row:** a note row's delete button says Delete row, the same as a mechanic row's. (The row buttons have since become icons: see Row controls.)
- **Rows in phases, and the sheet pane:** an encounter has no table of its own (selecting one shows its settings; **+ Encounter** starts with a first phase); the name, Duty, Job and Level rows with labels on the left, and the icons under the line; phase lines right after their tick; the labelled **Show phase** and **Show row** ticks; each row reading Mechanic, Actions (on a backdrop), Notes with its own controls on the last line; the overlay's column titled Actions; **Use job level** (the level your character has on the job); rows that are on showing in the overlay even when empty (the tick alone decides).
- **Installer and level buttons:** the plugin installer shows the plugin's icon (after a game restart) and the command text as two lines, `/dutynotes` and `/dutynotes edit`; under Level the **Duty** and **Job** buttons sync the level (greyed out until a duty or a job is chosen), and a chosen job has a clear X.
- **Second forms in the action picker:** Sage's Eukrasian Diagnosis, Eukrasian Prognosis and Eukrasian Prognosis II, Dancer's Improvised Finish and Scholar's Seraph actions show in the picker and can be picked (confirmed on the dev build before 0.1.3).
- **Text scaling for a player with another global UI scale:** with 0.1.4 the text sizes follow the sliders for the tester who reported the fallback (reported working after a first test that showed blurry, stretched text; the cause of the first result is not known). 125% and 150% were later confirmed by the owner (next item); 200% has not been checked. (The per-font "ready" log lines that 0.1.4 wrote were removed again; a font that fails to build is still logged as a warning, search `dalamud.log` for `could not be built`.)
- **Text sizes at a Dalamud global scale other than 100%:** with Global Scale at 125% to 150%, the text is sharp, the text size sliders still change it, and the plugin's sizes multiply with the global scale as intended (confirmed on 24 October 2026 with 0.1.6).
- **No encounters:** deleting every encounter, with the empty-state messages, Undo, Import into the empty list, and the list staying empty after a restart.
- **Skill tooltips at the encounter's level:** hovering a skill in the overlay shows the full card (icon, name, kind, cast and recast, range and radius, the description in the game's colours, and when it is learned) at the encounter's level and job (confirmed on 24 October 2026).
- **Phase controls in the sheet pane:** a selected phase shows its own tick (the same setting as the one in the list), a divider, then Duplicate and Delete icons; an encounter has Duplicate, Export and Delete, no tick. Duplicate puts a copy (`<name> (copy)`) right below and selects it, the Delete tooltip says `this phase` or `this encounter`, and Ctrl + click deletes without asking.
- **Hint at the bottom of the list:** **Drag to reorder.** sits at the bottom of the left pane; with many encounters the list scrolls above it, and dragging and clicking still work after scrolling.
- **Overlay with an encounter selected:** with the editor open and an encounter selected, the overlay shows its first phase that is on (or all of them with **Show full encounter**); a selected phase shows that phase; an encounter with no phases, or only empty ones, says `No rows yet`.
- **Action icon spacing:** the action icons in a row sit close together, with the wider gap before the **+** button, and a dropped action lands where the line shows.
- **+ Encounter selects the encounter:** the new encounter is highlighted with **New phase** under it, the right pane shows its settings and "Pick one of its phases in the list to edit its rows.", and **+ Phase** adds to it.
- **Sharper text while a size builds:** while a slider's exact-size font is being built, the text is stretched from the nearest ready size, then snaps to the exact font, with no flicker.
- **Anchor through full encounter on and off:** with auto-size on, toggling **Show full encounter** and stepping phases keeps the overlay's anchor point in place for every anchor; the window never leaves the screen, grows and shrinks around a new place after a drag, does not move when the anchor is changed, and reopens where it was.
- **Icons under a sheet's name:** an encounter has Duplicate, Export and Delete icons (red when hovered) with tooltips, a phase only Delete under its Name; they do what the buttons did (Export says so on the status line, Delete asks, Ctrl + click does not), sized and spaced like the row icons.
- **Dragging in the tree:** an encounter drags as one block with its phases (yellow outline, a steady yellow line where it lands, the line above the first encounter fully visible); a phase moves only inside its own encounter; the order is kept after a reload and Undo puts it back; clicks, the arrow and the phase tick still work; Move up / Move down are gone.
- **Dragging an action:** the dragged icon is outlined in yellow where it sits, a yellow vertical line shows where it will land (before **+** for the end), and dropping an action the other row already has does nothing.
- **Banded rows, no lines between:** every row has the same lighter band with a small even gap and no line between, the controls sit inside the band with a margin, one line above the first row and one below the last, and an empty sheet shows "No rows yet." and the buttons.
- **Reload keeps the overlay:** reloading the plugin keeps the overlay showing (where it was) or hidden, as it was; starting the game hides it.

### Built, but not yet confirmed in game

Nothing at the moment: everything in the plugin has been run in game and passed. When something new is built, add it here as a row (the feature and what to check), and move it up to the list above once the owner has confirmed it.

| Feature | What to check |
| --- | --- |

When something fails, check the Dalamud log first (`%APPDATA%\XIVLauncher\dalamud.log`; search for `DutyNotes`, `Switched to`, and
`ImGui assertion failed`). Auto-select writes a `Switched to "<name>" for territory N, job N` line.
