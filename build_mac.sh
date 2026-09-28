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

# Change to "osx-arm64" if your Macs are Apple Silicon (M1/M2/M3/M4)
# Change to "osx-x64"   if your Macs are Intel
# Run both targets to support all Macs on your team
RUNTIME="osx-arm64"

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

# --- STEP 1: Clean previous build ---
if [ -d "$DEST_DIR" ]; then
    echo "🧹  Cleaning previous distribution folder..."
    rm -rf "$DEST_DIR"
fi
mkdir -p "$DEST_DIR"

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
# This is a FREE local signature (no Apple Developer account required).
# It allows the app to pass macOS Gatekeeper on your staff's machines.
# The "--deep" flag signs all nested binaries (required for .NET apps).
echo "✍️   Ad-hoc code signing the app bundle..."
codesign --sign - \
         --force \
         --deep \
         --timestamp=none \
         --options runtime \
         "$APP_BUNDLE"
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
echo "      Share the .dmg file via USB, AirDrop, or network share."
echo "      Staff: double-click the DMG → drag 'DBM Select' to Applications."
echo ""
echo "  ⚠️  First launch on each Mac:"
echo "      Right-click the app → Open → 'Open' (once only, bypasses Gatekeeper)."
echo "      After that, the app opens normally every time."
echo "=========================================================="
echo ""
read -p "Press Enter to exit..."