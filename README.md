# Studio Pro Tools

A comprehensive utility suite for managing and archiving **PreSonus Studio One** music production projects.

## Overview

**Studio Pro Tools** provides four integrated applications to streamline your Studio One workflow:

1. **Song Archiver** – Analyze, inspect, and create optimized ZIP archives of your Studio One songs with detailed HTML documentation
2. **Folder Sweeper** – Identify and safely delete orphaned Studio One cache and temporary folders
3. **Song Backup** – Synchronize song folders to a backup location, copying only new or changed files
4. **Song ReNamer** – Rename the entire song package — folder, `.song` files, and Mixdown/Master audio — in one step

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
  - Automatic archive folder opening in Windows Explorer

- **Debugging & Analysis**
  - Configurable Debug Mode (Settings) to include XML schema discovery reports
  - Identifies plugin vendor and name information
  - Channel-to-plugin mapping from audio mixer configuration

### Song Backup

- **Smart Synchronization**
  - Scans a root folder and lists all song subfolders containing `.song` files
  - Copies only files that are new or have been modified since the last backup
  - Skips files already up to date — fast incremental runs after the first backup

- **Selective Backup**
  - Checkbox list to include or exclude individual song folders
  - **Select All** / **Deselect All** for bulk selection
  - Both the songs folder and backup destination are remembered between sessions

- **Backup Confirmation Dialog**
  - Preview which songs will be backed up and how many files each requires
  - Toggle **"Include unused audio takes from tracks"** — file counts update live
  - Cancel to return without backing up, or confirm to start

- **Audio Take Control**
  - By default all files are included (safest option)
  - Uncheck to skip WAV files that are recorded but not used in the song — saves space in the backup

### Song ReNamer

- **Full Package Rename**
  - Renames the song folder, all `.song` files that match the old name, and any Mixdown/Master audio files that start with the old name
  - Updates internal file references inside `.song` archives automatically — no manual editing required

- **Default Name Detection**
  - Detects Studio One's default `Username_YYYY-MM-DD` naming pattern and flags it with a "Default name" badge
  - Prompts you to choose a real name when a default is detected

- **Smart Name Suggestions**
  - Scans the song folder for `.song` files with non-default names and surfaces them as clickable suggestion chips
  - One click fills in the new name field — useful when the song was saved under a descriptive name at some point

- **Preview Playback**
  - If a Mixdown or Master audio file exists, a **Play** button lets you listen before renaming — handy for confirming you have the right song open

### Folder Sweeper

- **Automated Scanning**
  - Instantly scans folders as you select them—no button clicks needed
  - Identifies "junk" Studio One cache, temporary, and orphaned folders
  - Categorizes flagged folders by reason (e.g., "Empty", "Cache Only")

- **Safe Deletion**
  - Detailed folder list with path and reason for flagging
  - Select individual folders or use "Select All"
  - Preview each folder's path before deletion
  - Confirmation dialog with count display—prevents accidental deletion

- **Seamless Integration**
  - Right-click context menu on flagged folders:
    - **Open in Explorer** – browse folder contents
    - **Send To Archiver** – immediately preload selected folder in Song Archiver and auto-analyze

## Installation & Usage

### Running the Application

1. **Download** `StudioOneTools.exe` from the `publish/` folder
2. **Run** the executable—no installation required (portable)
3. Choose your tool from the home screen:
   - **Song Archiver** for archiving and analyzing songs
   - **Folder Sweeper** for cleaning up orphaned folders
   - **Song Backup** for syncing songs to a backup location
   - **Song ReNamer** for renaming a complete song package

### Song Archiver Workflow

1. **Select Song Folder**
   - Click **Browse** or paste the path to your Studio One song folder
   - Analysis starts automatically (with a 500ms debounce while typing)

2. **Review Analysis**
   - View song name, file paths, and issue summary
   - Preview used/unused media file counts
   - Optional: Play individual WAV files with visual progress meter
   - Optional: Listen to preview files (Master or Mixdown)

3. **Configure Archive**
   - Choose **Retain Mixdown files** / **Retain Master files** (optional)
   - Select **Archive destination** via "Save As" button

4. **Create Archive**
   - Click **Create Archive**
   - Optional: Include unused media files (default: exclude)
   - Optional: Delete original song folder after archiving
   - Optional: Open archive folder in Explorer

5. **Review Report**
   - Open the generated `Song_Information.html` from the ZIP to view complete metadata

### Song Backup Workflow

1. **Select Songs Folder**
   - Click **Browse** next to "Songs folder" or paste a path
   - All subfolders containing `.song` files are listed automatically

2. **Select Songs to Back Up**
   - Check individual songs or use **Select All**
   - All songs are selected by default when the folder loads

3. **Choose Backup Destination**
   - Click **Browse** next to "Backup to" and choose (or create) a destination folder
   - The destination is remembered for next time

4. **Click Backup**
   - A summary dialog shows each song and the number of files to copy
   - Toggle **"Include unused audio takes from tracks"** — counts update in real time
     - **Checked (default):** all files are copied, including recorded takes not used in the mix
     - **Unchecked:** unused WAV files are skipped, reducing backup size
   - Click **Cancel** to go back, or **Backup** to start

5. **Done**
   - Only new and modified files are copied — unchanged files are skipped
   - A completion message shows the total files copied and any errors

### Song ReNamer Workflow

1. **Open a song folder**
   - Click **Browse** and select the Studio One song folder you want to rename
   - The tool analyses the folder instantly: it reads the current name, checks for a default name pattern, and scans for name suggestions

2. **Review the analysis**
   - **Current name** is displayed at the top of the panel
   - If the name matches Studio One's default `Username_YYYY-MM-DD` pattern, a **"Default name"** badge appears
   - Any `.song` files in the folder with non-default names appear as clickable **suggestion chips**

3. **Optionally preview the audio**
   - If a Mixdown or Master audio file exists, click **Play Mixdown** (or **Play Master**) to confirm this is the right song before renaming

4. **Choose the new name**
   - Click a suggestion chip to fill in the name field automatically, or type directly in the **New name** field

5. **Click Rename**
   - Confirm in the dialog — the tool then:
     - Updates internal file path references inside each `.song` archive
     - Renames `.song` files that match the old folder name
     - Renames Mixdown/Master audio files that start with the old name
     - Renames the folder itself

6. **Done**
   - A confirmation shows how many files were renamed
   - The panel re-analyses the renamed folder so it is ready for another rename if needed

### Folder Sweeper Workflow

1. **Select Root Folder**
   - Click **Browse** to select a folder containing Studio One projects
   - Scanning starts automatically

2. **Review Flagged Folders**
   - Folders are listed with reason and full path
   - Use checkboxes to select folders for deletion

3. **Delete or Navigate**
   - Right-click any row:
     - **Open in Explorer** → Browse folder contents
     - **Send To Archiver** → Preload folder and auto-analyze in Song Archiver
   - Or use **Select All** / **Deselect All** and click **Delete**

4. **Confirm & Clean**
   - Confirm deletion with folder count
   - Deleted folders are immediately removed from the list

## Settings

Access **Settings** (⚙ icon in Song Archiver) to configure:

- **Default Song Folder** – Auto-populate the song folder field
- **Default Archive Folder** – Where archives are saved by default
- **Debug Mode** – Enable to include XML schema reports in archives (useful for troubleshooting)

## System Requirements

- **Windows 10 or later** (64-bit)
- **.NET 10 Runtime** (bundled in the single executable)
- **PreSonus Studio One** (any recent version with .song file format)

## Audio File Playback

- Audio playback uses **Windows Media Player** (system default)
- Progress meter fills with blue as the file plays
- Single player instance (starting a new file stops the previous one)

## Architecture

- Built on **.NET 10** with **C# 14** and **WPF**
- **Multi-layered design** with clear separation:
  - **Core** – Domain models and interfaces
  - **StudioOne** – Studio One file format parsing and analysis
  - **App** – User interface and orchestration

## Known Limitations

- Only supports `.song` format (PreSonus Studio One files)
- Media file playback is audio-only (no video support)
- Sweeper scans only identify common cache/temp patterns
- Archive XML embedding is optional to keep file size down
- Song Backup does not delete files removed from the source — it is additive only
- Song ReNamer only renames Mixdown/Master audio files at the top level of those subfolders; deeply nested files with the old name in other locations are not affected

## Troubleshooting

### "No .song files found in the selected folder"
- Ensure you've selected a Studio One song folder (not a parent directory)
- The folder should directly contain `.song` files

### "Referenced media file is missing"
- Some WAV files referenced in the song do not exist on disk
- Fix: Locate and add the missing files to the Media folder, or relink them in Studio One

### Analysis seems slow
- First scan of large song folders may take 10–20 seconds
- Subsequent scans are faster due to OS caching

### Archive is large despite "exclude unused media"
- If "Use Count > 0" in mediapool.xml, the file is considered used
- This is safer than excluding files that *might* be referenced

## Contributing & Feedback

This tool is designed for music producers and engineers using PreSonus Studio One. Feedback and suggestions are welcome!

## License

This tool is provided as-is for Studio One users. Please ensure you have backups before using the Folder Sweeper deletion feature.

---

**Version:** 1.2  
**Built on:** .NET 10 | WPF | C# 14  
**Last Updated:** 2026
