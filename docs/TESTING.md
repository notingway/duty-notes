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

At the time of writing: **48 tests, 207 checks, all passing.** (This is the one place the count is recorded; update it here when you add tests.
`dotnet test` reports the tests; the checks are the `Check` calls.)

| Test class | What it checks |
| --- | --- |
| `NavigationTests` (stepping between phases) | Next, Previous and Reset (including the ends and encounters without phases), fallbacks for deleted ids, how the editor's selection drives the navigation, stepping from the encounter view |
| `SettingsTests` (defaults, resets, overlay visibility) | The defaults, each section's Reset and its undo, "is this section at its defaults", the show and hide decisions of auto-select, sheet helpers (`HasContent`, `Clone`) |
| `SerializationTests` (saving and loading settings) | Loading the way Dalamud does, repeated, with no duplication (a regression test for a real bug); the one-time rewrite of the old type names; older and odd files; healing deep or duplicated trees |
| `SaveSchedulerTests` (when a pending save is written) | Nothing is pending until something changes; a change is due after the quiet time; a burst of changes is one save; a run of changes that never pauses is still saved at the longest wait; a save clears what was pending and the next change starts a fresh wait |
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

- The plugin loads as a dev plugin, and its settings were copied over from the old name (**Mitigation Sheet** to **Duty Notes**).
- Typing in text boxes works. (A mid-session Dalamud update once made typing go to the game until the game was restarted.)
- Entering the matching duty on the matching job selects the encounter and opens the overlay.
- Dragging skills to reorder them, dragging a skill between rows, and right-clicking a skill to remove it.
- Dragging rows to reorder them, with the yellow insertion line (up and down).
- The overlay's shared table, the column headers under each phase title, and the translucent title bars.
- The Settings tab layout (Behavior, Layout, Style, each with a Reset button).
- The phase buttons under the overlay, and that they also work while the editor is open.
- **Export**: the green message, and the text starts `DutyNotes:1:`.
- **Import** through the paste window (confirmed with a larger Warrior sheet, since replaced; the made-up `samples/example-encounter.txt` that replaced it is tested outside the game only).
- **Limit Break** in the skill picker: one skill, for any job.
- **Saving while you drag:** a dragged slider and typing are kept (settings are written after a short pause, and at once after a click).
- **Close with X, reopen:** the overlay comes back where it was (it used to reappear partly off the screen).
- **Phases on and off:** the tick in the tree; the overlay, the full encounter and the Previous / Next / Reset steps skip an off phase.

### Built, but not yet confirmed in game

Tested outside the game only. Confirm each, and then move it up to the list above.

| Feature | What to check |
| --- | --- |
| **Auto hide** | The overlay was opened by auto-select: leaving the duty hides it. One you opened yourself stays |
| **Hide during cutscenes** | Off: the overlay stays visible in a cutscene. On: it hides |
| **Escape** | Pressing Escape with the overlay focused does not close it |
| **Right-click menu** | Right-click on the overlay opens the menu; with it locked, **Ctrl + right-click** does, and the overlay is click-through again after Ctrl is released |
| **Note rows** | A note row draws across the full width of the table, wraps at the table's width, and does not widen the first column or create a Notes column. **Note row text** changes its size |
| **Level follows the duty** | Choosing a duty sets the encounter's level; editing the level shows **Use duty level (N)**; the skill picker uses the encounter's level for phases |
| **Duty type dropdown** | Party Finder icons in the list and in the closed box with a gap, the box text stays in place while the list is open, and picking an entry closes the list |
| **Previous / Next at the ends** | Greyed out on the first and last phase, with a tooltip saying why |
| **Auto-size and anchor** | The overlay resizes to its content while the chosen anchor point stays put |
| **Scaled text** | Title, mechanic, notes and note row text are sharp (not blurry) at sizes above and below 1 |
| **Help tab** | The editor's third tab, Help: the steps for making a macro, then each command with a copy icon: next, prev and reset show the suggested macro icon (right arrow, left arrow, loop: the same pictures as in the game's macro Icons window), a suggested macro name and a copy icon (hover shows the macro text) instead, the other commands have no icon (a green "Copied" shows for a moment). The copied text pastes into chat and into a macro line, and a macro with `/dutynotes next` moves the overlay. Check the Macros menu wording in the first step against the game |
| **Delete keeps you in place** | Delete the phases of the bottom encounter one at a time (with Ctrl + click): the selection moves to the neighbouring phase, then to that encounter once its last phase is gone, never to the top encounter. Undo brings each one back and selects it |
| **No encounters** | Delete every encounter (Ctrl + click is quick): the editor shows "No encounters yet" with the + Encounter / Import hint, + Phase is greyed out, the overlay says to use `/dutynotes edit`, and nothing breaks (no errors in the log). Undo brings one back. Restart the game: it is still empty, with no "Encounter 1" returning. Import into the empty list works |
| **Editor buttons** | The left pane has only + Encounter, + Phase and Import. Under the name on the right: Move up, Move down, Duplicate and Export (encounters only), and Delete, on one line. Ctrl + click on Delete deletes at once, without the question (Undo brings it back). A phase has no Export button (only an encounter does); Delete is always available. The green or red message from Import and Export shows under those buttons |
| **Rows on and off** | A tick at the top of each row in the editor, next to the drag grip. Unticking dims the row. The overlay leaves it out straight away, with the editor open or closed; the Notes column disappears if the only notes belonged to off rows; a note row can be switched off too. Duplicate keeps the setting. Export, then import, keeps it |
| **Column alignment** | Settings > Layout > Column alignment: one 3 x 3 grid of dots per column; the dot you pick moves its text (every line of a wrapped note) and icons, and the column header with it; top, center and bottom move a short cell within a tall row (for example a one-line mechanic beside a long note). With auto-size on, columns still size to their content and the overlay does not creep wider; Reset puts every column back to left and top |
| **Reset buttons** | Each section's Reset restores its defaults, is greyed out at defaults, and Undo / Ctrl+Z takes it back |
| **Sync of the active encounter on duplicate and delete** | Duplicate, delete, undo and import leave a valid selection and a sensible overlay |

When something fails, check the Dalamud log first (`%APPDATA%\XIVLauncher\dalamud.log`; search for `DutyNotes`, `Switched to`, and
`ImGui assertion failed`). Auto-select writes a `Switched to "<name>" for territory N, job N` line.
