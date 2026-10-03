#!/usr/bin/env bash
#
# Tools for Studio One -- macOS app
#
# Runs ON THE MAC MINI (never GitHub Actions). Don't call this directly from
# Windows -- use installer/build-mac.ps1, which syncs this working tree to the
# mini, runs this script there, and copies the finished artefacts back to
# ./dist-mac/. Converted from the former GitHub Actions workflow; the step
# bodies are unchanged.
set -euo pipefail

cd "$(dirname "$0")/.."
# NativeAOT links against OpenSSL (-lssl); Homebrew keeps it off the default path.
[ -d /opt/homebrew/opt/openssl@3/lib ] && export LIBRARY_PATH="/opt/homebrew/opt/openssl@3/lib:${LIBRARY_PATH:-}"
# Use Microsoft's official .NET SDK, not Homebrew's: Homebrew's is source-built,
# and self-contained publishes made with it link /opt/homebrew/.../libbrotli*
# (absent on a user's Mac -> dyld abort at launch). Installed once with
# dotnet-install.sh into ~/.dotnet-ms.
if [ -x "$HOME/.dotnet-ms/dotnet" ]; then
  export DOTNET_ROOT="$HOME/.dotnet-ms"; export PATH="$DOTNET_ROOT:$PATH"
else
  echo "~/.dotnet-ms missing: install with dotnet-install.sh --channel 10.0 --install-dir ~/.dotnet-ms"; exit 1
fi
export RUNNER_TEMP="$(mktemp -d)"
export GITHUB_OUTPUT="$RUNNER_TEMP/output"; : > "$GITHUB_OUTPUT"
export GITHUB_ENV="$RUNNER_TEMP/env";       : > "$GITHUB_ENV"
export HOME="${HOME:-$(cd ~ && pwd)}"
DIST="$PWD/dist-mac"; rm -rf "$DIST"; mkdir -p "$DIST"
trap 'rm -rf "$RUNNER_TEMP"' EXIT

# Run one step in its own shell (so `set -e` behaves as it did in Actions),
# then load anything it exported via $GITHUB_ENV (e.g. SIGN_* identities).
run_step () {
  local name="$1" ext="$2"; local f="$RUNNER_TEMP/step.$ext"
  echo; echo "=== $name ==="
  cat > "$f"
  if [ "$ext" = ps1 ]; then pwsh -NoProfile -NonInteractive -File "$f"
  else bash -eo pipefail "$f"; fi
  set -a; . "$GITHUB_ENV"; set +a
}
run_step 'Import Developer ID certificates' sh <<'STEP_EOF_0'
.github/scripts/mac-sign.sh setup-local
STEP_EOF_0
run_step 'Read project version' sh <<'STEP_EOF_1'
V=$(grep -m1 '<Version>' src/StudioOneTools.Avalonia/StudioOneTools.Avalonia.csproj | sed -E 's/.*<Version>([0-9.]+)<\/Version>.*/\1/')
test -n "$V"
echo "version=$V" >> "$GITHUB_OUTPUT"
echo "Tools for Studio One version: $V"
STEP_EOF_1
run_step 'Restore + build + test' sh <<'STEP_EOF_2'
dotnet build src/StudioOneTools.Core/StudioOneTools.Core.csproj -c Release
dotnet build src/StudioOneTools.StudioOne/StudioOneTools.StudioOne.csproj -c Release
dotnet build src/StudioOneTools.Avalonia/StudioOneTools.Avalonia.csproj -c Release
dotnet test tests/StudioOneTools.Tests/StudioOneTools.Tests.csproj -c Release
STEP_EOF_2
run_step 'Publish (osx-arm64)' sh <<'STEP_EOF_3'
dotnet publish src/StudioOneTools.Avalonia/StudioOneTools.Avalonia.csproj \
  -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o publish
echo "--- publish/ contents ---"
ls -la publish/
STEP_EOF_3
run_step 'Assemble .app bundle' sh <<'STEP_EOF_4'
set -euo pipefail
VER="$(grep -h "^version=" "$GITHUB_OUTPUT" | tail -1 | cut -d= -f2-)"
APP="Tools for Studio One.app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# The publish output name isn't guaranteed to exactly equal the
# csproj name on osx-arm64, so find it rather than assume.
BIN=$(find publish -maxdepth 1 -type f -perm -u+x ! -name '*.dylib' | head -1)
test -n "$BIN" || { echo "no executable found in publish/"; ls -la publish/; exit 1; }
echo "executable: $BIN"
cp "$BIN" "$APP/Contents/MacOS/StudioOneTools"
chmod +x "$APP/Contents/MacOS/StudioOneTools"
# A self-contained (non-AOT) publish still drops the Avalonia/Skia
# native libs alongside the executable rather than folding them in
# -- ship every one.
find publish -maxdepth 1 -name '*.dylib' -exec cp {} "$APP/Contents/MacOS/" \;

sed "s/__VERSION__/$VER/g" installer/Info.plist > "$APP/Contents/Info.plist"

# Generate icon.icns from the brand mark -- no .icns exists anywhere
# in the repo yet, and there's no MSBuild ApplicationIcon equivalent
# for a plain self-contained publish on macOS.
ICONSET="$RUNNER_TEMP/icon.iconset"
rm -rf "$ICONSET"; mkdir -p "$ICONSET"
for size in 16 32 64 128 256 512 1024; do
  sips -z "$size" "$size" src/StudioOneTools.Avalonia/Branding/Assets/six_walls_mark.png \
    --out "$ICONSET/icon_${size}x${size}.png" > /dev/null
done
cp "$ICONSET/icon_32x32.png"     "$ICONSET/icon_16x16@2x.png"
cp "$ICONSET/icon_64x64.png"     "$ICONSET/icon_32x32@2x.png"
cp "$ICONSET/icon_256x256.png"   "$ICONSET/icon_128x128@2x.png"
cp "$ICONSET/icon_512x512.png"   "$ICONSET/icon_256x256@2x.png"
cp "$ICONSET/icon_1024x1024.png" "$ICONSET/icon_512x512@2x.png"
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/icon.icns"

echo "--- bundle contents ---"
find "$APP" -maxdepth 3
STEP_EOF_4
run_step 'Smoke check (Mach-O sanity)' sh <<'STEP_EOF_5'
file "Tools for Studio One.app/Contents/MacOS/StudioOneTools" | grep -q "Mach-O"
STEP_EOF_5
run_step 'Package' sh <<'STEP_EOF_6'
set -euo pipefail
WS="$PWD"
SIGN="$WS/.github/scripts/mac-sign.sh"
VER="$(grep -h "^version=" "$GITHUB_OUTPUT" | tail -1 | cut -d= -f2-)"
APP="$WS/Tools for Studio One.app"

# Sign each nested dylib individually BEFORE the bundle itself, same
# reasoning as six-walls-installer: codesign on a bundle (no --deep)
# leaves a nested binary alone if it already carries a signature,
# and some Avalonia-native dylibs ship pre-signed with something
# that has no Developer ID and no secure timestamp -- notarization
# then rejects that outright. Signing directly always overwrites it.
#
# Unlike the Installer's NativeAOT publish, this is a normal
# self-contained PublishSingleFile build: native libs are embedded
# in the single executable and self-extracted at runtime, not
# shipped as loose .dylib files -- so there may be none to sign
# here. nullglob avoids passing the Avalonia bundle the literal
# unmatched glob if so.
shopt -s nullglob
dylibs=("$APP"/Contents/MacOS/*.dylib)
shopt -u nullglob
if [ ${#dylibs[@]} -gt 0 ]; then
  "$SIGN" bundle "${dylibs[@]}"
fi
SIGN_ENTITLEMENTS="$WS/installer/entitlements.plist" "$SIGN" bundle "$APP"

# Launch smoke test. A hardened-runtime .NET app with the wrong entitlements
# (or a runtime linked against Homebrew libs) builds, signs, and notarizes
# fine, then dies on the user's Mac with no window -- the 1.4.0 bug. Start the
# signed binary and require it to still be alive after a few seconds.
echo "--- launch smoke test ---"
if [ -n "${SIGNED:-}" ]; then
  otool -L "$APP/Contents/MacOS/StudioOneTools" | grep -E "/opt/homebrew|/usr/local" \
    && { echo "binary links a non-system library"; exit 1; } || true
  "$APP/Contents/MacOS/StudioOneTools" > "$RUNNER_TEMP/smoke.log" 2>&1 &
  SMOKE_PID=$!
  sleep 8
  if kill -0 "$SMOKE_PID" 2>/dev/null; then
    kill "$SMOKE_PID"; echo "launch OK"
  else
    echo "app exited within 8s of launch:"; cat "$RUNNER_TEMP/smoke.log"; exit 1
  fi
fi

ROOT="$RUNNER_TEMP/pkgroot"
rm -rf "$ROOT"
mkdir -p "$ROOT/Applications"
cp -R "$APP" "$ROOT/Applications/"

PKG="Tools-for-Studio-One-$VER-macOS.pkg"
COMP="$RUNNER_TEMP/component"
rm -rf "$COMP"
mkdir -p "$COMP"
pkgbuild \
  --root "$ROOT" \
  --identifier com.sixwalls.studioonetools.app \
  --version "$VER" \
  --install-location / \
  "$COMP/StudioOneTools.pkg"

# Wrap the component in a product archive whose distribution declares both
# architectures. A bare component pkg makes Installer.app on Apple Silicon
# demand Rosetta (same fix as The Installer 1.3.4, 2026-10-03).
DISTXML="$RUNNER_TEMP/distribution.xml"
productbuild --synthesize --package "$COMP/StudioOneTools.pkg" "$DISTXML"
if grep -q 'hostArchitectures=' "$DISTXML"; then
  sed -i '' -E 's/hostArchitectures="[^"]*"/hostArchitectures="x86_64,arm64"/' "$DISTXML"
elif grep -q '<options ' "$DISTXML"; then
  sed -i '' 's|<options |<options hostArchitectures="x86_64,arm64" |' "$DISTXML"
else
  sed -i '' 's|</installer-gui-script>|    <options hostArchitectures="x86_64,arm64"/>\
</installer-gui-script>|' "$DISTXML"
fi
sed -i '' 's|</installer-gui-script>|    <title>Tools for Studio One</title>\
</installer-gui-script>|' "$DISTXML"
grep -q 'hostArchitectures="x86_64,arm64"' "$DISTXML" || { echo "distribution lacks hostArchitectures"; cat "$DISTXML"; exit 1; }
cat "$DISTXML"
productbuild --distribution "$DISTXML" --package-path "$COMP" "$WS/$PKG"
"$SIGN" pkg "$WS/$PKG"

# Does NOT wait -- see the header comment. Submits the signed-but-
# not-yet-stapled .pkg and prints the submission id to
# notarization-id.txt; check it later with notary_status.py, then
# run the "macOS staple" workflow once it says Accepted.
"$SIGN" notarize "$WS/$PKG"

echo "pkg=$PKG" >> "$GITHUB_OUTPUT"
echo "--- sha256 (pre-staple; will change once stapled) ---"
shasum -a 256 "$WS/$PKG"
ls -l "$WS/$PKG"
STEP_EOF_6
run_step 'Import Developer ID certificates' sh <<'STEP_EOF_7'
.github/scripts/mac-sign.sh setup-local
STEP_EOF_7
run_step 'Staple + verify' sh <<'STEP_EOF_8'
set -euo pipefail
SIGN="$PWD/.github/scripts/mac-sign.sh"
PKG=$(ls Tools-for-Studio-One-*-macOS.pkg)

"$SIGN" staple "$PKG"
"$SIGN" verify "$PKG"

echo "pkg=$PKG" >> "$GITHUB_OUTPUT"
STEP_EOF_8
run_step 'Catalog fragment' sh <<'STEP_EOF_9'
set -euo pipefail
PKG="$(grep -h "^pkg=" "$GITHUB_OUTPUT" | tail -1 | cut -d= -f2-)"
VER=$(echo "$PKG" | sed -E 's/Tools-for-Studio-One-([0-9.]+)-macOS\.pkg/\1/')
BYTES=$(stat -f%z "$PKG")
SHA=$(shasum -a 256 "$PKG" | awk '{print $1}')

jq -n \
  --arg product "studio-one-tools" \
  --arg version "$VER" \
  --arg date "$(date -u +%Y-%m-%d)" \
  --arg file "Downloads/$PKG" \
  --argjson bytes "$BYTES" \
  --arg sha "$SHA" \
  '{schemaVersion: 1, product: $product, component: "app", version: $version,
    date: $date, soundChange: "none",
    artifacts: [{os: "mac", variant: "app", file: $file,
                 bytes: $bytes, sha256: $sha, signed: true}]}' \
  > "studio-one-tools-app-$VER.json"

cat "studio-one-tools-app-$VER.json"
echo "--- sha256 (final, stapled) ---"
shasum -a 256 "$PKG"
STEP_EOF_9

echo; echo "=== Collect artefacts ==="
for k in pkg pkg_suggested zip; do
  f=$(grep -h "^$k=" "$GITHUB_OUTPUT" | tail -1 | cut -d= -f2- || true)
  [ -n "$f" ] && cp "$f" "$DIST/"
done
cp ./*-app-*.json "$DIST/" 2>/dev/null || true
ls -l "$DIST"
