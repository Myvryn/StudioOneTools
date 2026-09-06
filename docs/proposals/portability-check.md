# Proposal: Portability Check

**Priority:** High — cheapest build of the five, reuses the most existing code.
**Effort estimate:** S — almost entirely UI + a "what's installed" lookup;
the hard part (extracting plugins from a song) is already done.

## Problem

Songs silently break when moved to another computer or handed to a
collaborator, and the failure is only discovered **after** opening — Studio
One doesn't warn you up front that a plugin, soundset, or (on cross-platform
moves) plugin *format* your song depends on isn't available on the target
machine.

Sources: [Studio One 6: Moving Songs Between Computers — PreSonus KB](https://support.presonus.com/hc/en-us/articles/9389658069773-Studio-One-6-Moving-Songs-Between-Computers),
[Studio One 6: Why is my song telling me there are missing files? — PreSonus KB](https://support.presonus.com/hc/en-us/articles/9168067437069-Studio-One-6-Why-is-my-song-telling-me-that-there-are-missing-files),
[Why Are All Of My Files Constantly Missing In Studio One? — KVR Audio](https://www.kvraudio.com/forum/viewtopic.php?t=494812).
The KB itself confirms the AU→VST cross-platform gap: songs built with AU
plugins on macOS show those as "missing" when opened on Windows, even if the
VST3 equivalent is installed, because Studio One doesn't substitute formats.

## Why this fits

`StudioOneSongAnalyzer.Analyze()` **already extracts every plugin a song
uses** — `SongAnalysisResult.Plugins`, pulled from `song.xml`, `mediapool.xml`,
and `Devices/audiomixer.xml`, deduplicated of version suffixes. Song Archiver
already surfaces this list; Portability Check is "take that same list and
cross-reference it against what's actually installed on this machine," which
is new small logic (a plugin registry scan), not new song-parsing logic.

## Proposed shape

```
Core/Contracts/IPortabilityChecker.cs
    PortabilityReport Check(SongAnalysisResult analysis);
    PortabilityReport Check(string songFolderPath);   // convenience overload, calls Analyze() internally

Core/Contracts/IInstalledPluginRegistry.cs
    IReadOnlyList<InstalledPlugin> GetInstalledPlugins();   // scans standard VST2/VST3/AU search paths + Studio One's own PlugInScanner cache if readable

Core/Models/InstalledPlugin.cs
    sealed class InstalledPlugin
    {
        required string Name { get; init; }
        required string Vendor { get; init; }
        required PluginFormat Format { get; init; }   // enum: VST2, VST3, AU (AU only meaningful if ever cross-built for Mac)
    }

Core/Models/PortabilityReport.cs
    sealed class PortabilityReport
    {
        required string SongName { get; init; }
        required IReadOnlyList<PortabilityFinding> MissingPlugins { get; init; }
        required IReadOnlyList<PortabilityFinding> FormatMismatches { get; init; }  // e.g. song used AU, only VST3 installed
        bool IsFullyPortable => MissingPlugins.Count == 0 && FormatMismatches.Count == 0;
    }

Core/Models/PortabilityFinding.cs
    sealed class PortabilityFinding
    {
        required string PluginDisplayName { get; init; }
        required string Reason { get; init; }   // "Not installed on this machine" / "Only AU installed; song used VST3"
    }
```

## UX flow

Fits naturally as a companion panel on **Song Archiver** (run automatically
alongside the existing analysis, since the plugin list is already computed
there) rather than a standalone window — the existing "Issues" list in the
Archiver's analysis view (`SongAnalysisResult.Issues`) is exactly where
"Plugin 'Fab Filter Pro-Q 3' is not installed on this machine" belongs
alongside "Referenced media file is missing."

A standalone entry point also makes sense for the specific "am I safe to zip
this up and send it to my collaborator" moment — run it against a folder
before archiving, independent of actually building the archive.

1. Select a song folder (reuse the existing folder-picker + auto-scan pattern
   from Sweeper/Archiver).
2. Report shows two sections: **Missing entirely** and **Format mismatch**
   (only relevant if the registry scan finds the same plugin name in a
   different format than the song references).
3. No destructive action here at all — this is a read-only report. No
   confirmation dialog needed, unlike every other tool in the suite.

## Open questions

- **Where does "installed on this machine" come from?** Two options:
  (a) scan the standard VST2/VST3 folder locations directly (simple, but
  duplicates work Studio One's own scanner does and can drift from what Studio
  One considers "installed" if the user has custom scan paths configured), or
  (b) read Studio One's own plugin scan cache/log if its format and location
  are discoverable and stable across versions (more accurate, more fragile to
  version changes). Needs a spike against a real Studio One install before
  committing.
- **Cross-platform (AU) relevance is Windows-only for now** — this app is
  Windows/.NET; a song built on someone else's Mac with AU plugins is the
  scenario, not anything running on this machine. Format-mismatch detection
  only needs to know "the song used format X" vs. "only format Y is
  installed here," which the existing plugin extraction plus a Windows-side
  registry scan can already answer without needing to run on macOS.
- Resolve the shared `Plugins: IReadOnlyList<string>` vs. `SongPlugin`
  question (see proposals README) before building — matching against
  installed plugins by display string alone risks false negatives if a vendor
  prefix is inconsistently present.
