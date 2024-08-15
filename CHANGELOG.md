# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

-   Job designations are now saved in the save file so job histories don't get messed up each time the game is loaded.
-   Every non-game-specific aspect (specific pawns, specific storage areas, etc.) of a manager job can now have its default values configured in the mod's settings.
-   Reinvented/reimplemented import/export feature that existed in a very early version of the original Fluffy's Colony Manager.
-   History now records targets as well, so they render with changes over time just like values do
-   The inline legend in history graphs are now interactive and can be clicked to show/hide that chapter or be right-clicked to hide every chapter but the right-clicked one.
-   Added trainability icons and aggression icons to the available livestock animal list.
-   When a job is selected in the overview tab, show the workers for that job in the work panel.
-   Show progress bars on livestock tab.
-   Show an alert when an AI manager has been constructed and there are still manager's desks constructed.
-   Made it so that you can have overrides of default values per animal type for the Livestock jobs.
-   Added expected resource icons to the available livestock animal list.
-   Mining jobs can now designate resulting chunks for hauling automatically. Whether to do this is controllable through a setting in the mining job.
-   Jobs can be forced to update immediately.
-   Show an alert if a player configures both auto-slaughter (RimWorld feature) and 'butcher excess' (Colony Manager feature) for the same animal type.
-   Power management now adds its own job once it's been unlocked by research. This job is responsible for counting up the various buildings involved in power production/consumption/storage in the colony.
-   Attempting to import an exported job list with a different mod list now produces the same kind of warning as other save/load features in the game.
-   Recording historical data can now be disabled, which might help with performance.
-   Setting for continuing to tame animals past targets for the Livestock jobs.
-   Setting for mining thick roofs for Resource Gathering jobs.
-   Jobs now log what they've done, which can be reviewed at a later time. Adds a new tab next to the overview tab for this purpose.
-   Resource Gathering jobs now avoid deconstructing the ancient danger by default. This can be overridden with a setting.
-   Manager job should fail if all jobs suddenly become paused for whatever reason.

### Changed

-   Manager tabs are now defined using ManagerDefs; this makes it much easier for third party mods to add additional tabs without having to resort to patching.
-   Newly created jobs are now marked as immediately needing to be updated by managers. This avoids having to wait as long as the update interval before it is tended to. A setting has been added to make it work as before, if desirable.
-   Added comp support to ManagerDefs using the ManagerJobComp as the base comp class.
-   Job history chapters are now defined using ManagerJobHistoryChapterDefs; this was done so that the ManagerDefs could have a CompManagerJobHistory be responsible for recording history.
-   Use a proper PawnTable for rendering pawn details in the overview tab rather than a custom table.
-   Suspended job stamp now has priority over job completed/not completed stamps.
-   Show progress bars even when a job is suspended/completed.
-   AI manager gives off a bit of heat.
-   AI manager now costs 750 W when doing work, but only 250 W when idle.
-   Use a proper PawnTable for rendering animal details in the livestock tab rather than a custom table.
-   Manager job is now a higher priority job (placed between Warden and Handle). Managing is essential for running the colony well, so it being behind research in default priority makes it rather unlikely to happen in a busy colony. A player can always manually make it a lower priority job if they so desire.
-   The list of stockpiles to pick from in the threshold trigger now splits them up into rows so you don't get an impossibly narrow selection box for each stockpile if you have more than a few.
-   Mining jobs have been modified to handle chunks differently. They no longer directly allow using chunks as a resource to configure thresholds on, chunks are now instead considered a resource from which to gather stone, much like mining and building deconstruction already was. Among other changes, this means that mining jobs can now automatically mark chunks for hauling as the threshold requirements require.
-   Mining has been renamed to Resource Gathering, which is a more broadly applying description of that job type.
-   The UI layout of jobs has been reworked to be more flexible/less rigid.
-   The rule for which suffix to use in graphs now differs between the y axis and the chapter values; this lets you do things like have both power producers/consumers (using W) and batteries (using Wd) in the same graph having the right units.
-   New graphics for the basic manager desk.
-   Guest animals are not included in target counts for livestock, but are still managed by job settings for things like training and area restrictions.
-   Unforbid corpses before designating hunting; already dead animals are easier/faster/safer source of food and the unforbidding was previously gated behind the check for huntable animals, meaning that if there were no animals to hunt, no corpses would be unforbidden either.
-   Job state changes (active/completed) now happens as part of managerial work and not magically whenever the threshold changes. This also means that jobs that enter their completed state can do cleanup of their designations, to prevent already set designations from making the stock going way above targets post-completion.
-   The alerts for missing managers and work tables now feature useful actions on click.
-   When we allow slaughtering trained animals, prioritize slaughtering the least trained ones first.
-   Moved our own ManagerDef classes into its own project so they can't accidentally access internal features which would give them an advantage over third-party implementations. This way we make sure all the features required for making the various features are correctly accessible from the outside.
-   Base the filters for the resource gathering job on the actual available minerals and materials on the map, not hard-coded thing categories.
-   The list of areas to pick from in the area selectors now splits them up into rows so you don't get an impossibly narrow selection box for each area if you have more than a few.

### Fixed

-   History labels now store translation keys instead of finished translations so that changing the UI language also changes the label values where appropriate.
-   Threshold details window wasn't working properly, but is now fixed.
-   Numerous minor interactivity and rendering bugs in the history graph and the power tab
-   The power tab now properly saves and loads its history
-   Render the training job selectors over multiple lines (3 jobs per line) so they're not so crowded, which is especially relevant if mods add additional TrainableDefs or uses a UI language more verbose than English.
-   It is no longer possible to attempt to assign a master to animals that cannot be trained in guarding/obedience.
-   Make all rendering work properly even if the "Disable tiny font" setting is enabled.
-   Don't rely on a static field to know whether power has been researched. It causes issues if you start a new game without restarting the game first.
-   Logic for detecting whether training had been assigned was backwards.
-   Various caches used game-specific values that would persist between saves/loads and even different games that led to various odd/hard to understand bugs. These caches have been made to be per-game instance instead.
-   Properly handle areas that are in use being deleted by setting them to null/unrestricted.

[Unreleased]: https://github.com/ilyvion/colony-manager-redux/compare/pre-redux...HEAD
