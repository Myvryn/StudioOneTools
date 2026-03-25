# Studio One Tools

A comprehensive utility suite for managing and archiving **PreSonus Studio One** music production projects.

## Overview

**Studio One Tools** provides two integrated applications to streamline your Studio One workflow:

1. **Song Archiver** – Analyze, inspect, and create optimized ZIP archives of your Studio One songs with detailed HTML documentation
2. **Folder Sweeper** – Identify and safely delete orphaned Studio One cache and temporary folders

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

**This tool is provided as-is for Studio One users. Please ensure you have backups before using the Folder Sweeper deletion feature.**

© 2026 Edwin Steven Jones. All rights reserved.
Unauthorized copying of this file, via any medium, is strictly prohibited.
---

**Version:** 1.0  
**Built on:** .NET 10 | WPF | C# 14  
**Last Updated:** 2025
