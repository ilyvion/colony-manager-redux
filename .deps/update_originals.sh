#!/bin/bash
set -e

RIMWORLD_STEAM_WORKSHOP_FOLDER_PATH="$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/workshop/content/294100"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

for original in "$SCRIPT_DIR/originals"/*/; do
    MOD_NAME=$(basename "$original")

    # Remove old backup, if any
    rm -rf "$SCRIPT_DIR/backups/$MOD_NAME"

    # Make new backup
    mv "$SCRIPT_DIR/originals/$MOD_NAME" "$SCRIPT_DIR/backups/$MOD_NAME"

    # Copy new version over
    cp -r "$RIMWORLD_STEAM_WORKSHOP_FOLDER_PATH/$MOD_NAME" "$SCRIPT_DIR/originals/$MOD_NAME"
done
