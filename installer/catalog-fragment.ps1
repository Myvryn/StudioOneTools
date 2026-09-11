<#
    New-CatalogFragment -- emits one catalog fragment for sixwalls.net.

    Dot-source this, then call it once per component at the end of a build,
    after signing. See docs/catalog-schema.md in the sixwalls.net repo for the
    schema; this writes the "fragment" shape described there.

        . (Join-Path $here 'catalog-fragment.ps1')
        New-CatalogFragment -Product 'the-room' -Component 'plugin' `
            -Version $ver -OutDir $dist -Artifacts @(
                @{ Os='win'; Variant='minimal'; File=$minimalExe }
            )

    bytes and sha256 are read off the finished file, and `signed` is read from
    the file's own Authenticode status -- not from whether signing was asked
    for. A build that silently produced unsigned installers therefore says so
    in the catalog rather than inheriting an optimistic flag.

    A file that does not exist is a hard error: a fragment naming a file the
    site cannot serve is exactly the dead link the catalog exists to prevent.
#>

function New-CatalogFragment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string]   $Product,
        [Parameter(Mandatory)] [string]   $Component,
        [Parameter(Mandatory)] [string]   $Version,
        [Parameter(Mandatory)] [string]   $OutDir,
        [Parameter(Mandatory)] [hashtable[]] $Artifacts,

        # none | versioned | breaking -- see the schema. Defaults to the
        # honest answer for a packaging-only rebuild.
        [ValidateSet('none', 'versioned', 'breaking')]
        [string] $SoundChange = 'none',

        [string] $Date,

        # When given, the fragment is also dropped straight into the site's
        # catalog.d/ so it does not have to be copied by hand.
        [string] $CatalogDir
    )

    if (-not $Date) { $Date = (Get-Date).ToString('yyyy-MM-dd') }

    $entries = @()
    foreach ($a in $Artifacts) {
        $path = $a.File
        if (-not (Test-Path -LiteralPath $path)) {
            throw "catalog fragment: $Product/$Component $Version names a file that does not exist: $path"
        }
        $item = Get-Item -LiteralPath $path

        $isSigned = $false
        try {
            $sig = Get-AuthenticodeSignature -LiteralPath $path
            $isSigned = ($sig -and $sig.Status -eq 'Valid')
        } catch {
            $isSigned = $false      # unsignable container (.zip): not an error
        }

        $entries += [ordered]@{
            os      = $a.Os
            variant = $a.Variant
            file    = 'Downloads/' + $item.Name
            bytes   = [int64] $item.Length
            sha256  = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
            signed  = $isSigned
        }
    }

    $fragment = [ordered]@{
        schemaVersion = 1
        product       = $Product
        component     = $Component
        version       = $Version
        date          = $Date
        soundChange   = $SoundChange
        artifacts     = @($entries)
    }

    $name = "$Product-$Component-$Version.json"
    $json = $fragment | ConvertTo-Json -Depth 6

    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
    $local = Join-Path $OutDir $name
    $json | Set-Content -LiteralPath $local -Encoding UTF8
    Write-Host "catalog   -> $local" -ForegroundColor Green

    if ($CatalogDir) {
        New-Item -ItemType Directory -Force -Path $CatalogDir | Out-Null
        $dest = Join-Path $CatalogDir $name
        $json | Set-Content -LiteralPath $dest -Encoding UTF8
        Write-Host "catalog   -> $dest" -ForegroundColor Green
    }

    $unsigned = @($entries | Where-Object { -not $_.signed -and $_.file -notlike '*.zip' })
    if ($unsigned.Count) {
        Write-Host ("  note: {0} unsigned non-zip artifact(s) recorded as signed=false" -f $unsigned.Count) -ForegroundColor DarkYellow
    }
}
