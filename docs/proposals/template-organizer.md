# Proposal: Template Organizer

**Priority:** Lowest of the five — narrower audience, smaller pain, and it's
the one proposal that touches Studio One's *own* template list rather than
song files, which means more unknowns about the on-disk format.
**Effort estimate:** M — mostly UI (drag-to-reorder, categorize); the
song-parsing side is minimal since templates are just `.song` files in a
specific folder.

## Problem

A specific, recurring complaint rather than a broad one: users with a long
list of Studio One templates (the "User" template category) have no way to
reorder, categorize, or archive old ones without risking breaking songs that
were originally created from those templates.

Source: [User Template Management — Studio One & Fender Studio Pro User Forum](https://studiooneforum.com/threads/user-template-management.1743/) —
a user explicitly asks whether moving templates into an "archived" subfolder
would clean up the list without interfering with opening old projects
created from them, and separately wishes for click-and-drag reordering.

## Why this fits (with a caveat)

Studio One templates live as `.song`-like files in a known user-data
location (`%APPDATA%\PreSonus\Studio One\...\Templates`, needs confirming per
version). The suite's existing `.song` ZIP-parsing code applies the same way
it does to any other song — the difference is this tool operates on Studio
One's own template folder rather than a user-chosen song library. That's the
one piece of new ground: Song ReNamer/Backup/Sweeper all take a
user-specified root folder as input; this is the first tool that would need
to *locate* Studio One's own data folder rather than being pointed at one.

## Proposed shape

```
Core/Contracts/ITemplateOrganizer.cs
    IReadOnlyList<TemplateInfo> ListTemplates(string templatesRootPath);
    void SetCategory(string templateFilePath, string category);       // metadata only, doesn't move the file
    void SetSortOrder(IReadOnlyList<string> orderedTemplateFilePaths); // persisted alongside, doesn't touch Studio One's own list

Core/Models/TemplateInfo.cs
    sealed class TemplateInfo
    {
        required string FilePath { get; init; }
        required string DisplayName { get; init; }
        required DateTime LastModified { get; init; }
        string? Category { get; init; }        // user-assigned, stored in a sidecar file this app owns
        int SortOrder { get; init; }
    }
```

## Key design decision: don't touch Studio One's own template folder structure

The forum thread's own concern is the right one to design around: moving
files into subfolders (or renaming them) inside Studio One's real Templates
folder risks Studio One no longer recognizing them as templates, and risks
breaking the association old songs have with "created from template X" (if
Studio One tracks that at all — needs verifying).

Safer design: **read** the real template folder (list, don't modify), and
store all organization metadata (category, sort order, "archived" flag) in a
sidecar file this app owns (e.g.
`%APPDATA%\StudioOneTools\template-organizer.json`), the same way
`settings.json` already persists app-owned state today. The app becomes a
*view* over Studio One's template folder with its own organizational layer on
top — it never renames or moves the actual template files, so there's zero
risk of breaking Studio One's own template recognition or any song's
provenance link back to its template.

"Archived" in this design means "hidden in this app's view," not moved on
disk — sidestepping the exact risk the original forum poster was worried
about, without needing to verify how Studio One internally tracks template
associations.

## UX flow

1. Auto-locate (or let the user browse to) Studio One's Templates folder.
2. Flat list becomes a categorized, drag-reorderable list — category assignment
   and ordering are both this app's metadata, applied as a view/filter, never
   written back into Studio One's own folder.
3. No confirmation dialogs needed anywhere in this tool — nothing it does is
   destructive or touches the real template files at all.

## Open questions

- Confirm the actual on-disk Templates path and file format across Studio One
  versions before starting — this is the one unknown that could change scope.
- Does Studio One itself read subfolders within Templates as categories
  already (some DAWs do)? If so, "category" here should mirror that existing
  mechanism rather than invent a parallel one the user has to maintain in two
  places.
- Lowest priority of the five for a reason: worth revisiting only after the
  higher-value proposals ship, or if you personally hit this exact annoyance
  with your own template list.
