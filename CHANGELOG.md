# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

-   Job designations are now saved in the save file so job histories don't get messed up each time the game is loaded.
-   Every non-game-specific aspect (specific pawns, specific storage areas, etc.) of a manager job can now have its default values configured in the mod's settings.

### Changed

-   Manager tabs are now defined using ManagerDefs; this makes it much easier for third party mods to add additional tabs without having to resort to patching.
-   Newly created jobs are now marked as immediately needing to be updated by managers. This avoids having to wait as long as the update interval before it is tended to. A setting has been added to make it work as before, if desirable.

### Fixed

-   History labels now store translation keys instead of finished translations so that changing the UI language also changes the label values where appropriate.
-   Threshold details window wasn't working properly, but is now fixed.

[Unreleased]: https://github.com/ilyvion/colony-manager-redux/compare/pre-redux...HEAD
