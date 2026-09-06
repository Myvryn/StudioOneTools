# Proposal: Library Cleanup (cache, pool, history, duplicate takes)

**Priority:** Medium — real, recurring complaint, but Studio One already has
manual per-song fixes; the value-add here is purely "do it across your whole
library in one pass" instead of one song at a time.
**Effort estimate:** M — three related but distinct scan types under one
UI, each individually simple.

## Problem

Three separate but thematically identical complaints, all boiling down to
"Studio One accumulates disk-bloating cruft per-song, and cleaning it up is
manual and per-song":

1. **Cache bloat.** The `Cache/Images` and `Cache/Audio` subfolders hold
   waveform renders and re-rendered audio from operations like time-stretch —
   stretching one event 10 times creates 10 cached audio files. Studio One's
   own "Cleanup Cache" (via the performance meter) handles one song at a time.
   Source: [Cache Files — Studio One & Fender Studio Pro User Forum](https://studiooneforum.com/threads/cache-files.756/),
   [Studio One taking up a lot of space on my harddrive — PreSonus Forums](https://forums.presonus.com/viewtopic.php?f=213&t=6731).
2. **History/Versions bloat.** "Save New Version" piles up in each song's
   `History` folder; one report describes a song folder swelling from
   2–3 GB to 12–15 GB from accumulated takes and versions, with the only
   documented safe cleanup being Studio One's manual "Save to New Folder"
   (which minimizes one song at a time, and doesn't touch the original).
   Source: [Episode 78: Save As vs Versions in Studio One — PreSonus KB](https://support.presonus.com/hc/en-us/articles/115005943506-Episode-78-Save-As-vs-Versions-in-Studio-One),
   [Safe Keeping — Sound on Sound](https://www.soundonsound.com/techniques/safe-keeping).
3. **Duplicate takes.** A dedicated forum thread specifically complains about
   `*.wav(1)`, `*.wav(9)`-style incremented duplicate filenames littering
   `Media` folders from repeated recording takes, scattered across multiple
   song locations and drives after a studio rebuild.
   Source: [Studio One File & Folder Management — Studio One & Fender Studio Pro User Forum](https://studiooneforum.com/threads/studio-one-file-folder-management.619/).

## Why this fits

This is the most direct extension of an existing tool in the suite:
`ISongFolderSweeper.Sweep(rootFolderPath) -> IReadOnlyList<SweepFolderResult>`
already walks a folder tree and flags things by reason ("No .song file", "Not
modified in over a year"). Library Cleanup is the same shape — walk a tree,
flag things, let the user pick, confirm, delete — just flagging *inside* each
song folder (Cache/, History/, duplicate Media/ filenames) instead of flagging
whole orphaned folders.

## Proposed shape

```
Core/Contracts/ILibraryCleanupScanner.cs
    LibraryCleanupReport Scan(string libraryRootPath, LibraryCleanupOptions options);

Core/Models/LibraryCleanupOptions.cs
    sealed class LibraryCleanupOptions
    {
        required bool IncludeCacheCleanup { get; init; }
        required bool IncludeHistoryPruning { get; init; }
        required int KeepMostRecentVersions { get; init; }       // e.g. keep last 5, flag the rest
        required int? PruneVersionsOlderThanDays { get; init; }  // alternative/additional cutoff
        required bool IncludeDuplicateTakeDetection { get; init; }
    }

Core/Models/LibraryCleanupReport.cs
    sealed class LibraryCleanupReport
    {
        required IReadOnlyList<CacheCleanupItem> CacheItems { get; init; }
        required IReadOnlyList<HistoryPruneItem> HistoryItems { get; init; }
        required IReadOnlyList<DuplicateTakeGroup> DuplicateTakeGroups { get; init; }
        long TotalReclaimableBytes => CacheItems.Sum(c => c.SizeBytes)
                                     + HistoryItems.Sum(h => h.SizeBytes)
                                     + DuplicateTakeGroups.Sum(g => g.ReclaimableBytes);
    }

Core/Models/CacheCleanupItem.cs
    sealed class CacheCleanupItem
    {
        required string SongFolderPath { get; init; }
        required string CacheFolderPath { get; init; }   // .../Cache/Audio or .../Cache/Images
        required long SizeBytes { get; init; }
    }

Core/Models/HistoryPruneItem.cs
    sealed class HistoryPruneItem
    {
        required string SongFolderPath { get; init; }
        required IReadOnlyList<string> VersionFilesToRemove { get; init; }  // the ones NOT in the "keep" window
        required IReadOnlyList<string> VersionFilesToKeep { get; init; }
        required long SizeBytes { get; init; }
    }

Core/Models/DuplicateTakeGroup.cs
    sealed class DuplicateTakeGroup
    {
        required string SongFolderPath { get; init; }
        required string BaseFileName { get; init; }             // e.g. "Vocal Take"
        required IReadOnlyList<string> DuplicateFilePaths { get; init; }  // the "(1)", "(9)" siblings
        required bool AnyDuplicateIsReferencedInSong { get; init; }       // cross-check against mediapool.xml before ever suggesting deletion
        long ReclaimableBytes { get; init; }
    }
```

## UX flow (mirrors Folder Sweeper exactly)

1. Select a library root — same pre-populated-from-settings, auto-scan-on-open
   pattern as Sweeper.
2. Three collapsible sections (Cache / History / Duplicate Takes), each a
   checkbox list with size shown per item and a running total at the top —
   same visual pattern as the existing per-row reason + full path in Sweeper.
3. **Duplicate Takes is the one that needs the most care**: never suggest
   deleting a file that `mediapool.xml` still references as used, even if it
   matches the `(N)` naming pattern — cross-reference against
   `SongAnalysisResult.MediaFiles[].IsUsed` (already computed by the existing
   analyzer) before ever surfacing a duplicate as safe to remove. Default the
   checkbox unchecked for anything ambiguous; only pre-check duplicates that
   are unambiguously orphaned (zero references anywhere in the song).
4. Confirmation dialog before anything is removed — total size reclaimed,
   file count — same as Sweeper's existing confirmation step.

## Open questions

- **History pruning safety**: Studio One's own docs are explicit that "Save
  New Version" files are the *only* rollback mechanism a user has for that
  song. Pruning needs a very visible warning (more so than Sweeper's, which
  only touches orphaned/junk folders) since this touches a song's actual
  undo history, not junk. Strongly consider defaulting
  `KeepMostRecentVersions` to a conservative number (10+) rather than
  encouraging aggressive pruning.
- **Duplicate detection heuristic**: is `(N)` suffix matching enough, or do
  real-world duplicate takes also show up as Windows' own "file - Copy.wav" /
  "file (1).wav" auto-rename pattern from drag-and-drop duplication outside
  Studio One? Worth checking both patterns.
- Consider whether this should be three separate simpler tools instead of one
  combined scanner — the combined report is convenient, but if any one of the
  three (especially History pruning, given its risk) needs materially
  different UX/warnings, splitting may be cleaner than forcing them into one
  results view.
