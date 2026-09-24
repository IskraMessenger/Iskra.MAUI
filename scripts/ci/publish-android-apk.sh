#!/usr/bin/env bash
# Publish a signed TorgLink Android APK for GitHub Actions.
# Usage: publish-android-apk.sh <runtime-identifier> <configuration>
#
# Signing (always; unsigned APKs will not install on Android / BlueStacks):
#   All of ANDROID_KEYSTORE_BASE64, ANDROID_KEY_ALIAS, ANDROID_KEY_PASSWORD,
#   ANDROID_KEYSTORE_PASSWORD → release keystore from secrets.
#   Otherwise → scripts/android/ci-debug.keystore
#     alias androiddebugkey / password android (public CI debug key, not Play Store).

set -euo pipefail

rid="${1:?usage: publish-android-apk.sh <runtime-identifier> <configuration>}"
configuration="${2:?usage: publish-android-apk.sh <runtime-identifier> <configuration>}"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

tmp="${RUNNER_TEMP:-/tmp}"
extra=()

if [[ -n "${ANDROID_KEYSTORE_BASE64:-}" && -n "${ANDROID_KEY_ALIAS:-}" && -n "${ANDROID_KEY_PASSWORD:-}" && -n "${ANDROID_KEYSTORE_PASSWORD:-}" ]]; then
  keystore="$tmp/torglink.keystore"
  echo "$ANDROID_KEYSTORE_BASE64" | base64 -d > "$keystore"
  extra+=(
    -p:AndroidKeyStore=true
    -p:AndroidSigningKeyStore="$keystore"
    -p:AndroidSigningKeyAlias="$ANDROID_KEY_ALIAS"
    -p:AndroidSigningKeyPass="$ANDROID_KEY_PASSWORD"
    -p:AndroidSigningStorePass="$ANDROID_KEYSTORE_PASSWORD"
  )
  echo "Signing with release keystore from GitHub secrets."
else
  keystore="$repo_root/scripts/android/ci-debug.keystore"
  if [[ ! -f "$keystore" ]]; then
    echo "Missing CI debug keystore: $keystore" >&2
    exit 1
  fi
  extra+=(
    -p:AndroidKeyStore=true
    -p:AndroidSigningKeyStore="$keystore"
    -p:AndroidSigningKeyAlias=androiddebugkey
    -p:AndroidSigningKeyPass=android
    -p:AndroidSigningStorePass=android
  )
  echo "Release signing secrets incomplete; signing with scripts/android/ci-debug.keystore (installable, not for Play Store)."
fi

# EnableWindowsTargeting: restore evaluates every TFM of TorgLink.Maui; on Linux
# ShortP2P.Transport.Bluetooth.Windows fails with NETSDK1100 without this flag.
# IncludeAndroid=true → ShortP2PBuildAndroid via src/Directory.Build.props.
dotnet publish src/TorgLink.Maui/TorgLink.Maui.csproj \
  -c "$configuration" \
  -f net10.0-android \
  -p:IncludeAndroid=true \
  -p:EnableWindowsTargeting=true \
  -p:RuntimeIdentifier="$rid" \
  -p:AndroidPackageFormat=apk \
  "${extra[@]}"

mkdir -p dist/apk
mapfile -t apks < <(find src/TorgLink.Maui/bin -type f -name '*.apk' ! -name '*-Unsigned.apk' ! -name '*-unsigned.apk')
if [[ ${#apks[@]} -eq 0 ]]; then
  echo "No signed APK produced. Remaining APKs:" >&2
  find src/TorgLink.Maui/bin -type f -name '*.apk' -print >&2 || true
  exit 1
fi

dest="dist/apk/TorgLink-${rid}.apk"
cp "${apks[0]}" "$dest"
echo "Copied ${apks[0]} -> $dest"

# Fail the job if MSBuild still emitted an unsigned package.
python3 - "$dest" <<'PY'
import sys
import zipfile
from pathlib import Path

path = Path(sys.argv[1])
data = path.read_bytes()
v2 = b"APK Sig Block 42" in data
with zipfile.ZipFile(path) as z:
    v1 = any(
        name.startswith("META-INF/") and name.endswith((".RSA", ".DSA", ".EC"))
        for name in z.namelist()
    )
if not (v1 or v2):
    print(f"ERROR: {path} has no APK signature (v1/v2/v3). Android will refuse install.", file=sys.stderr)
    sys.exit(1)
print(f"APK signature OK: v1={v1} v2/v3={v2} ({path})")
PY
