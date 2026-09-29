<#
.SYNOPSIS
  Builds the macOS release artefacts on the Mac mini and copies them back.

.DESCRIPTION
  No GitHub, no Actions. Syncs this working tree (uncommitted changes
  included) to the mini, runs installer/<Script> there, and pulls the finished,
  signed, notarized artefacts into ./dist-mac/.

  Produces the signed, notarized, stapled .pkg plus the catalog-fragment
  .json in ./dist-mac/. Needs dotnet + jq on the mini (already there).
#>
param([string]$Script = 'build-mac.sh', [string]$Mini = 'mac-mini')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$name = Split-Path $repo -Leaf
$tar  = Join-Path $env:TEMP "$name-src.tar"

Push-Location $repo
try {
    # Top-level items only (bsdtar --exclude globs are unanchored, and 'build-*'
    # would also swallow installer/build-mac.sh).
    $items = Get-ChildItem -Force | Where-Object { $_.Name -notmatch '^(\.git|build|build-.*|bin|obj|publish|dist|dist-mac|node_modules|\.vs)$' } | ForEach-Object Name
    & "$env:SystemRoot/System32/tar.exe" -cf $tar --exclude=node_modules --exclude=dist --exclude=dist-mac --exclude=bin --exclude=obj @items
    if ($LASTEXITCODE) { throw 'tar failed' }
    ssh $Mini "chmod -R u+rwX ~/build/$name 2>/dev/null; rm -rf ~/build/$name && mkdir -p ~/build/$name"
    scp -q $tar "${Mini}:build/$name.tar"
    ssh $Mini "tar -xf ~/build/$name.tar -C ~/build/$name && chmod -R u+rwX ~/build/$name && rm ~/build/$name.tar && cd ~/build/$name && find installer .github/scripts -name '*.sh' -exec sed -i '' 's/\r$//' {} + -exec chmod +x {} +"
    ssh -o ServerAliveInterval=30 $Mini "cd ~/build/$name && PATH=/opt/homebrew/bin:/usr/local/bin:`$PATH installer/$Script"
    if ($LASTEXITCODE) { throw "mac build failed ($Script)" }
    $dist = Join-Path $repo 'dist-mac'
    New-Item -ItemType Directory -Force $dist | Out-Null
    scp "${Mini}:build/$name/dist-mac/*" $dist
    Get-ChildItem $dist | Select-Object Name, Length
} finally {
    Pop-Location
    Remove-Item $tar -ErrorAction SilentlyContinue
}
