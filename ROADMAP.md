# Roadmap

What is left, in rough priority order, and what was deliberately not done. See
[docs/DECISIONS.md](docs/DECISIONS.md) for why things are the way they are.

## First: confirm what is built but unseen

Several features were built and pass their automated tests but have not been seen in game. Check them before adding more.
The list is in [docs/TESTING.md](docs/TESTING.md#built-but-not-yet-confirmed-in-game). Anything that behaves oddly comes before
the items below.

## Open

1. **Publishing.** Needed only if it is released:
   - the repository exists (`notingway/duty-notes`, private for now); its address still has to go in the manifest (`RepoUrl`), and it has to be made public;
   - an icon, the custom repository entry (`pluginmaster.json`), `ApplicableVersion`, the version and a changelog entry;
   - decide whether to test through a testing channel first.
2. **An accent for note rows** (a dimmer text colour, or a bar at the left) if they are hard to tell from mechanic rows.
3. **Remember that auto-select opened the overlay across a plugin reload.** Today `overlayOpenedByUs` is in memory, so
   reloading the plugin while inside a duty means leaving that duty will not hide the overlay.
4. **Turn the console tests into a proper test framework** (xunit) and, if a repository is set up, run them in CI. CI needs
   the Dalamud assemblies, which come from a Dalamud distribution, not from NuGet.

## Small things noticed and left

- **The Help tab's macro name.** Next, prev and reset show a suggested name ("DN Next"). Copying it would need its own copy icon: putting the name in the copied macro text is not safe, because a line that is not a command is sent as chat. Proposed, not decided.
- **Macro icons by text.** The macro Icons window can only be used by hand. The game's `/micon` takes names, and the `general` category has "Next Board" and "Previous Board" (» and «) and "Target Forward" and "Target Back" (gold arrows), which could set an icon without a manual step. Not tried in game, and reset has no good fit (see `docs/GAME-DATA.md`).
- Skills beyond 24 in a row of an imported text are dropped silently (not counted in the "dropped" message; this is documented in `docs/FORMATS.md`).

## Declined or deferred (don't redo without a reason)

- **A job-aware overlay** (dim or hide the skills your job doesn't have). Abandoned by the owner. It would have been small: the job matching exists for the skill picker (`ActionCatalog.UsableBy`), with a three-way setting (show all, dim, hide).
- **Live cooldowns on the icons** (greyed with a timer). Abandoned by the owner. It was the biggest and riskiest idea: it needs the game's action manager and internal structures that change between patches.
- **Importing a single phase** into an existing encounter. Abandoned by the owner. Import and export take whole encounters only, so a phase has no Export button.
- **Sorting skills by duration, cooldown or power.** Alphabetical and cooldown would be reliable, but duration and power
  are not in the game data in a usable form (descriptions leave the duration blank). Could return with a hand-curated table.
- **An editable column title** to replace "Mechanic". The word is the community's own; kept.
- **Showing the phase buttons disabled in full-encounter mode.** They are hidden instead, and the settings and menu say why.
- **Free notes per encounter** (a text box above or below the tables). Replaced by note rows, which can go anywhere.
- **Renaming to "Encounter Log" or "Duty Planner"**: "log" suggests recording, and "planner" suggests a timeline.
- **Backup and restore (files and automatic copies).** Built, then removed as overkill; export and import of encounters is enough. See `docs/DECISIONS.md`.
