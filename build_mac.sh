#!/bin/bash

# --- CONFIGURATION ---
APP_NAME="DBM Select"
FRAMEWORK="net8.0"
RUNTIME="osx-x64"
PUBLISH_DIR="bin/Release/$FRAMEWORK/$RUNTIME/publish"
DEST_DIR="bin/Distribution"

# Stop script on any error
set -e 

echo "=========================================="
echo "🚀 STARTING BUILD: $APP_NAME ($RUNTIME)"
echo "=========================================="

# 1. Clean previous distribution to ensure no stale files
if [ -d "$DEST_DIR" ]; then
    echo "🧹 Cleaning previous distribution folder..."
    rm -rf "$DEST_DIR"
fi

# 2. Run .NET Publish
echo "📦 Publishing .NET project..."
dotnet publish -c Release -r $RUNTIME --self-contained

# 3. Create .app Directory Structure
echo "📂 Creating macOS .app bundle structure..."
mkdir -p "$DEST_DIR/$APP_NAME.app/Contents/MacOS"
mkdir -p "$DEST_DIR/$APP_NAME.app/Contents/Resources"

# 4. Copy Binary Files
echo "mc Copying publish files to bundle..."
cp -a "$PUBLISH_DIR/." "$DEST_DIR/$APP_NAME.app/Contents/MacOS/"

# 5. Copy Resources (Icon & Plist)
echo "🎨 Copying Assets..."

if [ -f "Assets/AppIcon.icns" ]; then
    cp "Assets/AppIcon.icns" "$DEST_DIR/$APP_NAME.app/Contents/Resources/"
else
    echo "⚠️  WARNING: Assets/AppIcon.icns not found! The app will have a generic icon."
fi

if [ -f "Info.plist" ]; then
    cp "Info.plist" "$DEST_DIR/$APP_NAME.app/Contents/Info.plist"
else
    echo "❌ ERROR: Info.plist not found in project root. Build cannot complete."
    exit 1
fi

# 6. Cleanup (Remove redundant icon from binary folder if it was copied there)
if [ -f "$DEST_DIR/$APP_NAME.app/Contents/MacOS/Assets/AppIcon.icns" ]; then
    rm "$DEST_DIR/$APP_NAME.app/Contents/MacOS/Assets/AppIcon.icns"
fi

echo "=========================================="
echo "✅ BUILD SUCCESSFUL!"
echo "📍 Location: $(pwd)/$DEST_DIR/$APP_NAME.app"
echo "=========================================="

# Keeps the window open so you can see the result
read -p "Press Enter to exit..."