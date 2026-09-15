#!/usr/bin/env bash
#
# Six Walls macOS signing helper -- Developer ID codesign + notarize + staple.
# The same file lives in every plugin repo; keep the copies identical.
#
#   mac-sign.sh setup                 read the repo secrets, build a throwaway
#                                     keychain, export SIGN_* into GITHUB_ENV
#   mac-sign.sh bundle <path>...      sign a .vst3 / .component (hardened runtime)
#   mac-sign.sh pkg <path>...         sign a .pkg in place (Developer ID Installer)
#   mac-sign.sh notarize <path>...    submit to notarytool and wait
#   mac-sign.sh staple <path>...      attach the notarization ticket
#   mac-sign.sh verify <path>...      report what actually got signed
#
# Signing is OPT-IN. With no Developer ID secrets in the repo every command
# below is a no-op and the build ships unsigned exactly as it did before, so
# this file is safe to land ahead of the credentials.
#
# Secrets it reads (all base64 where noted -- `base64 -w0` (GNU) or `openssl base64 -A` to encode;
# `certutil -encode` / PowerShell on Windows):
#   APPLE_CERT_APP_P12            Developer ID *Application* .p12, base64
#   APPLE_CERT_APP_PASSWORD       its export password
#   APPLE_CERT_INSTALLER_P12      Developer ID *Installer* .p12, base64
#   APPLE_CERT_INSTALLER_PASSWORD its export password
#   APPLE_API_KEY_P8              App Store Connect API key .p8, base64
#   APPLE_API_KEY_ID              the key's 10-char id
#   APPLE_API_ISSUER_ID           the issuer UUID
#
set -euo pipefail

enabled () { [ -n "${SIGN_APP_IDENTITY:-}" ]; }
say ()     { printf '%s\n' "$*"; }

setup () {
  if [ -z "${APPLE_CERT_APP_P12:-}" ]; then
    say "No APPLE_CERT_APP_P12 secret in this repo -- building UNSIGNED."
    echo "SIGNED=0" >> "$GITHUB_ENV"
    return 0
  fi

  local kc="$RUNNER_TEMP/sixwalls-signing.keychain-db"
  local kcpw; kcpw=$(uuidgen)

  # A throwaway keychain rather than the login one: it dies with the runner,
  # and set-key-partition-list below is what stops codesign popping a UI
  # prompt on a headless machine.
  security create-keychain -p "$kcpw" "$kc"
  security set-keychain-settings -lut 21600 "$kc"
  security unlock-keychain -p "$kcpw" "$kc"

  printf '%s' "$APPLE_CERT_APP_P12" | openssl base64 -d -A > "$RUNNER_TEMP/app.p12"
  security import "$RUNNER_TEMP/app.p12" -k "$kc" \
    -P "${APPLE_CERT_APP_PASSWORD:-}" -T /usr/bin/codesign -T /usr/bin/security

  if [ -n "${APPLE_CERT_INSTALLER_P12:-}" ]; then
    printf '%s' "$APPLE_CERT_INSTALLER_P12" | openssl base64 -d -A > "$RUNNER_TEMP/installer.p12"
    security import "$RUNNER_TEMP/installer.p12" -k "$kc" \
      -P "${APPLE_CERT_INSTALLER_PASSWORD:-}" -T /usr/bin/productsign -T /usr/bin/security
  fi

  security set-key-partition-list -S apple-tool:,apple:,codesign:,productsign: \
    -s -k "$kcpw" "$kc" > /dev/null
  # Keep the default keychains searchable; codesign resolves the identity by
  # hash, but notarytool and productsign want the keychain on the list.
  security list-keychains -d user -s "$kc" $(security list-keychains -d user | tr -d '"')

  rm -f "$RUNNER_TEMP/app.p12" "$RUNNER_TEMP/installer.p12"

  say "--- identities in the signing keychain ---"
  security find-identity -v "$kc"

  local app inst
  app=$(security find-identity -v "$kc"  | awk '/Developer ID Application/ {print $2; exit}')
  inst=$(security find-identity -v "$kc" | awk '/Developer ID Installer/   {print $2; exit}')
  [ -n "$app" ] || { say "no Developer ID Application identity in the .p12"; exit 1; }
  [ -n "$inst" ] || say "WARNING: no Developer ID Installer identity -- the .pkg will not be signed."

  {
    echo "SIGNED=1"
    echo "SIGN_KEYCHAIN=$kc"
    echo "SIGN_APP_IDENTITY=$app"
    echo "SIGN_INSTALLER_IDENTITY=$inst"
  } >> "$GITHUB_ENV"

  if [ -n "${APPLE_API_KEY_P8:-}" ]; then
    printf '%s' "$APPLE_API_KEY_P8" | openssl base64 -d -A > "$RUNNER_TEMP/notary-key.p8"
    chmod 600 "$RUNNER_TEMP/notary-key.p8"
    {
      echo "SIGN_API_KEY=$RUNNER_TEMP/notary-key.p8"
      echo "SIGN_API_KEY_ID=${APPLE_API_KEY_ID:-}"
      echo "SIGN_API_ISSUER=${APPLE_API_ISSUER_ID:-}"
    } >> "$GITHUB_ENV"
  else
    say "WARNING: no APPLE_API_KEY_P8 -- signing without notarization."
    say "         Gatekeeper still blocks a downloaded unnotarized bundle."
  fi
}

sign_bundle () {
  enabled || return 0
  for b in "$@"; do
    say "codesign $b"
    # --options runtime (hardened runtime) is a notarization requirement;
    # --timestamp gets a trusted Apple timestamp so the signature outlives
    # the certificate. No entitlements: a plug-in inherits the host's.
    codesign --force --timestamp --options runtime \
             --keychain "$SIGN_KEYCHAIN" --sign "$SIGN_APP_IDENTITY" "$b"
    codesign --verify --strict --verbose=2 "$b"
  done
}

sign_pkg () {
  enabled || return 0
  [ -n "${SIGN_INSTALLER_IDENTITY:-}" ] || { say "no installer identity; leaving .pkg unsigned"; return 0; }
  for p in "$@"; do
    say "productsign $p"
    productsign --keychain "$SIGN_KEYCHAIN" --sign "$SIGN_INSTALLER_IDENTITY" "$p" "$p.signed"
    mv "$p.signed" "$p"
    pkgutil --check-signature "$p"
  done
}

notarize () {
  enabled || return 0
  [ -n "${SIGN_API_KEY:-}" ] || { say "no notarytool key; skipping notarization"; return 0; }
  for f in "$@"; do
    say "notarytool submit $f"
    local out id
    # Captured rather than left to stream straight through: --wait exits 0
    # even when the notarization itself comes back Invalid (only a
    # connectivity/submission failure is a non-zero exit), so the only way
    # to catch a content rejection is to check the printed status -- and
    # when it isn't Accepted, `notarytool log` is the only place Apple's
    # actual reason shows up (the submit output never explains "Invalid").
    out=$(xcrun notarytool submit "$f" \
      --key "$SIGN_API_KEY" --key-id "$SIGN_API_KEY_ID" --issuer "$SIGN_API_ISSUER" \
      --wait --timeout 30m)
    echo "$out"
    if ! echo "$out" | grep -q '^  status: Accepted'; then
      id=$(echo "$out" | awk '/^  id:/ {print $2; exit}')
      say "notarization did not come back Accepted for $f (submission $id) -- log:"
      xcrun notarytool log "$id" \
        --key "$SIGN_API_KEY" --key-id "$SIGN_API_KEY_ID" --issuer "$SIGN_API_ISSUER" || true
      exit 1
    fi
  done
}

# Six-walls-installer-only addition (not in the plugins' copies of this file --
# they've never needed it, since their submissions have always come back in
# seconds). --wait ties up a macOS runner -- billed at 10x -- for however
# long Apple takes, which one night in 2026-09 turned out to be hours, not
# seconds. `submit` returns the moment Apple accepts the upload for
# processing (seconds), writes the submission id next to the file so a
# *separate, unbilled* check (shared/_apple-signing/notary_status.py, a
# plain HTTPS call to the Notary API -- no macOS runner, no Actions minutes
# at all) can poll it later, and a short follow-up job staples once it's
# ready. See macos-build.yml / macos-staple.yml.
submit () {
  enabled || return 0
  [ -n "${SIGN_API_KEY:-}" ] || { say "no notarytool key; skipping notarization submit"; return 0; }
  for f in "$@"; do
    say "notarytool submit --no-wait $f"
    local out id
    out=$(xcrun notarytool submit "$f" \
      --key "$SIGN_API_KEY" --key-id "$SIGN_API_KEY_ID" --issuer "$SIGN_API_ISSUER")
    echo "$out"
    id=$(echo "$out" | awk '/^  id:/ {print $2; exit}')
    [ -n "$id" ] || { say "no submission id in notarytool output for $f"; exit 1; }
    echo "$id" > "$(dirname "$f")/notarization-id.txt"
    say "submission id: $id (not waiting -- check status with notary_status.py)"
  done
}

staple () {
  enabled || return 0
  [ -n "${SIGN_API_KEY:-}" ] || return 0
  for f in "$@"; do
    say "stapler staple $f"
    xcrun stapler staple "$f"
    xcrun stapler validate "$f"
  done
}

verify () {
  for f in "$@"; do
    say "=== $f ==="
    if [ -d "$f" ]; then
      # A plug-in bundle, not an app: `spctl --assess` is the wrong instrument
      # here (it judges executables and installers, and rejects a perfectly good
      # .vst3 on principle). The pair that actually matters is the seal, and
      # whether a notarization ticket is attached.
      codesign --display --verbose=4 "$f" 2>&1 | grep -E 'Identifier|Authority|Timestamp|flags' || true
      codesign --verify --deep --strict --verbose=2 "$f" 2>&1 || true
      codesign --verify --test-requirement="=notarized" --verbose=2 "$f" 2>&1 || true
    else
      # An installer: this IS what Gatekeeper runs when the user double-clicks.
      pkgutil --check-signature "$f" 2>&1 | head -8 || true
      spctl --assess --type install --verbose=4 "$f" 2>&1 || true
    fi
  done
}

cmd=${1:?usage: mac-sign.sh setup|bundle|pkg|notarize|submit|staple|verify ...}
shift || true
case "$cmd" in
  setup)    setup ;;
  bundle)   sign_bundle "$@" ;;
  pkg)      sign_pkg "$@" ;;
  notarize) notarize "$@" ;;
  submit)   submit "$@" ;;
  staple)   staple "$@" ;;
  verify)   verify "$@" ;;
  *) say "unknown command: $cmd"; exit 2 ;;
esac
