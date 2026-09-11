<#
    Builds Tools for Studio One (framework-dependent, win-x64), then produces
    the distributables in installer/dist/:

      Tools-for-Studio-One-<ver>-Windows-Web.exe       small; fetches the
                                                        .NET Desktop Runtime
                                                        at install time if
                                                        missing (default)
      Tools-for-Studio-One-<ver>-Windows-Offline.exe   bundles the runtime
                                                        installer; no internet
                                                        needed at install time

    The site's "Get It!" picker maps onto these two ("will this machine have
    internet during install?", which a user can actually answer).

    Both are one script (StudioOneTools.iss) compiled twice with /DFlavor=.
    The Offline flavour needs the runtime installer cached at
    installer/vendor/windowsdesktop-runtime-win-x64.exe (gitignored) --
    downloaded here on first use, reused after that.

    The .exe installers need Inno Setup 6 (ISCC). If it is not found, the
    script stops after the framework-dependent publish.
#>
[CmdletBinding()]
param(
    [string] $Config = 'Release',
    [switch] $NoSign,           # force an unsigned build
    [switch] $SkipOffline,      # skip the Offline flavour (and its ~55MB fetch)
    [string] $CatalogDir        # sixwalls.net\catalog.d -- drop the fragment there too
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Resolve-Path (Join-Path $here '..')
$appProj = Join-Path $root 'src\StudioOneTools.App\StudioOneTools.App.csproj'
$dist = Join-Path $here 'dist'
$vendor = Join-Path $here 'vendor'
$ver = (Select-String -Path $appProj -Pattern '<Version>([0-9.]+)</Version>').Matches[0].Groups[1].Value

# ─── signing helper (Azure Artifact / Trusted Signing) ────────────────
# Signs + RFC-3161 timestamps the finished installers if
# installer/signing-metadata.json exists AND the TrustedSigning module is
# installed. Missing either, or -NoSign, and the build still succeeds unsigned.
$SignMeta = Join-Path $here 'signing-metadata.json'
$TimestampUrl = 'http://timestamp.acs.microsoft.com'

function Test-CanSign {
    if ($NoSign) { return $false }
    if (-not (Test-Path $SignMeta)) {
        Write-Host "  (no signing-metadata.json - skipping signing)" -ForegroundColor DarkYellow
        return $false
    }
    # Run under pwsh (7), never powershell (5.1) -- see azure-artifact-signing
    # notes: a nested 5.1 child can inherit pwsh's PSModulePath and hide 5.1's
    # own module paths, making Get-Command report the module missing even
    # though it is installed, which quietly produces an UNSIGNED build.
    if (-not (Get-Module TrustedSigning)) {
        Import-Module TrustedSigning -ErrorAction SilentlyContinue
    }
    if (-not (Get-Command Invoke-TrustedSigning -ErrorAction SilentlyContinue)) {
        throw ("signing was requested (installer/signing-metadata.json exists) but the " +
               "TrustedSigning module could not be loaded. Run under pwsh, or pass " +
               "-NoSign to build unsigned deliberately.")
    }
    return $true
}

function Invoke-Sign([string[]] $Paths) {
    if (-not (Test-CanSign)) { return }
    Write-Host "== signing: $($Paths -join ', ') ==" -ForegroundColor Cyan
    $meta = Get-Content $SignMeta -Raw | ConvertFrom-Json
    foreach ($p in $Paths) {
        Invoke-TrustedSigning `
            -Endpoint $meta.Endpoint `
            -CodeSigningAccountName $meta.CodeSigningAccountName `
            -CertificateProfileName $meta.CertificateProfileName `
            -Files $p `
            -FileDigest SHA256 `
            -TimestampRfc3161 $TimestampUrl `
            -TimestampDigest SHA256
        $s = Get-AuthenticodeSignature $p
        if ($s.Status -ne 'Valid') { throw "signature check failed on $p : $($s.Status) $($s.StatusMessage)" }
        Write-Host "  ok: $p  <- $($s.SignerCertificate.Subject)" -ForegroundColor Green
    }
}
# ─────────────────────────────────────────────────────────────────────

Write-Host "== Tools for Studio One $ver -- publishing $Config (framework-dependent, win-x64) ==" -ForegroundColor Cyan
dotnet publish $appProj -c $Config | Out-Host
if ($LASTEXITCODE) { throw "publish failed" }

$publishDir = Join-Path $root 'src\StudioOneTools.App\bin\Release\net10.0-windows\win-x64\publish'
$exe = Join-Path $publishDir 'StudioOneTools.App.exe'
if (-not (Test-Path $exe)) { throw "missing: $exe" }
Write-Host ("  published exe: {0:N1} MB" -f ((Get-Item $exe).Length / 1MB)) -ForegroundColor Green

New-Item -ItemType Directory -Force -Path $dist | Out-Null

# ---- Inno Setup exe installers ----------------------------------------------
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    (Get-Command ISCC -ErrorAction SilentlyContinue | Select-Object -First 1 -Expand Source)
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $iscc) {
    Write-Host "Inno Setup not found -- stopped after the framework-dependent publish." -ForegroundColor Yellow
    Write-Host "  Install it (https://jrsoftware.org/isdl.php or 'winget install JRSoftware.InnoSetup')" -ForegroundColor Yellow
    Write-Host "  then run this script again, or compile installer\StudioOneTools.iss directly." -ForegroundColor Yellow
    return
}

& $iscc "/DMyAppVersion=$ver" "/DFlavor=Web" (Join-Path $here 'StudioOneTools.iss') | Out-Host
if ($LASTEXITCODE) { throw "ISCC failed on StudioOneTools.iss (Web)" }
$webExe = Join-Path $dist "Tools-for-Studio-One-$ver-Windows-Web.exe"

$exes = @($webExe)

if (-not $SkipOffline) {
    # ---- fetch (and cache) the .NET Desktop Runtime bootstrapper for Offline ----
    New-Item -ItemType Directory -Force -Path $vendor | Out-Null
    $runtimeExe = Join-Path $vendor 'windowsdesktop-runtime-win-x64.exe'
    if (-not (Test-Path $runtimeExe)) {
        Write-Host "== fetching .NET Desktop Runtime bootstrapper for the Offline flavour ==" -ForegroundColor Cyan
        Invoke-WebRequest -Uri 'https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe' `
            -OutFile $runtimeExe -UseBasicParsing
    }
    Write-Host ("  runtime bootstrapper: {0:N1} MB (cached, reused across builds)" -f `
        ((Get-Item $runtimeExe).Length / 1MB)) -ForegroundColor Green

    & $iscc "/DMyAppVersion=$ver" "/DFlavor=Offline" (Join-Path $here 'StudioOneTools.iss') | Out-Host
    if ($LASTEXITCODE) { throw "ISCC failed on StudioOneTools.iss (Offline)" }
    $exes += Join-Path $dist "Tools-for-Studio-One-$ver-Windows-Offline.exe"
}

Invoke-Sign $exes
foreach ($e in $exes) { Write-Host "installer -> $e" -ForegroundColor Green }

# ---- catalog fragment --------------------------------------------------
. (Join-Path $here 'catalog-fragment.ps1')
$artifacts = @(@{ Os = 'win'; Variant = 'web'; File = $webExe })
if (-not $SkipOffline) {
    $artifacts += @{ Os = 'win'; Variant = 'offline'; File = ($exes | Select-Object -Last 1) }
}
New-CatalogFragment -Product 'studio-one-tools' -Component 'app' -Version $ver `
    -OutDir $dist -CatalogDir $CatalogDir -Artifacts $artifacts
