# Roadmap

What is left, in rough priority order, and what was deliberately not done. See
[docs/DECISIONS.md](docs/DECISIONS.md) for why things are the way they are.

## First: confirm what is built but unseen

Nothing is outstanding: as of 24 October 2026 everything built has been confirmed in game. Something new goes in
[docs/TESTING.md](docs/TESTING.md#built-but-not-yet-confirmed-in-game) until the owner has seen it, and anything that behaves oddly
comes before the items below.

## Open

1. **Continuous integration** (a GitHub Actions workflow): **not wanted for now.** The owner's call (2026-10-18): all changes are made through one AI session that builds and runs the tests locally before every push (the release script also builds and tests before it releases), so CI would only repeat that. It becomes worth it if other people start sending changes, or for a scheduled build against the newest Dalamud to warn of an API bump. Plan if it is wanted: set up .NET 10, download Dalamud's published distribution (the assemblies are not on NuGet; check what it contains first, and point `DalamudLibPath` at it, which both projects already support), build the plugin, run `dotnet test` and check the package. Free for public repositories.
2. **Publishing.** Experimental releases are out (see `docs/RELEASING.md`):
   - done: both repositories (`notingway/duty-notes` and the custom repository `notingway/plogonway`) have been public since 2026-10-18, and releases v0.1.0 to v0.1.6 were made with `scripts\release.ps1`, all as pre-releases;
   - a test install from the repository was confirmed with v0.1.6 on 2026-10-24;
   - still to do: deciding when to make a full, non-pre-release.
3. **An accent for note rows** (a dimmer text colour, or a bar at the left) if they are hard to tell from mechanic rows.
4. **Remember that auto-select opened the overlay across a plugin reload.** Today `overlayOpenedByUs` is in memory, so
   reloading the plugin while inside a duty means leaving that duty will not hide the overlay.
5. **Other client languages (German, French, Japanese).** Deferred by the owner (2026-10-24) until someone asks. Today three things read English game text and fail on other clients: the picker's "Only defensive" filter, upgrade chains, and the tooltip's recast and charges from traits (see the README). Plan: the global client ships English text for every sheet, so read `Trait`/`TraitTransient` and the `Action` names used for the `DefensiveSkills` matching in English (`IDataManager.GetExcelSheet<T>(ClientLanguage.English)`) and link them by row id, in `ActionCatalog` (`UpgradeRegex`, `DefensiveSkills`) and `ActionTooltip` (`SyncedText.RecastsSetBy`/`ChargesSetBy`); what is displayed stays in the client's language. Chinese and Korean clients don't ship English, but they use their own Dalamud builds.

## Ideas considered and left

- **Reset to the first phase on a wipe.** Nothing watches combat, wipes or death today: Reset is the button or `/dutynotes reset` only (it goes to the first phase that is on). Automatic reset would use Dalamud's `ICondition` (combat state, death) but a wipe has no single signal, so it could misfire. If it is ever built: a setting, off by default, and a decision on the trigger first. Left as it is for now (the owner, 2026-10-18).

## Small things noticed and left

- **The Help tab's macro name.** Next, prev and reset show a suggested name ("DN Next"). Copying it would need its own copy icon: putting the name in the copied macro text is not safe, because a line that is not a command is sent as chat. Proposed, not decided.
- **Macro icons by text.** The macro Icons window can only be used by hand. The game's `/micon` takes names, and the `general` category has "Next Board" and "Previous Board" (» and «) and "Target Forward" and "Target Back" (gold arrows), which could set an icon without a manual step. Not tried in game, and reset has no good fit (see `docs/GAME-DATA.md`).
- Actions beyond 24 in a row of an imported text are dropped silently (not counted in the "dropped" message; this is documented in `docs/FORMATS.md`).

## Declined or deferred (don't redo without a reason)

- **Caching the overlay's per-frame layout** (found by the publishing review: lists, LINQ and text wrapping are rebuilt every frame). Deferred by the owner. The cost is small for a normal sheet and nothing has been measured, while a stale cache would be a worse bug than the allocations it saves. Do it only if someone sees stutter, a reviewer asks, or a measurement (a stopwatch around `OverlayWindow.Draw`) shows it matters, and then fix only what the measurement points at.
- **A guard for a failed settings load** (the review worried that an unreadable settings file would start empty and be overwritten by the next save). Deferred by the owner. Dalamud already writes plugin settings through `ReliableFileStorage` (atomic writes, plus a copy in an internal database it recovers from if the file is corrupt), which covers the case. What is not confirmed is what the plugin sees if both copies fail; at most, log it and show a message.

- **A job-aware overlay** (dim or hide the actions your job doesn't have). Abandoned by the owner. It would have been small: the job matching exists for the action picker (`ActionCatalog.UsableBy`), with a three-way setting (show all, dim, hide).
- **Live cooldowns on the icons** (greyed with a timer). Abandoned by the owner. It was the biggest and riskiest idea: it needs the game's action manager and internal structures that change between patches.
- **Importing a single phase** into an existing encounter. Abandoned by the owner. Import and export take whole encounters only, so a phase has no Export button.
- **Sorting actions by duration, cooldown or power.** Alphabetical and cooldown would be reliable, but duration and power
  are not in the game data in a usable form (descriptions leave the duration blank). Could return with a hand-curated table.
- **An editable column title** to replace "Mechanic". The word is the community's own; kept.
- **Showing the phase buttons disabled in full-encounter mode.** They are hidden instead, and the settings and menu say why.
- **Free notes per encounter** (a text box above or below the tables). Replaced by note rows, which can go anywhere.
- **Renaming to "Encounter Log" or "Duty Planner"**: "log" suggests recording, and "planner" suggests a timeline.
- **Backup and restore (files and automatic copies).** Built, then removed as overkill; export and import of encounters is enough. See `docs/DECISIONS.md`.
