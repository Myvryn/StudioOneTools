# Proposal: Song Doctor (corrupted `.song` recovery)

**Priority:** Highest — solves a problem with no existing tool anywhere.
**Effort estimate:** M (the read/salvage side is straightforward XML/ZIP work
the codebase already does; the UI for reviewing what was recovered is the
larger piece).

## Problem

"File is broken, or could not be opened" is a well-documented Studio One dead
end. There's no built-in repair. The only known workaround, repeated across
multiple threads, is entirely manual:

1. Open a brand-new empty song.
2. In the browser, navigate to the corrupted `.song` file and right-click →
   "Show Package Contents."
3. Manually locate and drag out whatever MIDI parts / FX chains / media you can
   find, one at a time, hoping the internal XML isn't itself corrupted.

Sources: [F*!@ Studio One! — Gearspace](https://gearspace.com/board/presonus-studio-one/744911-f-studio-one.html),
["File is broken or can't be opened" — PreSonus Answers](https://answers.presonus.com/63434/file-is-broken-or-cant-be-opened),
["Studio song crashed... file broken" — PreSonus Answers](https://answers.presonus.com/30124/studio-song-crashed-and-song-the-file-broken-could-not-opened).

## Why this fits

A `.song` file is already known (from `StudioOneSongAnalyzer`) to be a ZIP
containing `Song/song.xml`, `Song/mediapool.xml`, `Devices/audiomixer.xml`, and
a `Media/` folder alongside it on disk. "Broken" almost always means **one**
entry in that ZIP is truncated or unreadable — not that everything is gone.
`IStudioOneSongAnalyzer.DiscoverSongStructure(string songFilePath)` already
walks a `.song` archive's structure for debugging; Song Doctor is substantially
"run that walk defensively, entry by entry, and report what survived" instead
of assuming the whole archive parses cleanly.

## Proposed shape

```
Core/Contracts/ISongDoctor.cs
    SongDoctorReport Diagnose(string songFilePath);
    SongRecoveryResult Recover(string songFilePath, string outputSongFolderPath, SongRecoveryOptions options);

Core/Models/SongDoctorReport.cs
    sealed class SongDoctorReport
    {
        required string SongFilePath { get; init; }
        required bool ZipStructureIntact { get; init; }       // can the ZIP central directory even be read?
        required IReadOnlyList<SongDoctorEntryStatus> Entries { get; init; }
        required bool MediaFolderFound { get; init; }
        required IReadOnlyList<string> RecoverableMediaFiles { get; init; }
        bool IsFullyRecoverable => Entries.All(e => e.IsReadable);
    }

Core/Models/SongDoctorEntryStatus.cs
    sealed class SongDoctorEntryStatus
    {
        required string ArchivePath { get; init; }    // e.g. "Song/song.xml"
        required bool IsReadable { get; init; }        // did XDocument.Load succeed?
        string? ParseError { get; init; }
    }

Core/Models/SongRecoveryOptions.cs
    sealed class SongRecoveryOptions
    {
        required bool RebuildFromEmptyTemplate { get; init; }  // start from a known-good empty .song skeleton
        required bool IncludeMedia { get; init; }
    }

Core/Models/SongRecoveryResult.cs
    sealed class SongRecoveryResult
    {
        required bool Succeeded { get; init; }
        required string OutputSongFilePath { get; init; }
        required IReadOnlyList<string> RecoveredMediaFiles { get; init; }
        required IReadOnlyList<string> LostEntries { get; init; }   // what couldn't be salvaged, for the user's awareness
    }
```

## How recovery actually works

The realistic recovery strategy mirrors the manual workaround, automated:

1. **Diagnose** — open the ZIP (if the central directory itself is unreadable,
   stop here and report "not recoverable, ZIP container is damaged" — this is
   the one case with no path forward).
2. For each of the known entries (`Song/song.xml`, `Song/mediapool.xml`,
   `Devices/audiomixer.xml`, any `Arrangement/*.xml`), try to load it as XML
   independently. A truncated/corrupt entry fails in isolation — it doesn't
   have to take down the whole diagnosis.
3. **Recover** — start from a minimal known-good empty `.song` skeleton
   (checked into the repo as a fixture, generated once from a real empty Studio
   One song), then splice in whichever of the corrupted song's entries parsed
   successfully. `Media/` on disk next to the `.song` file is untouched by
   corruption in the vast majority of reported cases (it's the `.song` package
   itself that dies, not the media folder) — so recovered media is often
   "everything," even when song.xml itself needs partial reconstruction.
4. Report exactly what was recovered vs. lost, so the user isn't surprised
   Studio One opens something that's missing automation or a track.

## UX flow (mirrors Path Fixer's "diagnose → confirm → act → re-verify" shape)

1. Select a `.song` file (or Song Archiver's existing folder-select flow, just
   pointed at a folder with a broken song in it).
2. Diagnosis runs automatically, shown as a per-entry checklist (✓/✗ per XML
   file) — same visual language as Path Fixer's "stored path vs. current path"
   side-by-side.
3. If anything is recoverable: "Attempt Recovery" button, writes to a **new**
   file/folder (never overwrites the original — same non-destructive posture
   as Path Fixer's temp-file-then-swap pattern).
4. Result summary: what was recovered, what wasn't, with a clear warning that
   this is best-effort and the user should open it in Studio One and inspect
   before trusting it as the source of truth.

## Open questions

- Need a real corrupted `.song` sample to validate against — can't build the
  entry-by-entry defensive parser without seeing what "truncated XML" actually
  looks like in practice (mid-tag cutoff? valid XML with garbage content? zero
  bytes?). Corrupt a test file deliberately (truncate a real `.song`'s
  `song.xml` at various byte offsets) to cover realistic failure modes.
- Should the "empty template" skeleton be bundled as a resource, or generated
  fresh by having the user point at a known-good empty Studio One song once
  during setup? Bundling is simpler for the user; generating avoids drift if
  Studio One's schema changes across versions.
