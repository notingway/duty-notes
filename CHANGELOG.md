# Changelog

All notable changes. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Nothing has been released yet, so everything is under Unreleased.

## [Unreleased] (0.1.0, in development)

The plugin started as "Mitigation Sheet" and was renamed **Duty Notes** once its scope grew. Its internal name is now
`DutyNotes`; settings from the old name are copied over once on first start.

### Added

- **Sheets.** Encounters hold phases (two levels only), and phases hold rows. Mechanic rows have a name, skills shown
  as icons, and notes. Note rows are free text across the full width of the table.
- **Skill picker.** Searchable, offers the skill versions that exist at the encounter's level, finds any version by
  name, filters to your job and to defensive and healing skills, and stays open to add several skills at once.
- **Duty and job per encounter.** A Party Finder style duty picker with content type icons, and a job picker. The
  encounter's level follows its duty and can be overridden.
- **Auto-select.** Entering a duty selects the matching encounter for your job. Options to open the overlay on a match
  and to hide it again on leaving (only when the plugin opened it), and a separate option to hide it in cutscenes.
- **Phase navigation.** Reset, Previous and Next as buttons under the overlay and as chat commands. A setting to
  show the whole encounter or only the current phase; the encounter's own rows stay pinned above the phase.
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

- **The overlay starts hidden** every time the game starts, with or without encounters (it used to come back as it was left). Show it with `/dutynotes`, the editor's "Show overlay" tickbox, or let auto-select open it when you enter a duty with a sheet for your job.
- **A note row's delete button says "Delete row"**, like a mechanic row's.

- **Saving.** While you drag a slider or type, the settings are no longer written on every frame. They are written after a short pause (half a second, and never later than five seconds), and at once when the editor closes, when the plugin unloads, or after a click.

- **Build.** The plugin now builds with `Dalamud.NET.Sdk` (the official way), which finds Dalamud itself and generates the manifest from `DutyNotes.csproj`. The hand-written `DutyNotes.json` is gone. A Release build also makes `latest.zip`. The output moved from `bin\x64\Release` to `bin\Release`. The assembly name, namespace and `InternalName` are unchanged, so saved settings still load. The unused `AllowUnsafeBlocks` setting was dropped.

### Removed

- **Backups.** A backup and restore feature (files you choose, plus automatic dated copies) was built and then removed as overkill. Export and import of encounters covers keeping and sharing them. Settings it left in older files are ignored.

### Fixed

- Closing the overlay with its X button and opening it again could put it back in the wrong place, partly off the screen, while the "Show overlay" tickbox in the editor did not. Two suspected causes were fixed together: the overlay no longer uses Dalamud's fade-out (the one code path only the X button takes), and the anchor logic leaves the first frames after opening alone, while ImGui re-fits an auto-sized window. Confirmed fixed in game.
- The last encounter could not be deleted, and "Encounter 1" came back on every load. There can now be no encounters: a fresh install starts with none, and the editor and the overlay show a short message.
- Deleting the last phase of an encounter selected the top encounter in the list instead of the encounter the phase was in. The selection now stays in that encounter. (Found by the owner using Ctrl + click on Delete; the logic is now `Configuration.RemoveNode`, with tests.)
- Nested lists in the settings were duplicated on every load (Dalamud fills lists that already exist).
- Pressing Escape could close the overlay and switch "Show overlay" off (fix not yet confirmed in game).
- Import text with a header from a newer version (for example `DutyNotes:2:`) said it "doesn't look like Duty Notes text". It now says it was made by a newer version and to update the plugin.
