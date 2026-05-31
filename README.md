# Studio Pro Tools

A comprehensive utility suite for managing and archiving **PreSonus Studio One** music production projects.

## Overview

**Studio Pro Tools** provides six integrated applications to streamline your Studio One workflow:

1. **Song Archiver** – Analyse, inspect, and create optimised ZIP archives of your Studio One songs with detailed HTML documentation
2. **Un-Archiver** – Restore a song from an archive ZIP and automatically fix its internal file paths
3. **Folder Sweeper** – Identify and safely delete orphaned Studio One cache and temporary folders
4. **Song Backup** – Synchronise song folders to a backup location, copying only new or changed files
5. **Song ReNamer** – Rename the entire song package — folder, `.song` files, and Mixdown/Master audio — in one step
6. **Path Fixer** – Detect and correct broken internal file paths in a `.song` file after it has been moved or restored

---

## Features

### Song Archiver

- **Complete Song Analysis**
  - Identifies all referenced audio media files (used vs. unused)
  - Detects missing referenced WAV files with warnings
  - Lists all plugins/instruments used in the song
  - Associates plugins with their respective channels/tracks
  - Auto-deduplicates versioned plugin names (e.g., "StudioRack Stereo 4" → "StudioRack Stereo")
  - Discovers preview files (Mixdown/Master audio)

- **Smart Media Management**
  - Optional inline playback of WAV files with visual progress tracking
  - Choose to include or exclude unused media when archiving
  - Optional Mixdown/Master file inclusion in archive

- **Archive Generation**
  - Creates a single ZIP file with only the media files your song actually uses
  - Generates comprehensive HTML report with song metadata and media inventory
  - Optional embedded XML debug files for technical inspection

- **Post-Archive Options** (via completion dialog)
  - Remove song from Studio One's Recent Documents list (default: on)
  - Delete the original song folder after archiving
  - Open the archive folder in Explorer

### Un-Archiver

- **One-Click Restore**
  - Select any ZIP archive created by Song Archiver
  - Choose a destination folder (defaults to your configured Default Song Folder)
  - Extracts into a new subfolder named after the archive file

- **Automatic Path Fixing**
  - Immediately runs Path Fixer on the restored `.song` file
  - Updates all internal `file://` references from the original location to the new one
  - Studio One can open the song and find all media files without manual relinking

- **Result Summary**
  - Reports how many internal paths were corrected
  - Offers to open the extracted folder in Windows Explorer

### Folder Sweeper

- **Automated Scanning**
  - Pre-populates with your Default Song Folder and scans immediately on startup
  - Identifies "junk" Studio One cache, temporary, and orphaned folders
  - Categorises flagged folders by reason (e.g., "No .song file", "Not modified in over a year")

- **Safe Deletion**
  - Checkbox list with full path and reason for each flagged folder
  - Select individual folders or use "Select All" / "Deselect All"
  - Confirmation dialog before anything is removed
  - **Remove from Recent Documents** checkbox (default: on) — cleans up Studio One's recent file list for any deleted songs automatically

- **Seamless Integration**
  - Right-click any row to **Open in Explorer** or **Send To Archiver**

### Song Backup

- **Smart Synchronisation**
  - Scans a root folder and lists all song subfolders containing `.song` files
  - Copies only files that are new or have been modified since the last backup
  - Skips files already up to date — fast incremental runs after the first

- **Selective Backup**
  - Checkbox list to include or exclude individual song folders
  - **Select All** / **Deselect All** for bulk selection
  - Songs folder and backup destination remembered between sessions

- **Backup Confirmation Dialog**
  - Preview which songs will be backed up and how many files each requires
  - Toggle **"Include unused audio takes from tracks"** — file counts update live

### Song ReNamer

- **Full Package Rename**
  - Renames the song folder, all `.song` files that match the old name, and any Mixdown/Master audio files that start with the old name
  - Updates internal file references inside `.song` archives automatically

- **Default Name Detection**
  - Detects Studio One's default `Username_YYYY-MM-DD` naming pattern and flags it with a badge
  - Smart name suggestions from `.song` files found inside the folder

- **Preview Playback**
  - Listen to the latest Mixdown or Master audio before renaming to confirm you have the right song

### Path Fixer

- **Path Mismatch Detection**
  - Reads the path stored inside the `.song` file (from `Song/mediapool.xml`) and compares it to the file's current location
  - Displays both the stored path and the current path side-by-side
  - Shows how many internal references need updating

- **In-Place Repair**
  - Rewrites all `file://` URLs across every XML file inside the `.song` ZIP in a single pass
  - Handles songs moved between drives, users, or computers
  - Leaves all other file content untouched

- **Safe Operation**
  - Shows a warning to back up before proceeding
  - Uses a temporary file during rewrite — original is only replaced on success
  - Re-analyses the file after fixing so the result is immediately confirmed

---

## Installation & Usage

### Running the Application

1. **Download** `StudioOneTools.exe` from the `publish/` folder
2. **Run** the executable — no installation required (portable)
3. Choose your tool from the home screen

### Song Archiver Workflow

1. **Select Song Folder** — click Browse or paste the path; analysis starts automatically
2. **Review Analysis** — inspect used/unused WAV files, plugins, and any issues
3. **Configure Archive** — choose Retain Mixdown / Retain Master files, set archive destination
4. **Create Archive** — optionally include unused media, remove from recent docs, delete original, open folder

### Un-Archiver Workflow

1. **Select Archive** — click Browse and choose a `.zip` file created by Song Archiver
2. **Select Destination** — choose the folder where the song will be extracted (defaults to Default Song Folder)
3. **Review Preview** — the "Will extract to" field shows the exact folder that will be created
4. **Extract & Fix Paths** — extracts the ZIP, then automatically fixes all internal paths
5. **Open Folder** — optionally open the extracted song folder in Explorer

### Folder Sweeper Workflow

1. **Select Root Folder** — pre-populated from settings; scanning starts automatically on open
2. **Review Flagged Folders** — each row shows folder name, reason, and full path
3. **Select & Delete** — check folders to remove, optionally keep "Remove from Recent Documents" on
4. **Confirm** — deletion prompt shows count before anything is removed

### Song Backup Workflow

1. **Select Songs Folder** — lists all subfolders containing `.song` files automatically
2. **Select Songs to Back Up** — check/uncheck individual songs
3. **Choose Backup Destination** — remembered between sessions
4. **Click Backup** — review file counts in the confirmation dialog, then start

### Song ReNamer Workflow

1. **Open Song Folder** — analysis runs automatically
2. **Review Analysis** — check for default name badge and suggestion chips
3. **Optionally Preview** — play the Mixdown or Master to confirm the right song is open
4. **Choose New Name** — click a suggestion chip or type directly
5. **Click Rename** — all files and internal references are updated in one step

### Folder Sweeper Workflow

1. **Select Root Folder** — click Browse; scanning starts automatically
2. **Review Flagged Folders** — folders listed with reason and full path
3. **Delete or Navigate** — right-click to Open in Explorer or Send To Archiver
4. **Confirm & Clean** — confirmation dialog before deletion

### Path Fixer Workflow

1. **Select a .song File** — click Browse; analysis runs automatically
2. **Review Paths** — stored path vs current path displayed side-by-side
3. **Click Fix Paths** — confirm the warning, then all internal references are updated
4. **Verify** — the panel re-analyses and confirms paths are now correct

---

## Settings

Access **Settings** (⚙ icon in Song Archiver) to configure:

| Setting | Description | Used by |
|---|---|---|
| **Default Song Folder** | Pre-populates song folder fields and auto-scans on open | Archiver, Sweeper, Un-Archiver (destination) |
| **Default Archive Folder** | Default destination for new archives | Song Archiver |
| **Default Backup Folder** | Default backup destination | Song Backup |
| **Default Rename Folder** | Remembered after each rename session | Song ReNamer |
| **Debug Mode** | Include XML schema reports in archives | Song Archiver |

Settings are stored in `%APPDATA%\StudioOneTools\settings.json`.

---

## System Requirements

- **Windows 10 or later** (64-bit)
- **.NET 10 Runtime** (bundled in the single executable)
- **PreSonus Studio One** (any recent version with `.song` file format)

---

## Architecture

- Built on **.NET 10** with **C# 14** and **WPF**
- **Multi-layered design:**
  - **Core** – Domain models and interfaces
  - **StudioOne** – Studio One file format parsing, analysis, and path handling
  - **App** – User interface and orchestration

---

## Known Limitations

- Only supports `.song` format (PreSonus Studio One files)
- Media file playback is audio-only (no video support)
- Song Backup does not delete files removed from the source — it is additive only
- Song ReNamer only renames Mixdown/Master audio files at the top level of those subfolders
- Path Fixer rewrites paths based on the `documentPath` stored in `mediapool.xml` — if that entry is missing or malformed, the tool will report an error

---

## Troubleshooting

**"No .song files found in the selected folder"**
Ensure you selected the song folder directly — the one containing the `.song` file — not a parent directory.

**"Referenced media file is missing"**
One or more WAV files referenced in the song do not exist on disk. Locate and relink them in Studio One, then re-analyse.

**"Could not find the stored song path (documentPath)"**
The `mediapool.xml` inside the `.song` file does not contain a `documentPath` entry. This can happen with very old or manually edited song files. Use Studio One to open and re-save the file, then try again.

**"A folder named X already exists at the destination" (Un-Archiver)**
The Un-Archiver will not overwrite an existing folder. Rename or move the existing folder, or choose a different destination.

**Path Fixer shows "Paths are correct — no fix needed" but Studio One still reports missing files**
The internal paths match the current location, but the media files themselves may have been moved or deleted. Check that the `Media/` subfolder is present and contains the expected WAV files.

**All WAV files show as "not used" in the Archiver**
This typically happens when the song has been moved from its original location. The media pool stores absolute paths at save time. Use Path Fixer to update the internal paths, then re-analyse in the Archiver.

**Song Backup shows 0 files to copy**
All files are already up to date in the backup destination. If you expected changes, confirm you are pointing at the correct backup folder.

**Folder Sweeper flags folders I want to keep**
Leave those checkboxes unchecked before clicking Delete. Nothing is removed without explicit selection and confirmation.

---

**Version:** 1.3
**Built on:** .NET 10 | WPF | C# 14
**Last Updated:** 2026
