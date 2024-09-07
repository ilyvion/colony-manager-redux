# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

-   There was some error in the logic for designating in the resource gathering job which led to various bizarre behaviors like marking unmined stone for deconstruction and constantly re-designating already existing desigations for hauling/deconstruction/mining that have now been corrected.

## [0.4.1] - 2024-09-07

### Fixed

-   Attempt to fix bug reported by player who experienced that the hunting job started causing exceptions. It appears to have been caused by either meatDef or leatherDef being null on some animals, so make a null check before setting either as allowed.

## [0.4.0] - 2024-09-04

### Added

-   Colony Manager Redux research tab now has a title and description.
-   Show an alert if jobs aren't being updated in a reasonable amount of time.
-   Added a gizmo to the manager buildings that takes you to the manager tab.
-   When the Ideology DLC is active, the hunting and livestock tabs will show warnings on animals who at least one colonist venerates.
-   Refresh button added to forestry tab.
-   Setting to allow the resource gathering job to assume ownership of mining jobs. Disabled by default as it could be very frustrating behavior for somebody not prepared for it.

### Changed

-   Make the sorting of potential targets for various jobs a multi-tick operation. This should help substantially with performance when on large maps or when enabling the 'Calculate distance based on actual path' setting.
-   The way job exceptions were rendered wasn't very nice. It's been improved substantially now, and also includes a "copy to clipboard" button now.
-   Livestock jobs no longer get marked as complete. They are a bit complicated to reasonably determine completeness for, and the earlier logic was definitely not right.
-   Jobs will now reduce the number of designations so that the number achieved at the end is only slightly higher than the target.

### Fixed

-   Livestock tab's main section was blank when no animal was selected; made it look a bit nicer.
-   Due to an oversight, chunks that are processed by smelting (such as steel slag chunks) were not being marked for collection when the 'Designate chunks on the map for hauling' setting was enabled.
-   The mechanism to skip a history update was missing the escape hatch so it queued them up anyway.
-   Animals and plants weren't being sorted properly in their respective lists. Animals could also show up as duplicated.
-   When using path based distance, a thing's map can sometimes be null; this would cause an exception.
-   Include all pawn kinds, not just ones from animals, otherwise players can't select things like human meat when said pawn kinds are available.
-   Recalculate treshold filters on refresh. By not doing this, any new kinds of resources that became available would not show up in lists and threshold filters.
-   Only count corpses' resources when they match the threshold filter for hunting jobs.
-   When leather was chosen as the target resource, the Hunting tab was still showing counts for meat.
-   The resource gathering job for detecting designations didn't catch designations that weren't initiated by its own processes.
-   Don't attempt to count the yield of plants that have despawned.

## [0.3.0] - 2024-08-24

### Dependencies

-   ilyvion's Laboratory: v0.11
> [!IMPORTANT]  
> This release requires an update to ilyvion's Laboratory!

### Added

-   FinalizeInit methods added to jobs and manager comps
-   You can now cull your excess animals by releasing them into the wild instead of slaughtering them. This can be handy if you're playing with an ideoligion that causes your pawns to frown upon animal cruelty of any kind or one that celebrates releasing animals.
-   Managers can now be hidden in the settings. This is only visual; their functionality will still remain even if hidden.
-   Setting in hunting jobs to unforbid all corpses, not just animals selected for hunting. Since corpes (typically) can't fight back, it can often be safe to collect those even when hunting the same animals isn't.

### Fixed

-   Jobs whose managers get interrupted in their work for whatever reasons now stop running as soon as it happens; before this they would run to completion even when interrupted, which isn't very appropriate.
-   The synchronize logic fix from 0.2.0 wasn't correctly implemented for the resource gathering job, but has been fixed now.

## [0.2.4] - 2024-08-21

### Fixed

-   A player has reported an error where one of their jobs had become null after a load. This shouldn't be possible, but since it happened anyway, let's code so that we can at least recover from it if it does happen.

## [0.2.3] - 2024-08-21

### Fixed

-   A player has reported an error where their jobs list had become null. This shouldn't be possible, but since it happened anyway, let's code so that we can at least recover from it if it does happen.
-   When the setting 'Mine thick roofs' was disabled, attempting to check whether or not the mod could mine cells without roofs would cause an exception. This has been remedied.

## [0.2.2] - 2024-08-21

### Fixed

-   Discovered and fixed another source of exceptions in Forestry jobs. Hopefully this is the last one. 🤞

## [0.2.1] - 2024-08-21

### Fixed

-   Forgot to make sure a certain operation doesn't happen during load which caused an exception in any Forestry jobs on load.

## [0.2.0] - 2024-08-20

### Dependencies

-   ilyvion's Laboratory: v0.6
> [!IMPORTANT]  
> This release requires an update to ilyvion's Laboratory!

### Added

-   Major performance optimization/overhaul #1: Manager jobs now spread their work across multiple ticks rather than trying to do everything in a single tick. This should massively improve any hiccups/TPS issues that were caused by these jobs trying to do too much at a time.
-   Major performance optimization/overhaul #2: Manager job history trackers now spread their work across multiple ticks rather than trying to do everything in a single tick. This should massively improve any hiccups/TPS issues that were caused by these history trackers trying to do too much at a time.
-   The hunting job can now focus on leather as the target resource instad of meat.

### Changed

-   Don't hard code wood as the only possible resource produced by the forestry job. It now handles multiple different kinds of tree products by reading them out of the actual plants on the map rather than just hardcoding it.

### Fixed

-   When looking at the yields of things that had multiple resources, there'd be an extra dash at the beginning of the list. This is now gone.
-   If you opened the overview tab before your colonists had landed in a new game, you'd get an exception. This has been fixed.
-   Synchronize threshold logic wasn't quite right; if you allowed one thing and disallowed another that produced the same resource, the filter would be removed. Now the logic only removes the filter if _no_ selected things produce a given resource.

## [0.1.5] - 2024-08-18

### Changed

-   Make use of the culling feature of GUIScope.ScrollView so we don't spend resources on rendering something that isn't even on screen. This improves performance a lot when there are a large number of logs in the log list.

### Fixed

-   The log message produced when the Livestock job was taming past targets was incorrect and has been corrected.

## [0.1.4] - 2024-08-18

### Added

-   Added iconPath to MainButtonDef for better compatibility with Vanilla Texture Expanded's usage of icons on main buttons.

### Fixed

-   Assumed that a map could only have zero or one ancient dangers; that was an incorrect assumption and caused exceptions. Now supports any number of ancient dangers.

## [0.1.3] - 2024-08-16

### Changed

-   With the new textures, it makes more sense for the AI manager building to be 2x1 instead of 2x2

## [0.1.2] - 2024-08-16

### Changed

-   Using new textures for the work benches and AI manager based on the [[JGH] Colony Manager retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2603340242) mod. Used with permission; license unknown.

## [0.1.1] - 2024-08-16

### Removed

-   Outdated translations were removed from the Languages directory and placed in OldLanguages as reference for any new translators.

## [0.1.0] - 2024-08-16

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

[Unreleased]: https://github.com/ilyvion/colony-manager-redux/compare/v0.4.1...HEAD
[0.4.1]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.4.0...v0.4.1
[0.4.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.2.4...v0.3.0
[0.2.4]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.2.3...v0.2.4
[0.2.3]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.2.2...v0.2.3
[0.2.2]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.2.1...v0.2.2
[0.2.1]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.1.5...v0.2.0
[0.1.5]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.1.4...v0.1.5
[0.1.4]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.1.3...v0.1.4
[0.1.3]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/pre-redux...v0.1.0
