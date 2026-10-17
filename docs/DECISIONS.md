# Design decisions

Why Duty Notes works the way it does. Most of these were settled in conversation with the owner, often after looking at
the options. Read the relevant entry before changing behavior, and add one when you decide something new.

## Name

**Duty Notes.** Started as "Mitigation Sheet" and outgrew it. "Encounter Log" was rejected (a log suggests recording what
happened) and so was "Duty Planner" ("planner" suggests a timeline, like Boss Mod's cooldown planner). "Duty" is the game's
own word for instanced content and matches the duty picker. "Mitigation" stays in the description and tags for searching.

The internal name was changed to `DutyNotes` while nothing was published, with a one-time copy of the old settings
(`Configuration.RenameLegacyTypes`). The command is `/dutynotes` (the old `/mitsheet` was dropped, since nothing was published); window ids keep their old
`###mitsheet-` names so saved window positions survive.

## Structure of a sheet

- **Two levels only: encounter > phase.** Unlimited nesting was dropped as not needed. Anything deeper in an old file is hoisted
  to phase level on load.
- **Phases are optional.** An encounter can hold rows itself. Forcing every encounter to have a phase would make single-phase
  fights need an empty wrapper.
- **Rows come in two kinds:** mechanic rows (name, skills, notes) and note rows (full-width free text). The first design was a
  "free notes" text above or below each table; note rows replaced it because they can sit anywhere, there can be several, and
  they reuse dragging, duplicating, deleting and undo.
- **The level is set once, on the encounter.** Phases use it. Choosing a duty sets the level to the level that duty is played
  at; changing it by hand overrides that and a **Use duty level** button goes back.
- **One duty and one job per encounter.** To cover another job, use **Duplicate**. Several jobs per encounter was considered and
  rejected as more complicated for little gain. If several encounters match, **the top one in the list wins**.

- **Phases can be switched off** (a tick per phase; `SheetNode.Enabled`, phases only). An off phase is skipped by the overlay, the full encounter and
  Previous / Next / Reset, but stays in the tree, dimmed, and still shows in the overlay while it is selected in the editor, so editing it never looks
  like it vanished. It is part of export and import. There is no switch for an encounter: leave it unused or delete it.

- **Rows can be switched off** (a tick per row; `SheetRow.Enabled`, mechanic and note rows). An off row is always left out of the overlay, even
  while editing: unlike a phase, which you must be able to see to edit, a row is edited in the editor's own list, where it stays, dimmed. A sheet whose
  rows are all off counts as empty. It is part of export and import. A row is copied in one place, `SheetRow.Clone`.

## Help

- **A Help tab, drawn from one list of commands** (`HelpCommands`). It lists every chat command and gives a copy-ready macro under the ones worth binding to a hotbar key (next, prev and reset only; not show/hide, edit, lock or full). A macro line is just the command, so the macro text is the command itself; it is not shown, only a copy icon (the hover text shows it). Commands with no macro have no copy icon. The macro's icon cannot be set by the macro text (the game's /micon only takes names of actions, items and so on), so the tab draws the suggested icon (right arrow, left arrow, loop; ids in GAME-DATA.md) and the steps tell you to pick it in the macro's Icons window. A test keeps the list, the plugin's command handling and the README in step.

- **There can be no encounters.** The last encounter can be deleted, and a fresh install starts with none. The editor then says "No encounters yet" with a hint
  to use + Encounter or Import, and the overlay says to use `/dutynotes edit`. An early rule (always keep one, and re-create "Encounter 1" on load) was dropped: it
  only got in the way of importing a shared encounter without a leftover empty one. Undo brings a deleted encounter back.

## Selection versus navigation

The editor's tree selection and the overlay's position are separate things (`SelectedId` versus `ActiveEncounterId` and
`ActivePhaseId`). While the editor is open, the overlay shows the selection so you see what you are editing. With it closed,
the overlay follows the navigation (auto-select, buttons, commands). They are kept in step: selecting in the editor moves the
navigation, and the buttons and commands move the selection while editing. Merging them made the overlay jump around when
you clicked in the tree.

- **The editor's buttons split by what they do.** The left pane has the ones that add something (+ Encounter, + Phase, Import). The right pane has one line under the
  sheet's name for the ones that act on the selected sheet (Move up, Move down, Duplicate, Export, Delete), and the result of the last import or export is shown
  there too. Export copies a whole encounter, so only an encounter has the button; a phase does not show one (exporting a single phase is on the roadmap).

- **Ctrl + click on Delete skips the confirmation.** Deleting always asks first, but Undo brings a sheet back, so the owner can hold Ctrl to skip the question. (Same habit as Ctrl + right-click on a locked overlay.)

## What the overlay shows

- **Show full encounter** (setting): the encounter's own rows, then every phase, one shared table. Off: the encounter's own
  rows pinned above the phase you are on, so raid-wide text stays on screen. An encounter with nothing at its own level looks
  as if there were no pinning.
- **One table for all sections**, with a title row each, so columns are as wide as the widest content of any phase and line up.
  Separate tables per phase were tried first and had mismatched widths.
- **The Reset / Previous / Next buttons are hidden in full-encounter mode** (every phase is on screen, so there's nothing to step
  through). The settings checkbox says why, and the right-click item is greyed out with a tooltip. Showing them disabled
  was considered and rejected: it adds clutter on the overlay where you can't read a tooltip mid-fight.
- **The buttons are a separate window** under the overlay, so they stay clickable when the overlay is locked (click-through).
- **Right-click menu.** Ctrl + right-click opens it on a locked overlay (the overlay takes the mouse only while Ctrl is held).

## Skills

- **Which skills are offered.** Only real, usable ones: rows with an unlock level and a class category, not PvP. See
  [GAME-DATA.md](GAME-DATA.md); the Action sheet is full of unusable duplicates.
- **Level-aware, upgrade-aware.** A skill and its upgrades form a family (from the trait text "Upgrades X to Y"). The picker shows the
  version you have at the encounter's level, and searching **any** version finds it (search Bloodwhetting at level 80, get Raw
  Intuition, shown with the matching version's icon).
- **Defensive and healing filter is a hand-written list** (`DefensiveSkills.cs`). Classifying by description text was tried and
  was wrong both ways (it caught Raging Strikes and missed Holmgang, Living Dead, Hallowed Ground). Add names to the list when
  something is missing. Healing was added because the owner wanted heals visible, not only mitigation.
- **One "Limit Break" skill, not one per job and tier.** Limit breaks are not real player actions in the game data (so no filter finds
  them) and there are about a dozen mitigation ones. A plan only needs to say "limit break here", so there is a single entry with an id of
  its own (`Configuration.LimitBreakId`), which is saved in sheets and export text and so must never change.
- **Job filter** follows the encounter's job if set (so you can plan for a job you aren't playing), otherwise your current job.
- **The picker stays open** after a pick, so several skills can be added; picked ones are highlighted and clicking again removes.

## Auto-select

- **When:** only on a change of zone or job, never every frame, so picking a sheet by hand sticks until the next change.
- **Auto open** brings the overlay up on a match. **Auto hide** hides it on leaving, **only if auto-select opened it**. An overlay
  you opened yourself is never taken away. Any manual show or hide makes the visibility yours again. Defaults: both on.
- **It never needs the overlay to already be visible.** The first version only switched the sheet and, if the overlay had been
  closed (Escape used to close it), nothing appeared.
- **Escape does not close the overlay** (`RespectCloseHotkey = false`): Escape is for the game's menus.
- **Hide during cutscenes is off by default**, because the owner and many raiders rehearse mitigation during cutscenes. Dalamud
  hides plugin windows in cutscenes by default, so "off" means the plugin turns that off for its windows. It applies to all windows.

## Settings

- **Defaults in one place** (`Defaults`), used by a fresh install and by Reset. Chosen with the owner: column headers on, auto-size
  off (what most users are used to), icon size 32, background 0.6, cutscene hiding off.
- **A Reset button per section** (Behavior, Layout, Style), greyed out while the section is already at its defaults. Reset
  goes on the undo stack instead of asking for confirmation.
- **Dependent options are greyed out, not hidden,** and keep their tick, so the panel doesn't jump when something is toggled.
  Where a setting has no effect at the moment (phase buttons in full mode), a dim note says why.
- **Column alignment is per column, across and up and down, picked on a 3 x 3 grid of dots** (the same control as the anchor picker: one click sets both). The defaults are left and top, which is how the overlay always looked. The headers follow their column's across setting; up and down only matters when a row is taller than a cell's content. Cells are drawn on the draw list after the row's height is measured (the tallest cell decides it), because ImGui tables cannot align cells themselves.
- **Help text** sits behind a dim **(?)** marker, not on every control.
- **Text and icon sizes** each have a slider plus a number box that can be typed into. Sizes are rendered at real size with real
  fonts (`FontCache`), because scaling one baked font was blurry.

## Sharing

- **Export text** is `DutyNotes:1:` + base64 of gzipped JSON of a small purpose-made format, not the settings classes, so the
  settings can change without breaking text others already have. A realistic encounter is about 300 characters.
- **Import is a paste window, not a silent clipboard read.** It shows the text and its length, reports problems beside it, and avoids depending on the game window owning the clipboard. A **Paste from clipboard** button is kept as a shortcut.
- **Importing treats the text as untrusted:** a tiny gzip that expands to gigabytes is refused, the JSON is read without type
  names, sizes and counts are capped, values are clamped, control characters stripped, unknown skills, duties and jobs
  dropped (and counted), and the zone for a duty always comes from this game's data.
- **No backup feature.** There was one (backup and restore files, plus automatic dated copies) and it was removed as overkill:
  export and import of single encounters covers sharing and keeping a copy, and Dalamud keeps the settings file itself. If it is ever
  wanted again, export the whole list instead of rebuilding a second format. Settings left in older files by it are ignored on load.

## The fix worth remembering

Dalamud saves with type names and loads by filling lists that already exist. For any list inside a saved class that duplicated
its contents on every load (the symptom was each phase appearing twice, then four times). The fix is the attribute
`[JsonProperty(ObjectCreationHandling = Replace)]` on every list, plus a dedupe by id when loading to heal damaged files. A
regression test reproduces the loading exactly as Dalamud does it.
