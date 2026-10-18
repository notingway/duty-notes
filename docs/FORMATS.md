# Formats

Two things are written to disk or the clipboard. The export text is meant to travel between people and versions, so it
carries a version number and **old versions must keep working**. The code is in `Sharing.cs` and `Configuration.cs`.

| Format | Version | Where it is written | Made for |
| --- | --- | --- | --- |
| Export text | 1 | the clipboard | one encounter, shared between people |
| Settings file | none | `pluginConfigs\DutyNotes.json` | the plugin itself |

The export text uses its own plain data shape, not the settings classes. That keeps it stable when the
settings classes change.

## Export text

```
DutyNotes:1:<base64>
```

The base64 is the **gzip** of a compact UTF-8 JSON document:

```json
{
  "v": 1,
  "e": {
    "n": "TEA WAR MT",        // name
    "l": 80,                  // level (1-100)
    "d": 694,                 // duty: ContentFinderCondition row, 0 for none
    "j": 21,                  // job: ClassJob row, 0 for any job
    "r": [                    // rows of the encounter itself: no longer written; on import they become a first phase named after the encounter
      { "m": "Raidwide", "s": [7531, 7535], "o": "Every pull" },   // mechanic row: mechanic, action ids, notes
      { "t": "Stack on the marker" },                               // note row: its text
      { "m": "Spread", "x": true }                                  // "x": true is a row that is switched off
    ],
    "p": [                    // phases, each { n, l, r } (duty and job apply to the encounter only)
      { "n": "Phase 1", "l": 80, "r": [ ... ] },
      { "n": "Phase 2", "l": 80, "x": true, "r": [ ... ] }   // "x": true is a phase that is switched off
    ]
  }
}
```

Defaults and nulls are left out. A row with a `"t"` is a note row; any other row is a mechanic row.

`"x": true` marks a phase **or a row** (mechanic or note) that is switched off (it is stored as "off" and not "on" because default values are left out and the default of a
true/false value is false). Text without the key, which is all text made before phases and rows could be switched off, has every phase and row on, so
this fits format version 1 without a bump. An older plugin reading new text ignores the key and shows every phase and row. Only phases use it: the
encounter itself is always on.

**Reading it** (`Sharing.Import`) treats it as untrusted:

- text over 400,000 characters, a missing or wrong prefix, bad base64, bad gzip or bad JSON are refused with a message;
- the JSON is read with `TypeNameHandling.None` into plain DTOs, and unzipping stops at 1,000,000 bytes (a gzip bomb);
- at most 60 phases, 400 rows per sheet and 4,000 rows in all (more than that refuses the text), and 24 actions per row (actions beyond
  the 24th are cut off, **without** being counted in the "left out" message that reports unknown actions);
- names are cut to 128 characters, mechanics to 128, notes to 256, note rows to 1,000; control characters other than line
  breaks are removed; levels are clamped to 1-100;
- action ids that don't exist in this game version are dropped (the one exception to "an id is a game Action row" is `Configuration.LimitBreakId`, 4,000,000,001, the plugin's single Limit Break action) (and counted), the same action twice in a row is kept once;
- the duty is looked up in this game version's data (unknown: cleared and reported), and its zone is taken from there, never
  from the text; an unknown job becomes "any job";
- every sheet gets a fresh id.

**Which message you get.** The header decides it, before anything is decoded (`Sharing.Import`):

| Text starts with | Result |
| --- | --- |
| `DutyNotes:1:` | read normally |
| `DutyNotes:<n>:` with `n` greater than 1, or too large to read as a number (for example `DutyNotes:2:...`) | refused: "That text was made by a newer version of Duty Notes. Update the plugin to import it." |
| anything else that doesn't match (`DutyNotes:0:`, `DutyNotes:x:`, a missing colon, lowercase, another plugin's text) | refused: "That doesn't look like Duty Notes text. It should start with "DutyNotes:1:"." |

A text that has a `DutyNotes:1:` header but whose JSON says `"v"` is anything other than 1 is refused with the same "newer version"
message. There are two version numbers: the header's (the container) and `"v"` (the content). They are both 1 today and are
meant to move together.

**Compatibility rule:** if the format changes, bump the version (header and `"v"`), keep reading every older version, and add tests
for each (`SharingTests.cs`). The "newer version" message depends on the plugin that reads the text not knowing the version, so
older plugins already in people's hands will give it for your new text.

## Settings file

`%APPDATA%\XIVLauncher\pluginConfigs\DutyNotes.json`, written and read by Dalamud from the `Configuration` class
(`Configuration.cs`). It is **not** a stable interchange format.

- It records type names (`"$type": "DutyNotes.SheetNode, DutyNotes"`), so renaming the namespace, the assembly or a class
  breaks loading unless the old names are migrated (see `Configuration.RenameLegacyTypes`).
- A setting missing from an older file takes its field initializer value, which is the value in `Defaults`.
- A field the plugin no longer has is ignored when loading (the removed free-notes fields and the removed automatic backup
  settings `AutoBackup`, `AutoBackupFolder` and `AutoBackupKeep` are examples; a test covers the backup ones).
- Every list in a saved class carries `[JsonProperty(ObjectCreationHandling = Replace)]`; see [DECISIONS.md](DECISIONS.md).
- Stale data is healed on load by `Configuration.Migrate`: the same node listed twice is
  kept once, anything nested deeper than a phase is hoisted, and ids that no longer exist fall back to the first encounter or phase (or to nothing, when
  there are no encounters, which is allowed). The flat list of rows from the very first version still becomes one encounter.
