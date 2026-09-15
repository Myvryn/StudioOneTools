# Changelog

Changes to **Tools for Studio One** — a utility suite for managing and
archiving PreSonus Studio One songs (Song Archiver, Un-Archiver, Folder
Sweeper, Song Backup, Song ReNamer, Path Fixer).

Versioning is semantic. Dates are the release date; earlier detail is in the
git history.

---

## 1.4.0 — 2026-09-15 (macOS only)

First real macOS release. Windows stays on 1.3.2 — nothing changed there
this round.

- Song Archiver and Song ReNamer, the last two tools missing from the
  macOS (Avalonia) build, are now fully implemented: same folder analysis,
  missing-media detection, media-file grid, and WAV preview as Windows.
  All six tools are now available on both platforms.
- New cross-platform audio preview for WAV playback (Windows: hidden
  PowerShell `SoundPlayer`; macOS: `afplay`), replacing WPF's Windows-only
  `MediaPlayer`.
- Fixed: every `DataGrid` in the macOS build (Sweeper, Song Archiver, the
  multi-song picker) was rendering with no visible rows or headers — the
  DataGrid control's theme was never registered in the app.
- Redrew the Home screen's tool icons to match the polished, filled glyph
  style already used on Windows, replacing thin placeholder line icons
  (including two that had been silently duplicated between tools).
- Signed and notarized with the same Developer ID used for the plugins and
  The Installer.

## 1.3.2 — 2026-09-11

No functional app changes. Packaging only:

- Switched to a framework-dependent publish (needs the .NET 10 Desktop
  Runtime, fetched or bundled by the installer) — much smaller download than
  the old self-contained exe.
- Replaced the raw exe download with a real signed Inno Setup installer:
  Start Menu + optional desktop shortcut, proper Add/Remove Programs entry,
  and Web / Offline flavours depending on whether the target machine has
  internet during install.
- Installer now detects a prior install and offers Update/Reinstall or
  Uninstall, instead of always assuming an update.

## 1.3.1 — 2026-05-31

- **Path Fixer:** fixed XML corruption in `SongPathFixer` when folder names
  contained special characters.
- Added a standalone repair script for `.song` files corrupted by the
  pre-1.3.1 Path Fixer bug.

## 1.3.0 — 2026-05-31

- Added **Path Fixer** — detect and correct broken internal file paths in a
  `.song` after it has been moved or restored.
- Added **Un-Archiver** — restore a song from an archive ZIP and fix its
  internal paths automatically.
- Recent-documents cleanup; assorted bug fixes.

## 1.2.0 — 2026-05-26

- Added **Song Backup** — sync a song folder to a backup location, copying only
  new or changed files.
- Added **Song ReNamer** — rename the whole song package (folder, `.song`
  files, Mixdown/Master audio) in one step.
- In-app help system; home-screen updates.

## 1.x — 2026-03

- Initial suite: **Song Archiver** (optimised ZIP archives with HTML
  documentation, optional inclusion of unused media and Mixdown/Master) and
  **Folder Sweeper** (find and safely delete orphaned cache/temp folders).
- Improved media-path resolution and archive-request logic.

## Unreleased

- Licence changed to PolyForm Noncommercial 1.0.0.
- Six Walls rebrand: app icon, corrected assembly identity, branding assets.
- macOS CI pointed at `trunk`.
