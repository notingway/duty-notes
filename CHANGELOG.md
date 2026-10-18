# Changelog

All notable changes. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Nothing has been released yet, so everything is under Unreleased.

## [Unreleased] (0.1.0, in development)

The plugin started as "Mitigation Sheet" and was renamed **Duty Notes** once its scope grew. Its internal name is now
`DutyNotes`; settings from the old name are copied over once on first start.

### Added

- **New row controls.** Right-clicking the background of a row in the editor opens a menu: add a mechanic or a note row above or below it (the cursor goes into the new row's text box, and Undo removes it), duplicate it, or delete it. A row is dragged by its background (there is no grip icon any more); the dragged row gets a yellow outline and no tooltip. **Rows live in phases only.** An encounter no longer has a table of its own: selecting one shows its settings, and + Encounter creates it with a first phase (Phase 1) ready to fill in. Import turns rows that text carries on the encounter into a first phase named after it. The sample in samples/ was regenerated without encounter-level rows. With an encounter selected in the editor, the overlay shows its first phase (or all of them with the full encounter on) instead of an empty table. **Use duty level** is always shown next to Use my level, greyed out until a duty is chosen. The sheet pane puts the name's label to the left of its box (Encounter or Phase) and the icons below the settings. The buttons under a sheet's name are icons now, the same Duplicate and Delete icons as a row has, plus Export, with tooltips. Encounters, and the phases inside an encounter, are reordered by dragging them in the list (same outline and line; a dragged encounter is outlined together with its phases, and hovering one of an encounter's phases counts as hovering the encounter), which replaces the Move up and Move down buttons. A dragged skill is outlined the same way, and a yellow line shows where it will land (the old box on the target icon is gone). Duplicate and delete are also icons beside the row's tick, after a small divider, with tooltips (Delete is red when hovered), and Duplicate can be undone. Every row has a lighter band behind it, with a small even gap between rows and the row's content inset inside it, so the rows are easy to tell apart (there are no lines between rows, only one above and one below the list). **Add mechanic row** and **Add note row** stay at the bottom of the list.
- **An icon** (`images/icon.png`, 512 x 512): a clipboard with a shield, generated with ChatGPT and credited in the README. And a screenshot of the overlay (`images/image1.png`), shown in the README and in the plugin installer.
- **Sheets.** Encounters hold phases (two levels only), and phases hold rows. Mechanic rows have a name, skills shown
  as icons, and notes. Note rows are free text across the full width of the table.
- **Skill picker.** Searchable, offers the skill versions that exist at the encounter's level, finds any version by
  name, filters to your job and to defensive and healing skills, and stays open to add several skills at once.
- **Duty and job per encounter.** A Party Finder style duty picker with content type icons, and a job picker. The
  encounter's level follows its duty and can be overridden.
- **Auto-select.** Entering a duty selects the matching encounter for your job. Options to open the overlay on a match
  and to hide it again on leaving (only when the plugin opened it), and a separate option to hide it in cutscenes.
- **Phase navigation.** Reset, Previous and Next as buttons under the overlay and as chat commands. A setting to
  show the whole encounter or only the current phase.
- **Editing.** Drag rows and skills to reorder, duplicate rows and encounters, undo for deletions, imports and resets
  (Ctrl+Z too).
- **Sharing.** Export an encounter to the clipboard as text, and import it back through a paste window (so you can see the text and any problem before it is used). Pasted text is validated. An example is in `samples/`.
- **Look and layout.** Separate text sizes for titles, mechanics, notes and note rows (rendered at real size, not
  stretched), icon size, column spacing, background opacity, column headers, auto-size with an anchor point, and a
  right-click menu on the overlay (Ctrl + right-click when locked).
- **Settings.** Defaults live in one place, with a Reset button per section.
- **Commands.** `/dutynotes` with `edit`, `lock`, `next`, `prev`, `reset` and `full`.
- **Tests.** An xunit test project for the logic that doesn't need the game, run with `dotnet test` (see [docs/TESTING.md](docs/TESTING.md)).
- **Switch phases and rows on and off.** Each phase in the Sheets tree and each row in the editor has a tick. An off phase is dimmed in the tree and skipped by the overlay, the full encounter view and Previous, Next and Reset (and the matching commands); while the editor is open, a phase you have selected still shows so you can edit it. An off row is dimmed in the editor and always left out of the overlay, including its note in the Notes column; a sheet whose rows are all off shows nothing. Both travel with export and import.
- **Column alignment.** Each column (Mechanic, Mitigation, Notes) can be aligned left, center or right, and top, center or bottom, under Settings > Layout, by picking a dot on a 3 x 3 grid per column (the same control as Anchor). Left and top are the defaults, which is how the overlay always looked. Column headers follow the column's left, center or right setting. Up and down only shows when a row is taller than a cell's content.
- **Limit Break in the skill picker.** One skill called "Limit Break" can be added to any row, for any job (it counts as defensive). It is one entry, not one per job and tier, because a plan only needs to say "use the limit break here".
- **Help tab.** A new tab in the editor lists every chat command. Next, prev and reset have a suggested macro: the suggested macro icon drawn next to it (right arrow, left arrow and the loop from the game's macro Icons window), a name, and a copy icon (the macro text is the command, shown when you hover the icon). The tab also explains how to make a macro.
- **A sample.** `samples/example-encounter.txt` is an import text for a made-up "Example Encounter" with three phases. It shows a note row, a row and a phase switched off, and Limit Break. It names no real duty or job.
### Changed

- **Release tooling.** `scripts\release.ps1` builds, tests, checks the package and publishes a release to the custom plugin repository `plogonway`, and `docs/RELEASING.md` describes the process. (The script has been dry-run only: nothing has been released.)

- **The overlay opens on the screen.** If its remembered position is off the screen (it could be left partly above the top after the earlier close-with-X bug, or after a change of resolution), it is moved back so its title bar can be reached. On a fresh install it starts near the top left.

- **The overlay starts hidden** every time the game starts, with or without encounters (it used to come back as it was left). Reloading the plugin during a game session keeps it as it was. Show it with `/dutynotes`, the editor's "Show overlay" tickbox, or let auto-select open it when you enter a duty with a sheet for your job.

- **Saving.** While you drag a slider or type, the settings are no longer written on every frame. They are written after a short pause (half a second, and never later than five seconds), and at once when the editor closes, when the plugin unloads, or after a click.

- **Build.** The plugin now builds with `Dalamud.NET.Sdk` (the official way), which finds Dalamud itself and generates the manifest from `DutyNotes.csproj`. The hand-written `DutyNotes.json` is gone. A Release build also makes `latest.zip`. The output moved from `bin\x64\Release` to `bin\Release`. The assembly name, namespace and `InternalName` are unchanged, so saved settings still load. The unused `AllowUnsafeBlocks` setting was dropped.

### Removed

- **Backups.** A backup and restore feature (files you choose, plus automatic dated copies) was built and then removed as overkill. Export and import of encounters covers keeping and sharing them. Settings it left in older files are ignored.

### Fixed

- Stepping between phases with Next and Previous made the overlay flicker: it showed the new phase in the old-sized frame, resized a frame later, and then moved. An auto-sizing window is now hidden, background included, for the two frames it takes to catch up, and appears complete (the phase buttons under it wait too). Confirmed fixed in game.
- Closing the overlay with its X button and opening it again could put it back in the wrong place, partly off the screen, while the "Show overlay" tickbox in the editor did not. Two suspected causes were fixed together: the overlay no longer uses Dalamud's fade-out (the one code path only the X button takes), and the anchor logic leaves the first frames after opening alone, while ImGui re-fits an auto-sized window. Confirmed fixed in game.
- The last encounter could not be deleted, and "Encounter 1" came back on every load. There can now be no encounters: a fresh install starts with none, and the editor and the overlay show a short message.
- Deleting the last phase of an encounter selected the top encounter in the list instead of the encounter the phase was in. The selection now stays in that encounter. (Found by the owner using Ctrl + click on Delete; the logic is now `Configuration.RemoveNode`, with tests.)
- Nested lists in the settings were duplicated on every load (Dalamud fills lists that already exist).
- Pressing Escape could close the overlay and switch "Show overlay" off (fix not yet confirmed in game).
- Import text with a header from a newer version (for example `DutyNotes:2:`) said it "doesn't look like Duty Notes text". It now says it was made by a newer version and to update the plugin.
