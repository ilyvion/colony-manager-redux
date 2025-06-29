#!/bin/bash
set -e

CONFIGURATION="Debug"
TARGET="$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/RimWorld/Mods/ColonyManagerRedux"

export RimWorldSteamWorkshopFolderPath="../../.deps/refs"

mkdir -p .savedatafolder/1.6
mkdir -p .savedatafolder/1.5
mkdir -p .savedatafolder/1.4

# build dlls
export RimWorldVersion="1.5"
dotnet build --configuration "$CONFIGURATION" ColonyManagerRedux.sln
export RimWorldVersion="1.6"
dotnet build --configuration "$CONFIGURATION" ColonyManagerRedux.sln

# remove mod folder
rm -rf "$TARGET"

# copy mod files
mkdir -p "$TARGET"
cp -r 1.5 "$TARGET/1.5"
cp -r 1.6 "$TARGET/1.6"

# copy interop mod files
cp -r 1.5_AnimalGenetics "$TARGET/1.5_AnimalGenetics"
cp -r 1.6_AnimalGenetics "$TARGET/1.6_AnimalGenetics"
cp -r Common "$TARGET/Common"
cp -r Common_AnimalGenetics "$TARGET/Common_AnimalGenetics"

mkdir -p "$TARGET/About"
cp About/About.xml "$TARGET/About/"
cp About/Preview.png "$TARGET/About/"
cp About/ModIcon.png "$TARGET/About/"
cp About/PublishedFileId.txt "$TARGET/About/"

cp CHANGELOG.md "$TARGET/"
cp LICENSE "$TARGET/"
#cp LICENSE.Apache-2.0 "$TARGET/"
#cp LICENSE.MIT "$TARGET/"
cp README.md "$TARGET/"
cp LoadFolders.xml "$TARGET/"

# Trigger auto-hotswap
mkdir -p "$TARGET/1.5/Assemblies"
touch "$TARGET/1.5/Assemblies/ColonyManagerRedux.dll.hotswap"
mkdir -p "$TARGET/1.6/Assemblies"
touch "$TARGET/1.6/Assemblies/ColonyManagerRedux.dll.hotswap"
