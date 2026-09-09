# Proposal: Plugin Finder ("which songs use plugin X")

**Priority:** Medium-high — small, self-contained, high reuse of existing code.
**Effort estimate:** S — a loop over `Analyze()` across a folder tree plus a
results table; no new song-parsing logic needed at all.

## Problem

The reverse of [Portability Check](portability-check.md): before uninstalling
or replacing a plugin, there's no way to check which of your existing songs
depend on it. Discovery threads about plugins going missing/showing up as
"blocklisted" or "not found" (e.g. after an update or reinstall) consistently
point at Studio One's Plugin Manager as the fix *for the plugin registration
itself*, but nothing addresses "and by the way, which of my 200 songs use
this thing" — that's on the user to remember or discover the hard way, one
song at a time.

Sources: [How can I get my 3rd-party plug-ins to show up in Studio One? — PreSonus KB](https://support.presonus.com/hc/en-us/articles/360045544271-How-can-I-get-my-3rd-party-plug-ins-to-show-up-in-Studio-One),
[How to Rescan Plugins in Studio One — Slate Digital](https://support.slatedigital.com/hc/en-us/articles/115006153428-How-to-Rescan-Plugins-in-Studio-One),
[Studio One: Feature Requests, Drawbacks and Cons — Gearspace](https://gearspace.com/board/presonus-studio-one/1358936-studio-one-feature-requests-drawbacks-cons.html).

## Why this fits

Identical underlying data source to Portability Check —
`StudioOneSongAnalyzer.Analyze()`'s `Plugins` list — just run across every
`.song` in a folder tree instead of the one you have installed. This is the
simplest of all five proposals: no new parsing, no new "what's installed on
this machine" logic, just a fan-out over an existing method plus an inverted
index (plugin → songs, instead of song → plugins).

## Proposed shape

```
Core/Contracts/IPluginUsageFinder.cs
    PluginUsageReport FindSongsUsingPlugin(string libraryRootPath, string pluginNameQuery);
    IReadOnlyDictionary<string, IReadOnlyList<string>> BuildPluginIndex(string libraryRootPath);  // plugin name -> song folder paths, for browsing the full inventory

Core/Models/PluginUsageReport.cs
    sealed class PluginUsageReport
    {
        required string PluginNameQuery { get; init; }
        required IReadOnlyList<PluginUsageMatch> Matches { get; init; }
    }

Core/Models/PluginUsageMatch.cs
    sealed class PluginUsageMatch
    {
        required string SongFolderPath { get; init; }
        required string SongName { get; init; }
        required string MatchedPluginDisplayName { get; init; }   // the exact name found, since fuzzy match may differ slightly from the query
    }
```

## UX flow

1. Select a library root folder (same folder-tree walk Song Backup already
   does to enumerate song subfolders containing `.song` files).
2. Two modes:
   - **Search**: type or pick a plugin name, get every song that references
     it. Best for "I'm about to uninstall FabFilter Pro-Q 3, what breaks?"
   - **Browse**: build the full inverted index once (plugin → songs) and
     present it as a sortable/filterable table — "show me every plugin used
     anywhere in my library, and how many songs each touches." Useful on its
     own as a "what do I actually use" audit, independent of any specific
     uninstall plan.
3. Click a matched song to jump straight to Song Archiver or Path Fixer for
   that folder (mirrors the existing "Send To Archiver" right-click already
   in Folder Sweeper).
4. Purely read-only — no confirmation dialog, no destructive path.

## Performance note

Scanning a large library (hundreds of songs) means opening hundreds of ZIPs
and parsing XML in each. `Analyze()` today does a fair amount of work beyond
just plugin extraction (media file diffing, channel mapping) that this tool
doesn't need. Worth a lighter-weight
`StudioOneSongAnalyzer.GetPluginsOnly(songFilePath)` fast path that only opens
`song.xml` + `mediapool.xml` + `Devices/audiomixer.xml` and skips the media
pool / disk cross-referencing, so building the full library index doesn't
mean paying the cost of a full `Analyze()` per song. Same applies to
[Portability Check](portability-check.md) if it's ever run in a "check my
whole library" mode rather than one song at a time.

## Open questions

- Same `Plugins: string` vs. `SongPlugin` question as Portability Check —
  matching a user's typed query against a plain display string is more
  forgiving for search (substring match is fine for "search," less fine for
  Portability Check's exact-match "is this installed" comparison), so this
  proposal is actually less blocked by that decision than Portability Check
  is. Can ship against the current `string` list if needed.
- Index caching: for a library that doesn't change often, is it worth
  persisting the built index (e.g. alongside `settings.json`) so repeat
  searches don't re-scan every ZIP? Not needed for v1 — only worth it if
  real-world library sizes make a fresh scan noticeably slow.
