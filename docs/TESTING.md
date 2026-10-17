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

At the time of writing: **65 tests, 253 checks, all passing.** (This is the one place the count is recorded; update it here when you add tests.
`dotnet test` reports the tests; the checks are the `Check` calls.)

| Test class | What it checks |
| --- | --- |
| `NavigationTests` (stepping between phases) | Next, Previous and Reset (including the ends and encounters without phases), fallbacks for deleted ids, how the editor's selection drives the navigation, stepping from the encounter view |
| `SettingsTests` (defaults, resets, overlay visibility) | The defaults, each section's Reset and its undo, "is this section at its defaults", the show and hide decisions of auto-select, sheet helpers (`HasContent`, `Clone`) |
| `SerializationTests` (saving and loading settings) | Loading the way Dalamud does, repeated, with no duplication (a regression test for a real bug); the one-time rewrite of the old type names; older and odd files; healing deep or duplicated trees |
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
- **Editing:** dragging skills (reordering, between rows, right-click to remove) and rows (with the yellow insertion line); Limit Break; the tick that switches a phase or a row off (the overlay, the full encounter, the Notes column and Previous / Next / Reset respect it); column alignment on the 3 x 3 grids; the editor's buttons under the name (Move up, Move down, Duplicate, Export, Delete, Ctrl + click to delete without asking); Duplicate, delete, undo and import leave a valid selection; deleting phases keeps the selection in their own encounter.
- **Phases:** the phase buttons under the overlay (also while the editor is open), Previous / Next greyed out at the ends with a tooltip.
- **Sharing:** Export and Import through the paste window.
- **Help tab:** the commands, the macro icons, and the copy icons.
- **Stepping between phases:** with auto-size on, Next and Previous swap the phase with no visible flicker (the overlay is hidden, background included, for the two frames it takes to settle).
- **Startup and position:** the overlay starts hidden every time the game starts, its position is remembered between sessions (by Dalamud), and a saved position that is off the screen is moved back on it when the overlay opens (the log says so).
- **Delete row:** a note row's delete button says Delete row, the same as a mechanic row's.
- **No encounters:** deleting every encounter, with the empty-state messages, Undo, Import into the empty list, and the list staying empty after a restart.

### Built, but not yet confirmed in game

Nothing at the moment: everything in the plugin has been run in game and passed. When something new is built, add it here as a row (the feature and what to check), and move it up to the list above once the owner has confirmed it.

| Feature | What to check |
| --- | --- |
When something fails, check the Dalamud log first (`%APPDATA%\XIVLauncher\dalamud.log`; search for `DutyNotes`, `Switched to`, and
`ImGui assertion failed`). Auto-select writes a `Switched to "<name>" for territory N, job N` line.
