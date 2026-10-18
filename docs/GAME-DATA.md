# Game data

What the plugin relies on in the game's data tables (Excel sheets, read through Lumina or Dalamud's `IDataManager`), and
what was learned about them. Facts here were found by reading the data in the game version of October 2026 (Dawntrail,
patch 7.x); re-check after a major patch. How to look at a sheet yourself is in [CLAUDE.md](../CLAUDE.md#inspecting-the-games-data-probes).

## Skills (the `Action` sheet)

The sheet is full of rows that look like skills and aren't (many unused or legacy rows share a name: dozens of
"Rampart" and "Reprisal" rows). The usable ones are the rows that pass **all** of these (`ActionCatalog`):

- `IsPlayerAction` or `IsRoleAction`
- `ClassJobLevel > 0` and `ClassJobCategory.RowId != 0` (the junk rows have level 0 and category 0)
- not `IsPvP`, and `Icon != 0`, with a non-empty name

Facts:

- **Second forms are not player actions.** The game puts some actions on the hotbar in place of another while something is active (Eukrasia, a summoned Seraph, a dance step). They are separate rows with `IsPlayerAction` **false**, but a level, a job category and an icon, so they fail only that one test. `ActionCatalog.SecondForms` lists the ones worth offering, by row id and expected name: Sage's Eukrasian Diagnosis 24291, Eukrasian Prognosis 24292 and Eukrasian Prognosis II 37034; Dancer's Improvised Finish 25789; Scholar's Seraphic Veil 16548, Angel's Whisper 16550, Seraphic Illumination 16551, Manifestation 37015 and Accession 37016. They have no job of their own (`ClassJob` is the empty first row), so the job comes from the class category. Most other rows that fail only that test are not worth offering (damage combos, pet and Blue Mage actions, status rows); rows with the same name as a normal action are merged with it anyway. Found by listing every row that fails only `IsPlayerAction` (257 of them) and checking the curated names in `DefensiveSkills.cs` against the sheet: every curated name has a normal row, so only the second forms were missing.
- **Role actions** (Rampart 7531, Reprisal 7535, Addle 7560...) have `ClassJob` = `uint.MaxValue` and a role category (for
  example 113 for tanks), not a job. Which jobs can use a skill is decided by `ClassJobCategory`.
- **`ClassJobCategory`** has one boolean column per job, named by its abbreviation (`WAR`, `PLD`...). `ActionCatalog` reads them by
  name with reflection and caches the answer.
- **Upgrade chains** are not a column. They are in the trait text: `Trait` rows, with the description from
  `TraitTransient`, say "Upgrades Raw Intuition to Bloodwhetting." A regex over those descriptions links the versions into a
  family (Raw Intuition > Bloodwhetting, Sheltron > Holy Sheltron, Sentinel > Guardian, Vengeance > Damnation, Nebula > Great
  Nebula, Heart of Stone > Heart of Corundum). A family is shown as the version available at the encounter's level.
- **Descriptions** come from `ActionTransient`. Do not rely on them for numbers: duration is often blank ("Duration: s",
  as for Reprisal) and potency may be missing, because those values live in status data. That is why sorting skills by
  duration or power was not built.
- **There is no "is this defensive" flag.** Hand-written lists in `DefensiveSkills.cs` decide it (`Names` for mitigation,
  `Healing` for heals). Names are matched in English; a name that is not a real player action simply never matches (a test of
  this once removed `Eukrasian Diagnosis`, which is `Diagnosis` while Eukrasia is active, and `Seraphic Illumination`; they are offered again now that the catalog has those second forms).
- `Action.ActionCategory` 4 is an oGCD ability; 3 is a weaponskill; **9 is a limit break**.
- **Limit breaks fail every filter above** (level 0, no real class category, `IsPlayerAction` false, one generic icon, 103), and
  which one you have depends on your job and the tier. The plugin therefore offers **one** skill, "Limit Break", with an id of its
  own (`Configuration.LimitBreakId`, 4,000,000,001, outside the game's ids) and icon 103. It is not an Action row. The real rows
  are category 9 (for example Shield Wall 197, Stronghold 198, Last Bastion 199, Land Waker 4240, Dark Force 4241, Gunmetal Soul
  17105, Healing Wind 206, Breath of the Earth 207, Pulse of Life 208, Angel Feathers 4247, Astral Stasis 4248, Techne Makre
  24859); category 15 rows are the PvP limit breaks. A version that lasted a day offered those rows as separate skills, and
  `Configuration.Migrate` folds them into the one entry.

## Jobs

- `ClassJob.Abbreviation` is the three-letter name. Plain job icons are `62100 + ClassJob.RowId` (icon files
  `ui/icon/062000/0621xx.tex`).
- The picker lists tanks (PLD 19, WAR 21, DRK 32, GNB 37), healers (WHM 24, SCH 28, AST 33, SGE 40), melee (MNK 20, DRG 22,
  NIN 30, SAM 34, RPR 39, VPR 41), physical ranged (BRD 23, MCH 31, DNC 38) and casters (BLM 25, SMN 27, RDM 35, PCT 42).
  Base classes and Blue Mage are left out on purpose.

## Duties (`ContentFinderCondition`)

- A duty has a `Name`, a `TerritoryType` (the zone), a `ContentType`, `ClassJobLevelRequired` and, through `TerritoryType.ExVersion`,
  an expansion (0 A Realm Reborn ... 5 Dawntrail).
- **The zone is what matches:** `IClientState.TerritoryType` while inside a duty equals the duty's `TerritoryType` row. Confirmed
  in game with the Epic of Alexander (Ultimate): duty row 694, zone 887.
- **Names starting with "the" are stored in lowercase** ("the Epic of Alexander (Ultimate)") because the game uses them inside
  sentences. The plugin capitalizes the first letter, as Party Finder does.
- The picker offers these content types only (`DutyCatalog.ContentTypes`): 2 Dungeons, 4 Trials, 5 Raids, 21 Deep Dungeons,
  28 Ultimate Raids, 29 Save the Queen, 30 V&C Dungeon Finder, 37 Chaotic Alliance Raid, 38 Occult Crescent.
- Icons for those types come from `ContentType.IconDutyFinder` (the ones Party Finder uses). `ContentType.Icon` holds a second
  set of icons that was not used.

## Macro icons

The game's macro **Icons** window (the box next to a macro's name) draws from the `MacroIcon` sheet (250 rows; `MacroIconRedirectOld` maps old ones). Its icons
are ids in the 66xxx range (some, such as the ranks, are older ids). Found by dumping the sheet's icons and looking at them: the grey plate arrows are
**66301 left arrow, 66302 right arrow, 66303 up, 66304 down**, and the loop (two arrows chasing round) is **66329**; 66300 is a "Macro Symbol" label.
Neighbours: 66305 circle, 66306 triangle, 66307 square, 66322 heart, 66327 play, 66328 pause, 66330 and 66331 notes, 66332 to 66335 diagonal arrows.

These can **only be picked in that window**: the macro text cannot choose them. The `/micon` (`/macroicon`) command takes the name of an action, item, emote,
mount and so on (`/micon "name" [category]`, category one of action, blueaction, pvpaction, general, emote, companion, pet, minion, mount, enemysign,
waymark, gearset, classjob, quickchat, item). So the Help tab shows the icon next to each suggested macro and says to pick it by hand.
(The `general` category has gold arrow icons, "Target Forward" 113 and "Target Back" 114, which look different.)

## Other

- Game icons are loaded with `ITextureProvider.GetFromGameIcon(new GameIconLookup(id))`.
- Everything is read once, when the plugin loads, into the catalogs (`ActionCatalog`, `DutyCatalog`, `JobCatalog`).
