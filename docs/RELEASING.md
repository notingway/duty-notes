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
- [ ] **A test install** on the owner's machine, before telling anyone (see below).

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

- **v0.1.0** (2026-10-18), a pre-release. Installed from the repository and confirmed working by the owner. (The first attempt failed because the address pasted was the browser page, `github.com/.../blob/main/plogonway.json`, which serves HTML. The address has to be the raw one, `raw.githubusercontent.com/...`.)

## Testing the install path

The plugin installed from a repository is a different copy from the dev plugin: both have the internal name `DutyNotes`, so only one can be loaded.

1. In `/xlplugins > Dev Tools > Installed Dev Plugins`, **disable** the dev plugin (or remove its path under `/xlsettings > Experimental > Dev Plugin Locations`).
2. Add the repository address above in `/xlsettings > Experimental > Custom Plugin Repositories`, save, and open `/xlplugins`.
3. Find **Duty Notes** under All Plugins, install it, and run through the basics: `/dutynotes edit`, import `samples/example-encounter.txt`, show the overlay.
4. Settings carry over: both copies use `pluginConfigs\DutyNotes.json`.

## If something goes wrong

- **Hide a bad release at once:** set `"IsHide": true` on the entry in `plogonway.json` (or remove the entry), commit and push. New installs stop; people who already have it keep it.
- **Ship a fix:** bump `<Version>` and release again. A higher version replaces the entry.
- **Never change `InternalName`, the assembly name or the namespace** (saved settings record them). The script refuses a release whose manifest `InternalName` is not `DutyNotes`.

## What also needs maintenance

- **Dalamud API bumps** (usually with a game expansion): `DalamudApiLevel` follows the SDK version in `DutyNotes.csproj` (`Dalamud.NET.Sdk/15.0.0`). When Dalamud moves to a new API, update the SDK version, rebuild, test in game, and release again. Players on the new API won't be offered the old build.
- **Third-party status:** Dalamud offers only minimal support for plugins from custom repositories, and shows its usual third-party warning. If the plugin is ever submitted to the official repository, that is a separate process with its own rules (a public repository, a manifest, and a human-written AI disclosure); see `ROADMAP.md`.