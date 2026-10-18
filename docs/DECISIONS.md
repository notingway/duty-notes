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
- **Rows come in two kinds:** mechanic rows (name, actions, notes) and note rows (full-width free text). The first design was a
  "free notes" text above or below each table; note rows replaced it because they can sit anywhere, there can be several, and
  they reuse dragging, duplicating, deleting and undo.
- **The level is set once, on the encounter.** Phases use it. Choosing a duty sets the level to the level that duty is played
  at; changing it by hand overrides that and a **Duty** button next to the level goes back to it. It sits left of **Job** (your character's level on the encounter's job; the tooltips say they sync the level). A chosen job has a clear X like a chosen duty. Both are always shown, greyed out until there is a duty or a job to sync with, so a level can be put back at any time.
- **One duty and one job per encounter.** To cover another job, use **Duplicate**. Several jobs per encounter was considered and
  rejected as more complicated for little gain. If several encounters match, **the top one in the list wins**.

- **Phases can be switched off** (a tick per phase; `SheetNode.Enabled`, phases only). An off phase is skipped by the overlay, the full encounter and
  Previous / Next / Reset, but stays in the tree, dimmed, and still shows in the overlay while it is selected in the editor, so editing it never looks
  like it vanished. It is part of export and import. There is no switch for an encounter: leave it unused or delete it.

- **Rows can be switched off** (a tick per row; `SheetRow.Enabled`, mechanic and note rows). An off row is always left out of the overlay, even
  while editing: unlike a phase, which you must be able to see to edit, a row is edited in the editor's own list, where it stays, dimmed. A sheet whose
  rows are all off counts as empty. The tick is the only thing that decides whether a row shows: a row that is on is drawn even when nothing is written in it
  (an empty mechanic row keeps a line's height, and so does an empty note row), so a blank row can be used as a spacer. The Notes column is the exception: it only
  exists while at least one visible row has a note. It is part of export and import. A row is copied in one place, `SheetRow.Clone`.

## Help

- **A Help tab, drawn from one list of commands** (`HelpCommands`). It lists every chat command and gives a copy-ready macro under the ones worth binding to a hotbar key (next, prev and reset only; not show/hide, edit, lock or full). A macro line is just the command, so the macro text is the command itself; it is not shown, only a copy icon (the hover text shows it). Commands with no macro have no copy icon. The macro's icon cannot be set by the macro text (the game's /micon only takes names of actions, items and so on), so the tab draws the suggested icon (right arrow, left arrow, loop; ids in GAME-DATA.md) and the steps tell you to pick it in the macro's Icons window. A test keeps the list, the plugin's command handling and the README in step.

- **There can be no encounters.** The last encounter can be deleted, and a fresh install starts with none. The editor then says "No encounters yet" with a hint
  to use + Encounter or Import, and the overlay says to use `/dutynotes edit` (when you have chosen to show it). An early rule (always keep one, and re-create "Encounter 1" on load) was dropped: it
  only got in the way of importing a shared encounter without a leftover empty one. Undo brings a deleted encounter back.

## Build and packaging

- **Dalamud.NET.Sdk, with the manifest generated from the csproj.** It is the official, supported way (the Dalamud sample plugin uses it), and what the official plugin
  repository's build expects. The names stay `DutyNotes` (assembly, namespace, InternalName) because saved settings record them. The test project deliberately does
  not use the SDK: it compiles a few source files directly and references Dalamud by path, which is simpler than letting a plugin SDK shape a test project.

## Saving

- **Settings are saved after a short pause while you drag or type, and at once after a click.** Writing the whole settings file every frame during a slider drag was wasteful. The rule is in
  `SaveScheduler` (pure and tested): a quiet half second, or five seconds at most. A crash within that window can lose the last edit, which is accepted. The editor tells the two cases apart with
  `ImGui.IsAnyItemActive()`, and the pending save is also written when the editor or overlay closes and when the plugin unloads.

## Editing rows

- **Row actions: icons for the two common ones, a right-click menu for the rest.** Duplicate and delete are icons on the row's last line, after its **Show row** tick (the row's identity controls; the fields come first, each with a label on the left, in the order of the overlay's columns: Mechanic, Actions (the actions, on a backdrop in the boxes' colour, so they read as a field), Notes; or Note for a note row), after a small divider (`|`), with Delete red on hover so it is not hit by accident (Undo covers it anyway). Everything else a row can do is in a menu on a right-click of the row's background: add a mechanic or a note row above or below it, duplicate, delete. Rows can therefore be added anywhere without dragging, and the rows stay free of buttons. **Add mechanic row** and **Add note row** stay at the bottom of the list (adding at the end is the common case), with a hint that the menu exists. Every row has the same lighter band behind it (a faint white), with its content inset from the band's edges and a small even gap between bands, drawn on a layer behind the row's content (`ChannelsSplit`), so the rows are easy to tell apart, which is why there are no lines between rows, only one above the list and one below it. Alternating bands were tried first and judged too faint to read. A row is dragged by its background, the same area that opens its menu (there is no grip icon): a button covering the row's band is submitted after the row's content and is the drag source and the right-click target (ImGui gives a hovered item to the first widget under the pointer, so the boxes and buttons on top keep their clicks and the button only gets the bare background). The row being dragged gets a yellow outline, the colour of the insertion line, and no tooltip. Making the whole row group the drag source instead would let dragging inside a text box start a row drag, because after `EndGroup` the group takes the ID of the active widget inside it. This replaced two earlier versions: one "Add row below" button with Ctrl + click for a note row, and **+ Mechanic** / **+ Note** buttons on every row.

## Selection versus navigation

The editor's tree selection and the overlay's position are separate things (`SelectedId` versus `ActiveEncounterId` and
`ActivePhaseId`). While the editor is open, the overlay shows the selection so you see what you are editing. With it closed,
the overlay follows the navigation (auto-select, buttons, commands). They are kept in step: selecting in the editor moves the
navigation, and the buttons and commands move the selection while editing. Merging them made the overlay jump around when
you clicked in the tree.

- **The editor's buttons split by what they do.** The left pane has the ones that add something (+ Encounter, + Phase, Import). The right pane has one line under the
  sheet's name for the ones that act on the selected sheet (Duplicate, Export, Delete: icons, the same Duplicate and Delete icons as a row has, with tooltips; there are no Move up / Move down buttons: sheets are reordered by dragging them in the tree, an encounter among encounters and a phase inside its own encounter, with a yellow outline and an insertion line like rows and actions; a phase can't change encounter because the level lives on the encounter), and the result of the last import or export is shown
  there too. Export copies a whole encounter, so only an encounter has the button; a phase does not show one (exporting or importing a single phase was considered and dropped; see the roadmap).

- **Ctrl + click on Delete skips the confirmation.** Deleting always asks first, but Undo brings a sheet back, so the owner can hold Ctrl to skip the question. (Same habit as Ctrl + right-click on a locked overlay.)

## The overlay window

- **No fade in or out.** Dalamud fades windows in and out, and draws a stand-in copy of a window to fade it out when it is closed with the X button. The overlay switches that off (`DisableFadeInFadeOut`): a HUD gains nothing from a fade, and the stand-in was the suspected cause of the overlay coming back at the wrong place after closing it with X (fixed in game together with the settle frames below, which were not tried apart). The editor and the phase buttons still fade.
- **The first frames after opening are left alone by the anchor logic.** ImGui re-fits an auto-sized window over its first couple of frames after it was hidden; `KeepAnchorFixed` used to read that as the content growing and moved the window to compensate.

- **The overlay starts hidden every time the game starts.** Whether it was showing when the game last closed is not remembered (`OverlayVisible` is reset at startup). Only the first load of a game session does this: it leaves a marker in the process environment (`DUTYNOTES_GAME_SESSION_STARTED`), and a plugin reload in the same session (a dev reload, or a Dalamud update) finds it and brings the overlay back as it was (`Plugin.Dispose` saves what it was doing). Without that, every reload hid it. It comes up through `/dutynotes`, the "Show overlay" tickbox, or auto-select (auto open) when you enter a duty with a sheet for your job. A fresh install, or an empty list, therefore never greets you with a window. If a "show at start" setting is ever wanted, it belongs in Settings > Behavior.

- **The overlay is kept on the screen.** Its position and size are remembered between sessions by Dalamud itself (ImGui keeps every window's layout in `dalamudUI.ini`), so the plugin does not store them. The first time the overlay opens after each start, `ScreenPlacement.KeepOnScreen` checks that its title bar is on the screen and 80 pixels of it show, and moves it just far enough if not (a position saved by an older version, or a change of resolution or monitor, would otherwise leave it out of reach). On a fresh install it starts at the top left (80, 80).

- **An auto-sizing overlay is hidden while it catches up with new content.** ImGui sizes an auto-fit window from the previous frame's content, and a table's column widths lag the same way, so a new phase appeared in the old-sized frame and then resized (and `KeepAnchorFixed` moved it after that): stepping between phases looked like a flicker. Measured in game, the window size is stale for exactly **one** frame and final from the next, so when what the overlay shows changes (the encounter or phase, or the editor's selection) it is hidden for **two** frames: the stale one and the one in which the anchor adjustment moves it (hide, swap, scale, anchor, show). Opening the overlay is slower (ImGui hides a new window for a frame and re-fits it over two), so it waits at least three frames and for its size to repeat. `LayoutSettler` is the rule (pure and tested); its frame counter runs in `PreDraw`, so the wait is bounded (12 frames) even if the window is never drawn. The phase buttons under the overlay wait with it. Only auto-size needs this: with auto-size off the window does not resize. Hiding needs **two** things, because neither alone hides everything: a style alpha of 0.001 (not 0, which makes ImGui skip the window and never call `Draw`), and `BgAlpha = 0` (ImGui overwrites the background's alpha with `SetNextWindowBgAlpha` and ignores the style alpha, so the dark panel stayed on screen and was the flicker; with no background Dalamud's blurred backdrop is not drawn either). A true fix would be to compute the layout and the window size ourselves before drawing, which was judged too risky for the gain.

## What the overlay shows

- **Rows live in phases only.** An encounter has its settings (name, level, duty, job) and its phases; it has no table of its own. The editor
  shows no rows for a selected encounter, and **+ Encounter** creates the encounter with a first phase (\
- **One table for all sections**, with a title row each, so columns are as wide as the widest content of any phase and line up.
  Separate tables per phase were tried first and had mismatched widths.
- **The Reset / Previous / Next buttons are hidden in full-encounter mode** (every phase is on screen, so there's nothing to step
  through). The settings checkbox says why, and the right-click item is greyed out with a tooltip. Showing them disabled
  was considered and rejected: it adds clutter on the overlay where you can't read a tooltip mid-fight.
- **The buttons are a separate window** under the overlay, so they stay clickable when the overlay is locked (click-through).
- **Right-click menu.** Ctrl + right-click opens it on a locked overlay (the overlay takes the mouse only while Ctrl is held).

## Actions

- **Which actions are offered.** Only real, usable ones: rows with an unlock level and a class category, not PvP. See
  [GAME-DATA.md](GAME-DATA.md); the Action sheet is full of unusable duplicates.
- **Level-aware, upgrade-aware.** An action and its upgrades form a family (from the trait text "Upgrades X to Y"). The picker shows the
  version you have at the encounter's level, and searching **any** version finds it (search Bloodwhetting at level 80, get Raw
  Intuition, shown with the matching version's icon).
- **Defensive and healing filter is a hand-written list** (`DefensiveSkills.cs`). Classifying by description text was tried and
  was wrong both ways (it caught Raging Strikes and missed Holmgang, Living Dead, Hallowed Ground). Add names to the list when
  something is missing. Healing was added because the owner wanted heals visible, not only mitigation.
- **One "Limit Break" action, not one per job and tier.** Limit breaks are not real player actions in the game data (so no filter finds
  them) and there are about a dozen mitigation ones. A plan only needs to say "limit break here", so there is a single entry with an id of
  its own (`Configuration.LimitBreakId`), which is saved in sheets and export text and so must never change.
- **Job filter** follows the encounter's job if set (so you can plan for a job you aren't playing), otherwise your current job.
- **The picker stays open** after a pick, so several actions can be added; picked ones are highlighted and clicking again removes.

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
  names, sizes and counts are capped, values are clamped, control characters stripped, unknown actions, duties and jobs
  dropped (and counted), and the zone for a duty always comes from this game's data.
- **No backup feature.** There was one (backup and restore files, plus automatic dated copies) and it was removed as overkill:
  export and import of single encounters covers sharing and keeping a copy, and Dalamud keeps the settings file itself. If it is ever
  wanted again, export the whole list instead of rebuilding a second format. Settings left in older files by it are ignored on load.

## The fix worth remembering

Dalamud saves with type names and loads by filling lists that already exist. For any list inside a saved class that duplicated
its contents on every load (the symptom was each phase appearing twice, then four times). The fix is the attribute
`[JsonProperty(ObjectCreationHandling = Replace)]` on every list, plus a dedupe by id when loading to heal damaged files. A
regression test reproduces the loading exactly as Dalamud does it.
