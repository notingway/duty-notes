# Releasing

How a version of Duty Notes reaches players, through the custom plugin repository **plogonway**. Players add one address once, and every
release after that arrives as a normal plugin update.

```
notingway/duty-notes   this repository: source, tests, docs, and the GitHub Releases that hold latest.zip
notingway/plogonway    the index: plogonway.json lists the plugins and where to download them
```

Players add this address under `/xlsettings > Experimental > Custom Plugin Repositories`:

```
https://raw.githubusercontent.com/notingway/plogonway/main/plogonway.json
```

**The repository name `plogonway` and the file name `plogonway.json` must never change** once anyone has added that address: a raw GitHub address
cannot redirect.

## Before the first release (once)

- [x] **Both repositories are public** (done on 2026-10-18). Players can't download from a private repository, and Dalamud's custom repositories have no sign-in.
- [x] **An icon** at `images/icon.png` in this repository: square, 64 to 512 pixels, PNG (done: generated with ChatGPT and credited in the README). One screenshot is in (`images/image1.png`, the overlay on a sheet); up to four more are optional (`image2.png` to `image5.png`, at most 730 x 380 pixels each). The release script adds their addresses to the index if the files exist. Not enforced by Dalamud for custom repositories, but the plugin looks bare without them.
- [x] **`plogonway` is cloned next to this repository** (`C:\dev\github\notingway\plogonway`), and `gh` is signed in (`gh auth status`).
- [x] **A test install** on the owner's machine, before telling anyone (see below). Done with v0.1.6 on 2026-10-24.

## Making a release

1. **Decide the version.** Dalamud only offers an update when the version goes **up**. Edit `<Version>` in `DutyNotes\DutyNotes.csproj` (for example `0.1.0.0` to `0.1.1.0`). The tag is made from it (`0.1.0.0` gives `v0.1.0`).
2. **Update `CHANGELOG.md`** and make sure `docs/TESTING.md` has nothing unconfirmed that you would not want to ship.
3. **Commit and push** to `main`. The script refuses to run with uncommitted or unpushed changes.
4. **Rehearse:**

   ```powershell
   .\scripts\release.ps1 -DryRun
   ```

   It checks everything, builds in Release, runs the tests, checks `latest.zip` and the manifest the build generated, and prints the release it
   would create and the exact `plogonway.json` it would write. It changes nothing.
5. **Release:**

   ```powershell
   .\scripts\release.ps1 -Prerelease -Notes "What changed, in a sentence or two."
   ```

   (`-Prerelease` marks the GitHub release as a pre-release, which suits an experimental plugin. The notes are also the changelog shown in the plugin installer.)

The script creates the GitHub release **first**, with `latest.zip` attached, and only then updates `plogonway.json` in the index repository and pushes it, so
the index never points at a download that doesn't exist. The index entry is built from the manifest the build generated (`DutyNotes.json`), so the name,
version, API level and description can't drift from the csproj; the script adds only the download links, `LastUpdate` and the notes. If updating the
index fails after the release was created, it says so: fix the problem in the index repository and push `plogonway.json` there. Don't create the release again.

**Run the script plainly, without redirecting its output (no `2>&1`).** Windows PowerShell 5.1 turns anything a native command prints on its error stream (git's \\warning:\\ lines, for example) into an error when it is redirected, and that stopped the first real run after the release had been created but before the index was updated (the fix was to commit and push \\plogonway.json\\ in the index repository by hand, which is what the script's own warning says).

## Releases so far

The icon and screenshot addresses are in `DutyNotes.csproj` (`IconUrl`, `ImageUrls`) and also written into the index entry by the script. Both are needed: an installed plugin shows the icon named in the manifest inside `latest.zip`, while the plugin list before installing shows the one in the index.

- **v1.0.0** (2026-10-24), the first full release (not a pre-release): **Show phase buttons** moved to the top of the editor, the README's note that the plugin is made for the English game client, and everything in the plugin confirmed in game.
- **v0.1.6** (2026-10-24), a pre-release: skill tooltips at the encounter's level (the full card with the skill's cast, recast, range, description and numbers as at that level and job; see CHANGELOG). Confirmed on the dev build by the owner, then installed from the repository (the plogonway plugin list, with the dev plugin off) and confirmed working by the owner on 2026-10-24: `/dutynotes edit`, the sample import, the overlay, and the icon after a game restart.
- **v0.1.5** (2026-10-19), a pre-release: sharper text while a size is built, a new encounter is selected (not its first phase), new phases are called "New phase", and the 0.1.4 font log lines are removed. Not yet installed from the repository.
- **v0.1.4** (2026-10-18), a pre-release: text scaling for players with another global UI scale (see CHANGELOG), sent to one tester. Reported working on her machine after a first test where the text was still blurry (the stretched fallback); what changed between the two tests is not known. Scaling at a global scale other than 100% was later confirmed by the owner with 0.1.6.
- **v0.1.3** (2026-10-18), a pre-release: the second forms of some actions in the picker (see CHANGELOG). Not yet installed from the repository.
- **v0.1.2** (2026-10-18), a pre-release: the overlay no longer runs off the screen when the full encounter is switched off (see CHANGELOG). Not yet installed from the repository.
- **v0.1.1** (2026-10-18), a pre-release: the icon in the package's manifest (see CHANGELOG). Confirmed by the owner: the icon shows once installed, **after restarting the game** (Dalamud kept the missing icon from 0.1.0 until then, so an icon change is only visible after a game restart).
- **v0.1.0** (2026-10-18), a pre-release. Installed from the repository and confirmed working by the owner. (The first attempt failed because the address pasted was the browser page, `github.com/.../blob/main/plogonway.json`, which serves HTML. The address has to be the raw one, `raw.githubusercontent.com/...`.)

## Testing the install path

The plugin installed from a repository is a different copy from the dev plugin: both have the internal name `DutyNotes`, so only one can be loaded.

1. In `/xlplugins > Dev Tools > Installed Dev Plugins`, **disable** the dev plugin (or remove its path under `/xlsettings > Experimental > Dev Plugin Locations`).
2. Add the repository address above in `/xlsettings > Experimental > Custom Plugin Repositories`, save, and open `/xlplugins`.
3. Find **Duty Notes** under All Plugins, install it, and run through the basics: `/dutynotes edit`, import `samples/example-encounter.txt`, show the overlay.
4. **Restart the game to judge the icon**: the plugin list remembers a missing icon until Dalamud is restarted.
5. Settings carry over: both copies use `pluginConfigs\DutyNotes.json`.

## If something goes wrong

- **Hide a bad release at once:** set `"IsHide": true` on the entry in `plogonway.json` (or remove the entry), commit and push. New installs stop; people who already have it keep it.
- **Ship a fix:** bump `<Version>` and release again. A higher version replaces the entry.
- **Never change `InternalName`, the assembly name or the namespace** (saved settings record them). The script refuses a release whose manifest `InternalName` is not `DutyNotes`.

## What also needs maintenance

- **Dalamud API bumps** (usually with a game expansion): `DalamudApiLevel` follows the SDK version in `DutyNotes.csproj` (`Dalamud.NET.Sdk/15.0.0`). When Dalamud moves to a new API, update the SDK version, rebuild, test in game, and release again. Players on the new API won't be offered the old build.
- **Third-party status:** Dalamud offers only minimal support for plugins from custom repositories, and shows its usual third-party warning. If the plugin is ever submitted to the official repository, that is a separate process with its own rules (a public repository, a manifest, and a human-written AI disclosure); see `ROADMAP.md`.