# Changelog

Changes to **Tools for Studio One** — a utility suite for managing and
archiving PreSonus Studio One songs (Song Archiver, Un-Archiver, Folder
Sweeper, Song Backup, Song ReNamer, Path Fixer).

Versioning is semantic. Dates are the release date; earlier detail is in the
git history.

---

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
