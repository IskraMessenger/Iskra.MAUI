#!/bin/bash
# Build Iskra Messenger for Android ARM32 with Mono runtime

set -e

CONFIGURATION="${1:-Release}"
PROJECT_PATH="src/Iskra.Maui/Iskra.Maui.csproj"
OUTPUT_DIR="artifacts/android-arm32-mono"
FRAMEWORK="net8.0-android"
RUNTIME_ID="android-arm"

echo "========================================"
echo "Building Iskra for Android ARM32 (Mono)"
echo "========================================"
echo ""

# Очистка
echo "Cleaning previous build..."
rm -rf "$OUTPUT_DIR"

# Restore
echo "Restoring dependencies..."
dotnet restore "$PROJECT_PATH" \
    -p:ShortP2PBuildAndroid=true \
    -p:TargetFramework="$FRAMEWORK"

# Build
echo "Building project..."
dotnet build "$PROJECT_PATH" \
    -f "$FRAMEWORK" \
    -c "$CONFIGURATION" \
    -p:ShortP2PBuildAndroid=true \
    -p:RuntimeIdentifier="$RUNTIME_ID" \
    -p:AndroidSupportedAbis=armeabi-v7a \
    -p:UseMonoRuntime=true

# Publish
echo "Publishing APK..."
dotnet publish "$PROJECT_PATH" \
    -f "$FRAMEWORK" \
    -c "$CONFIGURATION" \
    -r "$RUNTIME_ID" \
    -o "$OUTPUT_DIR" \
    -p:ShortP2PBuildAndroid=true \
    -p:AndroidSupportedAbis=armeabi-v7a \
    -p:UseMonoRuntime=true \
    -p:AndroidPackageFormat=apk \
    -p:RunAOTCompilation=false \
    -p:PublishTrimmed=false

echo ""
echo "========================================"
echo "Build completed successfully!"
echo "========================================"
echo "APK location: $OUTPUT_DIR"
echo ""

# Информация о сборке
APK_FILE=$(find "$OUTPUT_DIR" -name "*.apk" | head -n 1)
if [ -n "$APK_FILE" ]; then
    APK_SIZE=$(du -h "$APK_FILE" | cut -f1)
    echo "APK file: $(basename "$APK_FILE")"
    echo "APK size: $APK_SIZE"
    echo "Runtime: Mono"
    echo "Architecture: ARM32 (armeabi-v7a)"
fi
