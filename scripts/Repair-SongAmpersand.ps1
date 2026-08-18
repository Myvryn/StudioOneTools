<#
.SYNOPSIS
    Repairs a Studio One .song file left non-well-formed by the pre-1.3.1
    StudioOneTools Path Fixer bug.

.DESCRIPTION
    Before v1.3.1, Path Fixer spliced the corrected folder path into each XML
    file inside a .song archive without XML-escaping it. If that folder name
    contained a literal '&' (e.g. "The Hungry & the Cold"), the result was a
    bare, unescaped ampersand — which is not well-formed XML — and Studio One
    would report the file as corrupted.

    Because that rewrite leaves the file's stored path already matching its
    current location, simply re-running Path Fixer (even the fixed version)
    does not detect or repair it — there's no path mismatch left to trigger a
    rewrite. This script instead scans every XML entry inside the .song ZIP
    directly for bare ampersands and re-escapes them as &amp;, leaving
    everything else untouched.

    Safe to run against a file that doesn't need it: it reports "nothing to
    repair" and makes no changes.

.PARAMETER SongFilePath
    Path to the .song file to repair.

.EXAMPLE
    .\Repair-SongAmpersand.ps1 -SongFilePath "C:\Song Files\The Hungry & the Cold\The Hungry & the Cold\The Hungry and the Cold.song"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [string]$SongFilePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$SongFilePath = (Resolve-Path -LiteralPath $SongFilePath).Path
$backupPath   = "$SongFilePath.bak"
$tempPath     = "$SongFilePath.repairtmp"

if (Test-Path -LiteralPath $tempPath) {
    Remove-Item -LiteralPath $tempPath -Force
}

# A '&' that is not already the start of a valid XML entity/character reference.
$badAmpersand = [regex]::new('&(?!amp;|lt;|gt;|quot;|apos;|#\d+;|#x[0-9A-Fa-f]+;)')

$fixedEntries = 0
$fixedTotal   = 0

$src  = [System.IO.Compression.ZipFile]::OpenRead($SongFilePath)
$dest = [System.IO.Compression.ZipFile]::Open($tempPath, [System.IO.Compression.ZipArchiveMode]::Create)

try {
    foreach ($entry in $src.Entries) {
        $destEntry = $dest.CreateEntry($entry.FullName, [System.IO.Compression.CompressionLevel]::Optimal)
        $destEntry.LastWriteTime = $entry.LastWriteTime

        $srcStream  = $entry.Open()
        $destStream = $destEntry.Open()

        if ($entry.FullName.EndsWith('.xml', [System.StringComparison]::OrdinalIgnoreCase)) {
            $reader  = New-Object System.IO.StreamReader($srcStream, [System.Text.Encoding]::UTF8)
            $content = $reader.ReadToEnd()
            $reader.Close()

            $matchCount = $badAmpersand.Matches($content).Count
            if ($matchCount -gt 0) {
                $content = $badAmpersand.Replace($content, '&amp;')
                $fixedEntries++
                $fixedTotal += $matchCount
                Write-Host ("  fixed {0,5} bad '&'  ->  {1}" -f $matchCount, $entry.FullName)
            }

            $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
            $bytes = $utf8NoBom.GetBytes($content)
            $destStream.Write($bytes, 0, $bytes.Length)
        }
        else {
            $srcStream.CopyTo($destStream)
        }

        $destStream.Close()
        $srcStream.Close()
    }
}
finally {
    $dest.Dispose()
    $src.Dispose()
}

if ($fixedTotal -eq 0) {
    Write-Host "No unescaped '&' found in any XML entry -- nothing to repair."
    Remove-Item -LiteralPath $tempPath -Force
    return
}

Copy-Item -LiteralPath $SongFilePath -Destination $backupPath -Force
Remove-Item -LiteralPath $SongFilePath -Force
Move-Item -LiteralPath $tempPath -Destination $SongFilePath

Write-Host ""
Write-Host "Repaired $fixedEntries XML file(s), $fixedTotal ampersand(s) escaped."
Write-Host "Original backed up to: $backupPath"
Write-Host "Repaired file: $SongFilePath"
