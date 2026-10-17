# CLAUDE.md

Guidance for continuing development of **Duty Notes**, a Dalamud plugin for FFXIV. Read this first. The README is for
users; this file is for whoever (human or AI session) picks the work up next. Keep it current: when you learn
something that cost time, add it here.

Owner: GitHub handle **notingway** (account not created yet). License: MIT. Nothing is published; the plugin runs
as a dev plugin only. The plugin was originally called "Mitigation Sheet", which explains a few leftover names (see
[Legacy names](#legacy-names-that-must-stay)).

## Start here

| Where | What |
| --- | --- |
| [README.md](README.md) | Features, install, commands, build |
| [ROADMAP.md](ROADMAP.md) | What is left to do, and ideas that were declined |
| [docs/DECISIONS.md](docs/DECISIONS.md) | Why things work the way they do. **Read before changing behavior.** |
| [docs/FORMATS.md](docs/FORMATS.md) | Export text and settings file. Versioned; don't break old text. |
| [docs/GAME-DATA.md](docs/GAME-DATA.md) | What the game's tables contain, and how to inspect them |
| [docs/TESTING.md](docs/TESTING.md) | Automated tests, and which behaviors have only been checked by hand in game |

## Environment

- Windows. **.NET 10 SDK** (10.0.402, installed with `winget install Microsoft.DotNet.SDK.10`). In a shell started before
  the install, `dotnet` may not be on PATH: use `C:\Program Files\dotnet\dotnet.exe`.
- **Dalamud 15** (API 15, .NET 10, ImGui through `Dalamud.Bindings.ImGui`). The plugin project uses **`Dalamud.NET.Sdk/15.0.0`**
  (see `DutyNotes.csproj`). The SDK finds Dalamud, ImGui, Lumina and Newtonsoft.Json in the XIVLauncher install by itself, and is itself
  restored from NuGet, so the first build needs internet. It also **generates the manifest** from the properties in the csproj
  (Author, Name, Punchline, Description, RepoUrl, Tags, Version): there is no hand-written `DutyNotes.json` any more. A Release build writes
  `DutyNotes.dll`, `DutyNotes.json` (generated) and `DutyNotes.deps.json` to `DutyNotes\bin\Release\`, and a release package,
  `DutyNotes\bin\Release\DutyNotes\latest.zip`. The **test project does not use the SDK**: it references `Dalamud.dll` and `Newtonsoft.Json.dll`
  by `HintPath` from `%APPDATA%\XIVLauncher\addon\Hooks\dev\` (property `DalamudLibPath`).
- The game-data probes (below) need the game's `sqpack` folder: `<game install>\game\sqpack`. Ask the owner where the game is installed.
- Settings file: `%APPDATA%\XIVLauncher\pluginConfigs\DutyNotes.json`. Dalamud log: `%APPDATA%\XIVLauncher\dalamud.log`
  (search it for `DutyNotes`, `Switched to`, and `ImGui assertion failed`).

## The development loop

```powershell
# build
dotnet build DutyNotes\DutyNotes.csproj -c Release

# run the automated tests (xunit; exit code 0 when all pass; the current count is in docs/TESTING.md)
dotnet test DutyNotes.Tests -c Release

# deploy to the folder the owner's dev plugin path points at (ask them which), then they reload it in game
$dest = "<the dev plugin folder>"
Get-ChildItem DutyNotes\bin\Release -File | Copy-Item -Destination $dest -Force
```

The dev folder is **not** the repository. The owner added the full path to `DutyNotes.dll` in it under
`/xlsettings > Experimental > Dev Plugin Locations` (it must be an absolute path) and reloads the plugin from
`/xlplugins > Dev Tools > Installed Dev Plugins`. (Their folder also holds a stale `.pdb` from before the SDK switch, which can be deleted.)

You cannot run the game, so **never claim something works in game.** Say "built, tests pass, not checked in game" and
record unverified behavior in [docs/TESTING.md](docs/TESTING.md).

## Architecture

Everything is in `DutyNotes/`. Files marked **pure** make no use of Dalamud services (data, textures, client state) or ImGui,
are compiled into the test project, and must stay that way. "Pure" does **not** mean free of Dalamud assemblies:
`Configuration.cs` implements Dalamud's `IPluginConfiguration`, so the test project references `Dalamud.dll` and
`Newtonsoft.Json.dll` from the XIVLauncher install (`DalamudLibPath`, the same as the plugin). The tests use xunit (NuGet packages, restored on the first build),
and they need the Dalamud files on disk.

| File | Responsibility |
| --- | --- |
| `Plugin.cs` | Entry point. Injects services, owns the catalogs and windows, registers the `/dutynotes` command. `OnFrameworkUpdate` does auto-select and overlay show/hide and applies the cutscene setting. Phase stepping and the one-time settings migration. |
| `Configuration.cs` **pure** | The saved data: `SheetRow`, `SheetNode`, `Configuration`, the `Defaults` class, enums. Also the navigation state, per-section reset methods (each returns an undo closure), and tree helpers. |
| `Sharing.cs` **pure** | Export/import text. `Sharing.Converter` turns untrusted data into real sheets (clamps, caps, drops unknown ids). |
| `OverlayWindow.cs` | The overlay: one shared table for all sections, title rows, optional header rows, `SpanningTextRow` for note rows, the right-click menu, auto-size anchoring. |
| `ScreenPlacement.cs` **pure** | `KeepOnScreen`: moves a window just far enough to be reachable. The overlay calls it on its first frame after opening. Window positions themselves are remembered by Dalamud in `dalamudUI.ini`, not by the plugin. |
| `SaveScheduler.cs` **pure** | Decides when a pending save is due (after a quiet moment, never later than a longest wait). `Plugin.Save()` writes now; `Plugin.SaveSoon()` is for changes that arrive every frame (a dragged slider, typing). |
| `HelpCommands.cs` **pure** | The list of chat commands, what each does, and which get a suggested macro. The Help tab is drawn from it (`HelpTab.cs`). |
| `HelpTab.cs` | The Help tab: how to make a macro, and the commands with Copy buttons. |
| `ControlsWindow.cs` | Reset / Previous / Next strip, a separate window under the overlay. |
| `EditorWindow.cs` | The editor (large). Tabs Sheets and Settings; tree; row editing with drag and drop; the duty, job and skill pickers; the import window; settings groups; undo stack. |
| `ActionCatalog.cs` | Reads the Action sheet, groups skills into upgrade families, answers "which job can use this". |
| `DefensiveSkills.cs` | Hand-curated lists of defensive and healing skill names. |
| `DutyCatalog.cs`, `JobCatalog.cs` | Duties (from ContentFinderCondition) and jobs, for the pickers and for zone matching. |
| `FontCache.cs` | Fonts built at real pixel sizes, for crisp scaled text. |

Where to look inside the two big files:

- `OverlayWindow.cs`: `Draw` decides what to show (editor selection versus navigation, full encounter versus one phase), `DrawTable`
  draws the shared table (title rows, header rows, mechanic rows), `SpanningTextRow` draws note rows, `ScaledText` draws text at a
  size, `DrawContextMenu` is the right-click menu, `KeepAnchorFixed` handles auto-size anchoring.
- `EditorWindow.cs`: `Draw` is the top level (quick toggles, tabs, undo, saving). Sheets tab: `DrawTreePane`, `DrawSheetPane`,
  `DrawEncounterLinks`, `DrawRows`, `DrawActionRow`; the pickers `DrawDutyPicker`, `DrawJobPicker`, `DrawPicker` (skills). Settings tab:
  `DrawSettingsTab` calls `DrawBehaviorGroup`, `DrawLayoutGroup`, and `DrawStyleGroup`.

Which setting belongs to which section (the section decides its `Is...Default` and `Reset...` in `Configuration.cs`):

| Section | Settings |
| --- | --- |
| Behavior | `AutoSwitch` (shown as "Auto-select encounter"), `AutoShowOverlay`, `AutoHideOverlay`, `HideDuringCutscenes` |
| Layout | `ShowFullEncounter`, `ShowControls`, `ShowHeaders`, `AutoSize`, `Anchor`, `MaxTextWidth`, `ColumnPadding`, and the six column alignments (`MechanicHAlign`, `MechanicVAlign`, `MitigationHAlign`, `MitigationVAlign`, `NotesHAlign`, `NotesVAlign`) |
| Style | `BackgroundAlpha`, `IconSize`, `TitleScale`, `MechanicScale`, `NotesScale`, `NoteRowScale` |
| None (no section Reset) | `OverlayVisible` (not restored at startup: the overlay starts hidden), `LockOverlay` (quick toggles), `OnlyCurrentJob` and `OnlyDefensive` (checkboxes inside the skill picker) |

Data flow in one paragraph: `Configuration` holds `Roots` (encounters) whose `Children` are phases whose `Rows` are
`SheetRow`s. The editor edits it and sets a `dirty` flag; at the end of `EditorWindow.Draw` it calls `plugin.Save()`.
The overlay reads it every frame. `Plugin.OnFrameworkUpdate` watches the zone and job and moves the navigation.

### Two states that look alike (don't merge them)

- **Selection** (`Configuration.SelectedId`): the node highlighted in the editor's tree.
- **Navigation** (`ActiveEncounterId`, `ActivePhaseId`): where the overlay is while you play.

While the editor is open the overlay shows the selection; with it closed it follows the navigation. Selecting in the
editor also moves the navigation (`SetActiveFromSelection`); the buttons and chat commands move both while editing.
See [docs/DECISIONS.md](docs/DECISIONS.md).

## Invariants and rules

1. **Never rename the namespace, the assembly or a saved type without a migration.** Settings are saved with type names
   (`"$type": "DutyNotes.SheetNode, DutyNotes"`) because Dalamud uses `TypeNameHandling.Objects`. The rename from
   MitigationSheet needed `Configuration.RenameLegacyTypes` plus a copy of the old file. The same applies to the
   manifest `InternalName`, which is the `AssemblyName` in `DutyNotes.csproj` and also names the settings file.
2. **Every list in a saved class needs `[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]`.**
   Dalamud loads with `Auto`, which fills the list that already exists, and that once doubled every nested list on each load.
   A regression test covers it.
3. **Untrusted text** (imports) is read with `TypeNameHandling.None` into the plain DTOs in `Sharing.cs`,
   never into the settings classes, and goes through `Sharing.Converter`. Sizes are capped, values clamped.
4. **Export text is versioned** (`DutyNotes:1:`). Old versions must keep importing. See
   [docs/FORMATS.md](docs/FORMATS.md).
5. **Two levels only**: encounter > phase. The level lives on the encounter; phases use it. A phase can be switched off (`SheetNode.Enabled`): everything
   that lists or steps through phases must skip the ones that are off (`Configuration.ActivePhase`, `CurrentPhase`, `PhaseTarget`, the overlay's full-encounter list).
6. **Defaults live in `Defaults`** (Configuration.cs). The field initializers and the Reset buttons both use them. A new
   setting touches many places in several files: follow [Adding a setting](#adding-a-setting-a-worked-example). Changing an
   existing default also has knock-on effects: see [Changing a default](#changing-a-default).
7. **Display text sizes go through `FontCache`**, never `SetWindowFontScale`: that stretches a baked bitmap and looks blurry.
8. **Keep pure files pure.** If logic is worth testing, move it into a pure file and add a check in `DutyNotes.Tests`.
   Several things (`OverlayChangeFor`, `PhaseTarget`, `RenameLegacyTypes`) exist as small functions for this reason.
9. **A new window must set `IsOpen = true`** to ever draw (Dalamud rule). `ControlsWindow` then decides in `DrawConditions`.

### Legacy names that must stay

- Window IDs `###mitsheet-overlay`, `###mitsheet-editor`, `###mitsheet-controls` and the table id `##mitsheet-table`:
  ImGui saves window positions under them. The visible titles say "Duty Notes".
- `MITSHEET_ROW` and `MITSHEET_SKILL` drag and drop payload names (internal only).

## Saving settings

- **A deliberate change** (a click, a command, a menu item): call `plugin.Save()`, which writes the settings file at once.
- **A change that arrives every frame** (dragging a slider, typing in a box): call `plugin.SaveSoon()`, which waits for a quiet half second (and never longer than five
  seconds). The editor does this by itself: it uses `SaveSoon` while an item is being dragged or typed into (`ImGui.IsAnyItemActive()`), and `Save` otherwise.
- A pending `SaveSoon` is written when the editor closes, when the overlay closes and when the plugin unloads (`Plugin.FlushPendingSave`, `Plugin.Dispose`). Any
  `Save()` also covers it. Don't save from a window's `Draw` every frame.

## Adding a chat command

1. Handle it in `Plugin.OnCommand` (a `case "word":`), and mention it in the command's `HelpMessage` there.
2. Add it to `HelpCommands.All`, with a macro name if it is worth binding to a hotbar key. The Help tab is drawn from that list.
3. Add its row to the commands table in `README.md`.
4. `HelpTests` fails if the list, the plugin and the README disagree (it reads `Plugin.cs` and `README.md`), so a missed step shows up.

## Adding a setting: a worked example

Take `NoteRowScale`, the text size of note rows (a `float`, a display setting in the **Style** section). A setting is never
just a field: the places below are all required, and missing one fails quietly (for example, a setting with no Reset entry is never restored by its section's Reset). Find them all again with `Grep` for `NoteRowScale` if in doubt.

| # | File | What to do (using `NoteRowScale` as the model) |
| --- | --- | --- |
| 1 | `Configuration.cs`, class `Defaults` | `public const float NoteRowScale = 1f;` The one place the starting value is written. |
| 2 | `Configuration.cs`, class `Configuration` | `public float NoteRowScale = Defaults.NoteRowScale;` This initializer is also the value an **older saved file** (without the key) gets. |
| 3 | `Configuration.cs`, `IsStyleDefault` | Add `&& Same(NoteRowScale, Defaults.NoteRowScale)`. Floats use `Same(...)`, bools and enums use `==`. Use the property of the section the setting belongs to: `IsBehaviorDefault`, `IsLayoutDefault` or `IsStyleDefault`. |
| 4 | `Configuration.cs`, `ResetStyle()` | Add it to the `before` tuple, assign `Defaults.NoteRowScale`, and add it to the tuple in the returned undo closure. (Same pattern in `ResetBehavior` and `ResetLayout`.) |
| 5 | `EditorWindow.cs`, `DrawStyleGroup` (or the matching `DrawLayoutGroup`, `DrawBehaviorGroup`) | A slider: `dirty \|= FloatControl("Note row text", ref cfg.NoteRowScale, Configuration.MinScale, Configuration.MaxScale, "%.2f", "tooltip");` A bool: `dirty \|= ImGui.Checkbox(...)` followed by `ImGui.SameLine(); HelpMarker("...")`. Always set `dirty`, or it won't be saved. |
| 6 | `OverlayWindow.cs` (or wherever it takes effect) | Read it from `plugin.Config`. `NoteRowScale` is read in `SpanningTextRow` and clamped to `Configuration.MinScale`/`MaxScale`, because a typed value is unclamped while it is being typed. |
| 7 | `DutyNotes.Tests` | `SettingsTests.cs`: in `ADefaultConfigurationIsAtItsDefaults` assert the default; in the `Changed()` helper give it a non-default value, and assert in the section's reset test (`StyleResetAndItsUndo`, `LayoutResetAndItsUndo` or `BehaviorResetAndItsUndo`) that the Reset restores it and its undo brings the old value back. `SerializationTests.cs`, `FilesFromBeforeASettingExistedStillLoad`: an older file without the key gets the default. |
| 8 | Docs | `CHANGELOG.md`, `docs/TESTING.md` (a row under "Built, but not yet confirmed in game", and the test count), and `docs/DECISIONS.md` if the setting embodies a decision. |

A setting saved inside a sheet (`SheetNode` or `SheetRow`) is a different job: it also needs `Sharing.cs` (the export format, its limits and
`Converter`) and a copy in `SheetNode.Clone` (rows: `SheetRow.Clone`); see [docs/FORMATS.md](docs/FORMATS.md) for the compatibility rules before you change
that.

## Changing a default

A default is written once (`Defaults`), but many things repeat or pin its value. When you change one, update all of these, and
**talk to the owner first**: several defaults were chosen deliberately with them (see [docs/DECISIONS.md](docs/DECISIONS.md), "Settings").

- **Tests that assert the old value** (they will fail, and that is the signal). By method:
  - `SettingsTests.ADefaultConfigurationIsAtItsDefaults`: column headers on, auto-size off, icon size 32; auto-select, auto open and auto hide on, and
    cutscene hiding off (`HideDuringCutscenes`); note row size equals notes size.
  - `SettingsTests` (the `Changed()` helper and the three `...ResetAndItsUndo` tests): what each Reset restores. Style: icon size 32 and note row size 1. Layout: headers on, auto-size
    off, column spacing 10, every column left and top. Behavior: auto-select and auto hide on, **cutscene hiding off**.
  - `SerializationTests.FilesFromBeforeASettingExistedStillLoad`: what an older file without the key gets, but only for `AutoShowOverlay`,
    `AutoHideOverlay` and `NoteRowScale`.
- **Docs that repeat the value:** the defaults paragraph in `docs/DECISIONS.md`
  ("Settings") and the entry that explains the setting, plus `CHANGELOG.md`.
- **Who is affected:** a fresh install, anyone pressing the section's **Reset**, and anyone whose saved settings file has no value for that
  key. Nobody's already saved value changes, so a changed default does not reach existing users on its own.

## Dalamud and ImGui quirks learned the hard way

**API shapes in this version** (`Dalamud.Bindings.ImGui`, Dalamud 15):

- There is no `ImGui.SeparatorText` (a manual heading is used) and no `ImGuiChildFlags`: `BeginChild(string, Vector2, bool border)`.
- Drag and drop: `SetDragDropPayload(string, byte[])`, `AcceptDragDropPayload(type, flags)` returns a payload with
  `IsNull` and `IsDelivery()`. The flags are `AcceptBeforeDelivery` and `AcceptNoDrawDefaultRect`.
- Colors: `ImGui.GetStyle().Colors[(int)ImGuiCol.X]` gives a `Vector4`.
- `IClientState.TerritoryType` is a `uint`. The player is `IObjectTable.LocalPlayer`. There is no zone-changed event
  used here: `IFramework.Update` is polled and compares with the last zone and job.
- Icons: `ITextureProvider.GetFromGameIcon(new GameIconLookup(id)).GetWrapOrEmpty().Handle`.
- File dialogs: `Dalamud.Interface.ImGuiFileDialog.FileDialogManager`; call `Draw()` every frame while one may be open.

**Behavior traps:**

- `SameLine(offset)` inside `BeginGroup` adds the group's own x to the offset, so controls in a group that isn't at the left
  edge land off screen. Use `SameLine()` then `SetCursorPosX(...)`.
- After `BeginCombo` returns true, `GetItemRectMin/Max` describe the popup, not the combo box. Measure before opening.
- A hand-drawn row (`InvisibleButton` plus draw list) does not close its popup the way `Selectable` does; call `CloseCurrentPopup()`.
- ImGui tables have no merged cells. A full-width row is drawn with the draw list into a zero-width `Dummy` of the right
  height (`SpanningTextRow`), so it can't stretch the first column.
- `TextWrapped` inside an auto-sizing window has no width to wrap at; wrap at an explicit maximum with `PushTextWrapPos`.
- Setting the cursor beyond the last item needs a `Dummy()` after it, or ImGui asserts.
- Dalamud's `WindowHost` (goatcorp/Dalamud, `Dalamud/Interface/Windowing/WindowHost.cs`) fades a window out after it is closed **with its X button or Escape** by drawing a stand-in window under the same ImGui name; closing it from code (`IsOpen = false`) does not. If a window behaves differently after X than after a code close, look there first. The overlay has `DisableFadeInFadeOut = true`.
- Dalamud closes a focused window on **Escape**: the overlay sets `RespectCloseHotkey = false`.
- Dalamud hides plugin windows in **cutscenes** unless `UiBuilder.DisableCutsceneUiHide` is set. The plugin sets it every
  frame from the setting. It applies to all of the plugin's windows.
- After Dalamud updates mid-session, keyboard input can go to the game instead of text boxes until the game is restarted.
  That's not a plugin bug.

**Tooling traps:**

- Windows PowerShell 5.1 cannot load net10 assemblies. Inspect them with a throwaway `dotnet run` console project instead.
- An XML comment (including in a `.csproj`) cannot contain `--`.
- Editing source from PowerShell: files may be LF or CRLF. Normalize with `-replace "`r`n","`n"` before matching, and
  match multi-line text exactly. A helper like this worked well:

  ```powershell
  function Edit-File([string]$path, [string]$old, [string]$new) {
    $p = Resolve-Path $path; $t = [IO.File]::ReadAllText($p) -replace "`r`n","`n"
    $o = $old -replace "`r`n","`n"; $n = $new -replace "`r`n","`n"
    if (-not $t.Contains($o)) { "NOT FOUND in ${path}"; return }
    [IO.File]::WriteAllText($p, $t.Replace($o, $n))
  }
  ```

## Inspecting the game's data (probes)

The skill, duty and job features rest on facts found by reading the game's Excel sheets. To look at a sheet, make a
throwaway console project (do not commit it) that references `Lumina.dll` and `Lumina.Excel.dll` from the Dalamud
hooks folder and opens `new GameData(@"<...>\game\sqpack")`, then `GetExcelSheet<T>()` with the generated types in
`Lumina.Excel.Sheets`. Print what you need and delete the project. What has already been learned is written down in
[docs/GAME-DATA.md](docs/GAME-DATA.md); check there first.

The same trick works for testing logic outside the game: a temp console project that compiles `Configuration.cs` and
`Sharing.cs` (see `DutyNotes.Tests`). It must use the assembly name `DutyNotes` so saved type names resolve.

## Working with the owner

- **Be open that this project is AI-written.** The README carries an AI disclosure (a banner and a section). Keep it true: if the way the project is made changes, update it, and never describe AI-written work as human-written, in the README, commit messages, changelog or any submission text. For a submission to Dalamud's official repository, the owner has to write their own disclosure.
- They **decide**; you advise. For a feature or a design change, answer "what do you think?" with a recommendation, the
  trade-offs and any decisions they need to make, then build once they agree. They often reply with a short
  "lets try your suggestion first" and then refine from screenshots. A message like "do not implement" means exactly that.
- They test in game and send screenshots. Take what they show as the truth over your own assumptions, and look for the
  cause in the code and the Dalamud log before guessing.
- Their audience is raiders rehearsing hard content, so defaults favor visibility (for example, the overlay stays visible
  in cutscenes by default so mitigation can be rehearsed).
- Wording and UI conventions that have settled: **Encounter** and **Phase**; **Mechanic row** and **Note row**;
  "Auto-select encounter / Auto open / Auto hide"; singular labels ("Above table"); help text lives behind a dim **(?)**
  marker; a setting that depends on another is greyed out but keeps its tick, with a short note why; every settings
  section has a **Reset** that can be undone; prefer undo over confirmation popups.
- They prefer small safe steps, and to know the cost of an option before choosing it. Say what you have **not** been able
  to verify.

## Where things stand

See [ROADMAP.md](ROADMAP.md) for the open work and [docs/TESTING.md](docs/TESTING.md) for what is confirmed in game. At the time
of writing (18 October 2026) the owner had run everything in `docs/TESTING.md` in game and it all passed, with nothing outstanding.
The repository (`notingway/duty-notes`) is private. The plugin builds with `Dalamud.NET.Sdk`, the tests are xunit, and the publishing
steps still to do are in the roadmap. Anything built after that date is unconfirmed until the owner says otherwise: add it to the
"Built, but not yet confirmed" table in `docs/TESTING.md`, and start the next session by asking the owner what they saw.