# Roadmap

What is left, in rough priority order, and what was deliberately not done. See
[docs/DECISIONS.md](docs/DECISIONS.md) for why things are the way they are.

## First: confirm what is built but unseen

Several features were built and pass their automated tests but have not been seen in game. Check them before adding more.
The list is in [docs/TESTING.md](docs/TESTING.md#built-but-not-yet-confirmed-in-game). Anything that behaves oddly comes before
the items below.

## Open

1. **Continuous integration** (a GitHub Actions workflow). **On hold for a while**, but to be done before the repository goes public
   (the tests are now xunit, so it is ready for this). The plan: set up .NET 10, download Dalamud's published distribution (the assemblies are not on NuGet; the build points at them
   with `DalamudLibPath`, which both projects already support), build the plugin, and run `dotnet test`. Check what the distribution contains
   before writing it. GitHub Actions is free for public repositories, and private ones have a monthly allowance far above what this needs.
2. **Publishing.** Needed only if it is released:
   - the repository exists (`notingway/duty-notes`, private for now) and is already the manifest's `RepoUrl`; it has to be made public;
   - already done: the build uses `Dalamud.NET.Sdk` and generates the manifest and `latest.zip`; the custom repository `notingway/plogonway` exists (private) with an empty `plogonway.json`; `scripts\release.ps1` and `docs/RELEASING.md` are written (the script has been dry-run only);
   - the icon (`images/icon.png`) and one screenshot (`images/image1.png`) are done; still to do for a first experimental release: more screenshots (optional), making `duty-notes` and `plogonway` public, the first real `scripts\release.ps1` run, and a test install (see `docs/RELEASING.md`);
   - an icon, the custom repository entry (`pluginmaster.json`), `ApplicableVersion`, the version and a changelog entry;
   - decide whether to test through a testing channel first.
3. **An accent for note rows** (a dimmer text colour, or a bar at the left) if they are hard to tell from mechanic rows.
4. **Remember that auto-select opened the overlay across a plugin reload.** Today `overlayOpenedByUs` is in memory, so
   reloading the plugin while inside a duty means leaving that duty will not hide the overlay.
## Small things noticed and left

- **The Help tab's macro name.** Next, prev and reset show a suggested name ("DN Next"). Copying it would need its own copy icon: putting the name in the copied macro text is not safe, because a line that is not a command is sent as chat. Proposed, not decided.
- **Macro icons by text.** The macro Icons window can only be used by hand. The game's `/micon` takes names, and the `general` category has "Next Board" and "Previous Board" (» and «) and "Target Forward" and "Target Back" (gold arrows), which could set an icon without a manual step. Not tried in game, and reset has no good fit (see `docs/GAME-DATA.md`).
- Skills beyond 24 in a row of an imported text are dropped silently (not counted in the "dropped" message; this is documented in `docs/FORMATS.md`).

## Declined or deferred (don't redo without a reason)

- **Caching the overlay's per-frame layout** (found by the publishing review: lists, LINQ and text wrapping are rebuilt every frame). Deferred by the owner. The cost is small for a normal sheet and nothing has been measured, while a stale cache would be a worse bug than the allocations it saves. Do it only if someone sees stutter, a reviewer asks, or a measurement (a stopwatch around `OverlayWindow.Draw`) shows it matters, and then fix only what the measurement points at.
- **A guard for a failed settings load** (the review worried that an unreadable settings file would start empty and be overwritten by the next save). Deferred by the owner. Dalamud already writes plugin settings through `ReliableFileStorage` (atomic writes, plus a copy in an internal database it recovers from if the file is corrupt), which covers the case. What is not confirmed is what the plugin sees if both copies fail; at most, log it and show a message.

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
