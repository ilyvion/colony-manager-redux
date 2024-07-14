# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

-   Job designations are now saved in the save file so job histories don't get messed up each time the game is loaded

### Changed

-   Manager tabs are now defined using ManagerTabDefs; this makes it much easier for third party mods to add additional tabs.

### Fixed

-   History labels now store translation keys instead of finished translations so that changing the UI language also changes the label values where appropriate.

[Unreleased]: https://github.com/ilyvion/colony-manager-redux/compare/pre-redux...HEAD
