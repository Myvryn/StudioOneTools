# Feature Proposals

Sketches for new tools, sourced from real user pain points found across PreSonus
Answers, Gearspace, KVR Audio, and the independent Studio One forums (research
done 2026-07). Each doc is a design sketch, not a spec — enough to evaluate
feasibility and scope before committing to a build.

None of these are audio-engine features (no DSP, no plugin hosting) — they're
all file/project housekeeping, which is exactly this app's lane. Every one of
them is a variation on the same shape the app already has: parse `.song`
internals and/or walk a folder tree, flag things, let the user pick, confirm,
act.

## Proposals, by recommended priority

| # | Proposal | Problem | Fit |
|---|---|---|---|
| 1 | [Song Doctor](song-doctor.md) | Corrupted `.song` → "File is broken", no repair path exists anywhere | New capability, no competing tool |
| 2 | [Portability Check](portability-check.md) | Songs silently break when moved/shared — missing plugins/soundsets only discovered after opening | Thin layer over existing `IStudioOneSongAnalyzer.Analyze()` |
| 3 | [Plugin Finder](plugin-finder.md) | "Which of my songs use plugin X?" — no way to check before uninstalling/replacing a plugin | Same analyzer, indexed the other direction |
| 4 | [Library Cleanup](library-cleanup.md) | Cache/Pool bloat, orphaned History versions, duplicate takes — all currently manual, per-song | Extends `ISongFolderSweeper`'s scan-and-flag pattern to library scale |
| 5 | [Template Organizer](template-organizer.md) | Long flat template list, no categorization/reordering | Standalone, lowest priority — smaller audience |

## Shared architectural note: `Plugins` as `string` vs `SongPlugin`

`SongAnalysisResult.Plugins` is currently `IReadOnlyList<string>` (display-name
strings, already deduplicated of version suffixes like "StudioRack Stereo 4" →
"StudioRack Stereo"). `SongPlugin` (`Vendor` + `Name` + `DisplayName`) exists in
`Core/Models` but isn't what `Analyze()` populates today.

Proposals #2 and #3 both need to match plugins against "what's installed on this
machine," which is more reliable done by vendor+name than by display string.
Worth deciding once, before building either: promote `Analyze()` to return
`IReadOnlyList<SongPlugin>` instead of strings, and thread that through the four
places that already read `.Plugins` (Song Archiver's UI, the HTML report
generator, etc.). Flagged in both docs; solving it once unblocks both.
