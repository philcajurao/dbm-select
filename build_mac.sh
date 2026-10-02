#!/bin/bash

# ==========================================================
# DBM Select — Internal macOS Distribution Build Script
# Creates a signed .app bundle + a distributable .dmg file
# Run this script ON YOUR MAC from the project root folder:
#   chmod +x build_mac.sh
#   ./build_mac.sh
# ==========================================================

# --- CONFIGURATION ---
APP_NAME="DBM Select"
BINARY_NAME="dbm-select"
FRAMEWORK="net8.0"
VERSION="1.0.0"

# Pass osx-arm64 or osx-x64 to build for a specific Mac architecture.
# Without an argument, build for the architecture of the Mac running this script.
case "${1:-}" in
    "")
        case "$(uname -m)" in
            arm64) RUNTIME="osx-arm64" ;;
            x86_64) RUNTIME="osx-x64" ;;
            *)
                echo "❌  Unsupported Mac architecture: $(uname -m)"
                exit 1
                ;;
        esac
        ;;
    osx-arm64|osx-x64) RUNTIME="$1" ;;
    *)
        echo "Usage: $0 [osx-arm64|osx-x64]"
        exit 1
        ;;
esac

PUBLISH_DIR="bin/Release/$FRAMEWORK/$RUNTIME/publish"
DEST_DIR="bin/Distribution"
APP_BUNDLE="$DEST_DIR/$APP_NAME.app"
DMG_NAME="${APP_NAME// /_}_v${VERSION}_${RUNTIME}.dmg"
DMG_PATH="$DEST_DIR/$DMG_NAME"
DMG_STAGING="$DEST_DIR/dmg_staging"

# Stop script on any error
set -e

echo ""
echo "=========================================================="
echo "  🚀  DBM Select — macOS Distribution Builder"
echo "  Runtime : $RUNTIME"
echo "  Version : $VERSION"
echo "=========================================================="
echo ""

# --- STEP 1: Prepare distribution output ---
mkdir -p "$DEST_DIR"
rm -rf "$APP_BUNDLE" "$DMG_STAGING"
rm -f "$DMG_PATH"

# --- STEP 2: .NET Publish ---
echo "📦  Publishing .NET project (this may take a minute)..."
dotnet publish -c Release -r $RUNTIME --self-contained
echo "    ✅  Publish complete."
echo ""

# --- STEP 3: Create .app Bundle Structure ---
echo "📂  Building .app bundle..."
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# --- STEP 4: Copy binary files ---
cp -a "$PUBLISH_DIR/." "$APP_BUNDLE/Contents/MacOS/"

# --- STEP 5: Copy resources ---
if [ -f "Assets/AppIcon.icns" ]; then
    cp "Assets/AppIcon.icns" "$APP_BUNDLE/Contents/Resources/"
else
    echo "⚠️   WARNING: Assets/AppIcon.icns not found — app will use a generic icon."
fi

if [ -f "Info.plist" ]; then
    cp "Info.plist" "$APP_BUNDLE/Contents/Info.plist"
else
    echo "❌  ERROR: Info.plist not found. Cannot build app bundle."
    exit 1
fi

# --- STEP 6: Cleanup duplicate assets ---
if [ -f "$APP_BUNDLE/Contents/MacOS/Assets/AppIcon.icns" ]; then
    rm "$APP_BUNDLE/Contents/MacOS/Assets/AppIcon.icns"
fi

# --- STEP 7: Set executable permission ---
echo "🔐  Setting executable permission..."
chmod +x "$APP_BUNDLE/Contents/MacOS/$BINARY_NAME"

# --- STEP 8: Ad-hoc Code Sign ---
# Ad-hoc signing is suitable for local testing, but does not replace Developer ID
# signing and notarization for Gatekeeper-friendly distribution.
echo "✍️   Ad-hoc code signing the app bundle..."
codesign --sign - \
         --force \
         --deep \
         --timestamp=none \
         --options runtime \
         "$APP_BUNDLE"
codesign --verify --deep --strict "$APP_BUNDLE"
echo "    ✅  Code signing complete."
echo ""

# --- STEP 9: Build the DMG installer ---
echo "💿  Creating DMG installer..."
mkdir -p "$DMG_STAGING"

# Copy the .app into the staging folder
cp -a "$APP_BUNDLE" "$DMG_STAGING/"

# Create a symbolic link to /Applications so staff can drag-and-drop
ln -s /Applications "$DMG_STAGING/Applications"

# Build the DMG from the staging folder
hdiutil create \
    -volname "$APP_NAME" \
    -srcfolder "$DMG_STAGING" \
    -ov \
    -format UDZO \
    "$DMG_PATH"

# Cleanup staging
rm -rf "$DMG_STAGING"

echo "    ✅  DMG created."
echo ""
echo "=========================================================="
echo "  ✅  BUILD SUCCESSFUL!"
echo ""
echo "  .app  →  $(pwd)/$APP_BUNDLE"
echo "  .dmg  →  $(pwd)/$DMG_PATH"
echo ""
echo "  📤  To distribute to staff:"
echo "      For organization-wide distribution, use Developer ID signing and notarization."
echo "      Ad-hoc signing may require users to approve the app in macOS security settings."
echo ""
echo "=========================================================="
echo ""