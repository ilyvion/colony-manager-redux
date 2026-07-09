# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Mining jobs now have "lock to map" toggles for the allowed minerals and allowed buildings lists, matching Foraging, Forestry, and Hunting. Enable them to only show mineral/building types actually present on the current map instead of every one known to the game.

### Changed

- In the mod settings, the adjustment buttons for building a custom update interval now stay right-aligned instead of shifting left or right as the duration text next to them changes width.

### Fixed

- Adjusting a gene bias slider in the animal genetics settings could cause the other genes' sliders to no longer add up to a full 100% share over repeated adjustments, subtly skewing which genes got preference over time. Rebalancing now keeps the full set of sliders summing to 100% correctly.
- Threshold jobs with a hit points filter set (e.g. "count only items above 75% condition") were counting items backwards: damaged items matching the filter were skipped, while items that didn't match were counted. Stockpile counts using an HP filter should now reflect the items you actually configured it to count.
- Custom update interval durations shown in the mod settings could silently drop smaller units once a bigger one was involved — e.g. displaying "1 year, 2 quadrums" when the actual interval was 1 year, 2 quadrums, 3 days, and 4 hours. Durations are now shown in full so you can always tell exactly what interval you've configured.
- Slider widgets no longer lag when rendering.
- A livestock job's cached "should this job be active" check was discarding its own cached result and always reporting "yes, still needs to run" instead, even once animal targets were fully met and training was set up. It now correctly reports the cached result.
- Importing a save that resulted in three or more power manager jobs ending up on the same map (e.g. after merging colonies) could crash the import instead of quietly deduplicating down to one job, as intended.
- AAAA-integrated danger-mode jobs could crash when switching danger levels if the previous allowed area's name contained characters like parentheses or brackets (e.g. an area named "Zone (1)").
- Fixed an AAAA-integration error log that named the wrong field when warning about a misconfigured boolean field, which could send anyone troubleshooting a bad AAAA patch chasing the wrong setting.
- If AAAA area-evacuation hit an unexpected error partway through swapping a job's allowed areas, the job could be left with an incomplete, corrupted set of allowed areas instead of either the old or new set. It's now restored to its previous areas if the swap fails.

## [0.14.7] - 2026-07-09

### Fixed

- Mining jobs using a threshold type other than 'at least' (e.g. 'exactly', 'more than', or 'not equal to') would keep removing haul-to-storage designations for mined chunks well past the intended target count, since the check that stops removal only understood 'at least' thresholds.
- The power manager could crash (or silently drop a valid power trader/battery building) after removing the last building of a given type, or after a mod list change reduced the number of relevant building types, due to an off-by-one error when trimming its internal building lists.
- Fixed a potential crash for manager jobs added by other mods that don't attach any extra job components.
- Importing jobs would attach them to whichever map was currently active instead of the map whose manager tab you opened the import dialog from, if you switched maps while the import dialog was still open.
- The livestock manager was quietly accumulating a small memory leak the longer you kept playing: internal caches used to speed up master/milking/shearing/follower lookups kept an entry for every animal or colonist ever tracked, even after they died, instead of cleaning those up. They're now pruned periodically so long play sessions don't keep growing in memory usage from this.
- In the mod settings, deleting a custom update interval from the list could cause the entry right below it to briefly not show up until you reopened the settings menu.
- Deep drill mining jobs would stop managing newly built deep drills once the resource quota was already met: any drill placed on a fresh deposit after that point would keep running instead of being switched off, letting stockpiles overshoot the quota until it dropped back below target on its own. Fixes [#35](https://github.com/ilyvion/colony-manager-redux/issues/35).
- Livestock 'send to culling area' was moving the entire herd of a given age/sex group into the culling area, including bonded pets, pregnant animals, and milkers/shearers you'd explicitly told it to avoid culling — instead of only the specific animals actually selected for culling. Fixes [#33](https://github.com/ilyvion/colony-manager-redux/issues/33).

## [0.14.6] - 2026-04-03

### Fixed

- Power manager should no longer risk being interrupted while enumerating buildings and batteries, which caused an exception when it happened.

## [0.14.5] - 2025-09-10

### Fixed

- Jobs imported after gravship landing weren't properly initialized.

## [0.14.4] - 2025-09-03

### Fixed

- Don't include gravship component on 1.5 and remove superfluous workTableRoomRole which is already set by BenchBase parent.

## [0.14.3] - 2025-09-02

### Fixed

- Properly handle when 'keep log count' setting is zero.

## [0.14.2] - 2025-09-01

### Fixed

- Add translation text for setting to show info card buttons

## [0.14.1] - 2025-09-01

### Fixed

- Restore accidentally broken Manager Database gravship facility functionality.

## [0.14.0] - 2025-08-31

### Added

- Compatibility with the AAAA mod: If the AAAA mod is loaded, all area settings for jobs get a new toggle that lets them participate in AAAA's evacuation mode, i.e. changing areas named 'Area' to 'Area#safe' (it respects the suffix set in its settings; isn't hardcoded to '#safe'). Closes [#1](https://github.com/ilyvion/colony-manager-redux/issues/1).
- Task priority order for resource gathering jobs. Closes [#9](https://github.com/ilyvion/colony-manager-redux/issues/9).
- Info card buttons for plants, trees, animals, and minerals in the various manager tabs. Can be turned off again with a mod setting.
- When 'synchronize threshold' is enabled and a resource gathering job has the relevant resources marked in the threshold filter settings, enabling 'deconstruct buildings' will automatically select the buildings that contain resources that match the threshold settings. Closes [#10](https://github.com/ilyvion/colony-manager-redux/issues/10).
- Setting to restrict animals that are fully trained for livestock jobs. Closes [#13](https://github.com/ilyvion/colony-manager-redux/issues/13).
- Functionality to avoid culling milkable and shearable livestock with adjustable thresholds. Closes [#14](https://github.com/ilyvion/colony-manager-redux/issues/14).
- Culling by sterilization in Livestock manager. Culled animals are not counted towards the target, meaning that with this setting enabled, the target represents 'unsterilized' animals, not total animals. Closes [#15](https://github.com/ilyvion/colony-manager-redux/issues/15).

## [0.13.2] - 2025-08-29

### Fixed

- Handle null harvestedThingDef in forestry job processing (introduced when we restored plants without harvest yield in 0.13.0)

## [0.13.1] - 2025-08-29

### Fixed

- Adjust row position for Take Ownership of Mining Jobs toggle in settings so it doesn't overlap with the Allow Mining toggle.

## [0.13.0] - 2025-08-28

### Added

- Added deep drill controlling to resource gathering job. When enabled, these jobs will now flick drills on to increase their resource counts and off when the resource counts are met. Closes [#6](https://github.com/ilyvion/colony-manager-redux/issues/6).
- Along with the above, also added a setting to make it possible to turn off mining on resource jobs.
- Managing spot for being able to do managing tasks even without having any resources. It's very slow compared to the better options. It's researchable and finished at start by neolithic and 'classic' factions. Logic has been added to retroactively grant this research to existing saves. Closes [#28](https://github.com/ilyvion/colony-manager-redux/issues/28).

### Changed

- Make separate section for deconstructible buildings in Resource Gathering UI.
- Resource gathering job won't mark things for mining or deconstruction that isn't reachable (i.e. no more marking things in 'inner corners' for mining/deconstruction.)
- The basic managing desk is now researchable and finished at start by 'classic' factions. Logic has been added to retroactively grant this research to existing saves.
- Managing workspaces now support tool cabinets for a small boost in productivity.
- Managing desks are now paintable.
- Because it's now possible to be without any possible researched manager workspaces, the alert for missing a manager desk now offers to take you to the research tab if you're in that situation.

### Fixed

- Prevent overflow when sum of power production/consumption/battery storage exceeds int.MaxValue. Presumably fixes [#21](https://github.com/ilyvion/colony-manager-redux/issues/21).
- Plants without harvest yield had accidentally been removed from the clear areas forestry job. These are now back. Fixes [#22](https://github.com/ilyvion/colony-manager-redux/issues/22).
- Remove destroyed or dead pawns from livestock caches before considering them for various operations.
- When choosing masters for animals, don't use cached follower numbers. Also, sort the list randomly to make the selection less regular. Fixes [#24](https://github.com/ilyvion/colony-manager-redux/issues/24).
- The logic was inverted for 'equals' and 'not equals' job threshold conditions.
- Manager workspace managing speed is now affected by worktable efficiency factor, such as being built outdoors or being in uncomfortable temperatures. This wasn't properly accounted for up until this point.

## [0.12.4] - 2025-08-13

### Fixed

- Respect setting for verbose logging

## [0.12.3] - 2025-08-04

### Fixed

- Improve error handling for removed PawnKindDefs in Livestock jobs.

## [0.12.2] - 2025-07-30

### Fixed

- Fix logic error in GetForestryPlants.
- Check for null ThingDef in CountProductsCoroutine. I don't think it's supposed to happen, but I got a bug report where it did, so it doesn't hurt to add a check against.
- Cache the result of the query over CompPowerTraders in RefreshCompLists, so we're not enumerating over it across multiple ticks.

## [0.12.1] - 2025-07-30

### Fixed

- Make sure that when the power job gets interrupted, its flag for avoiding doing a subtask multiple time gets properly cleared. This should hopefully remedy the jobs-never-finishing bug that's been plaguing us lately.

## [0.12.0] - 2025-07-29

### Added

- Setting and logic for printing verbose information about manager jobs in an attempt to figure out a bug with jobs never finishing.

### Fixed

- Add explicit 'loadAfter' rule for Odyssey.

## [0.11.1] - 2025-07-27

### Fixed

- Don't throw an exception if the request for 'give me all the buildings on the map' returns a null for whatever reason.

## [0.11.0] - 2025-07-26

### Added

- Performance settings. Players can now configure how many operations a management job should do each tick, as well as add a number of ticks to pause between each set of operations to reduce the load on the game. In addition to global/default settings, advanced settings can also be accessed where these can be configured on a per-task basis.

### Fixed

- Colonists now fill the cell in the manager job overview table.
- Can be trained logic updated to work with Odyssey's new specialty trainables.

## [0.10.0] - 2025-07-20

### Fixed

- Power tracking history bug fixed; should no longer constantly warn about history being updated with an incorrect number of chapters.

## [0.9.0] - 2025-07-19

### Added

- The forestry tab now has shortcuts that lets you filter trees by their tree category: mini, full and super.
- The foraging, forestry and hunting jobs now allow you to unlock all resources instead of only showing those that are relevant to the map.

### Changed

- Areas are now saved by name when transferred (import/export and gravship map change). This means that jobs with areas loaded in maps with the same areas present will be re-assigned these new areas by name instead of always being set to 'Unrestricted.'

### Fixed

- Power tracking now works correctly on a per-map basis and also tolerates map changes properly.

## [0.8.0] - 2025-07-18

### Added

- Restored an improved version of the original Colony Manager's threshold filter. You can now enable a setting to filter not only on items directly relevant to the job, but on any item. This lets you set up jobs based on criteria not directly related to the job's outcome, such as enabling jobs when a secondary product runs low instead of when the raw ingredient runs low.
- Concurrently with the above, the threshold comparison operator has been restored from the original Colony Manager. You can now enable jobs not just based on having less than a target, but also more than a target, equal to a target or not equal to a target.
- Settings for disabling alerts and for configuring the parameters of the outdated jobs alert.
- Setting for defining your own custom update intervals for jobs.

### Changed

- Use built-in verbose time string display system instead of custom one.

### Fixed

- "Any stockpile" is now a localizable string.
- Mining filter is properly limited now unless the any item mode from above is enabled.
- Livestock training selector region doesn't have a bunch of empty space anymore.

## [0.7.0] - 2025-07-15

### Added

- Manager Database gravship component has been added which upon the gravship's launch copies a map's manager jobs onto the ship, and upon the gravship's landing, copies the ship's stored jobs back to the new map.

### Changed

- Shrink the icon for the AI Manager in the production menu so it fits within the box.

## [0.6.1] - 2025-07-13

### Fixed

- The AI Manager Station would clip through things behind it due to its abnormal size. The building has been changed from a 2x1 to a 2x2 building.
- Attempt to fix issue with Gathering Resource tab.

## [0.6.0] - 2025-06-29

### Added

- When the Animal Genetics mod is active, allow overriding Colony Manager's usual mechanism for choosing which animal(s) to tame or cull with using preferences around its genetics to decide.
- Rimworld 1.6 support.

## [0.5.4] - 2024-09-27

### Fixed

- Add an additional check for humanlike races that claim to produce meat actually do. Believing the race properties without checking explicitly it can lead to a NullReferenceException.

## [0.5.3] - 2024-09-24

### Fixed

- Add a check for whether a humanlike race that has organic flesh actually produces meat. The assumption that that's the case is apparently not universal and failing to check it can lead to a NullReferenceException.

## [0.5.2] - 2024-09-22

### Fixed

- Don't keep iterating over listerThings.AllThings across multiple ticks; there's a high risk that it could change from one tick to the next, which would cause 'Collection was modified' exceptions.

## [0.5.1] - 2024-09-18

### Fixed

- Removing mods that have buildings consuming/producing power lead to a constant stream of warnings in the log due to a mismatch between the expected building type count and the actual building type count. Colony Manager now properly discards buildings that no longer exist, which should fix the issue.

## [0.5.0] - 2024-09-15

### Dependencies

- ilyvion's Laboratory: v0.13
    > [!IMPORTANT]  
    > This release requires an update to ilyvion's Laboratory!

### Added

- Catch exceptions that happen when MangerSettings are instantiated.
- Setting for limiting the number of designations a job can manage at one time.
- Properly count the resources produced by buildings in the active designations list on the resource gathering tab.
- Added nuzzle icon to the available livestock animal list when a given animal has nuzzle behavior, with information about how often it nuzzles.
- Added twisted meat quick toggle to the hunting job to go along with insect meat and human meat when the Anomaly DLC is active.
- Animation/effect when working the manager's desk (same as when doing research).
- Explicit support for Survivalist's Additions' turnip plants. The way these plants work is really odd and they need special handling to be useful when managed by Colony Manager. A player requested support, so it's been added.

### Changed

- Made more counting logic (i.e. current designations and relevant resources for a given job type) multi-tick operations to reduce per-tick performance cost.
- Hard code the presence of human meat, insect meat and, if the anomaly DLC is present, twisted meat in the hunting threshold filter.

### Fixed

- The code responsible for not deconstructing ancient dangers wasn't working at all. Works now!
- The logic behind the "count human meat" and "count insect meat" toggles for hunting jobs wasn't quite right. Now it works like it should.
- Only show huntable pawns in the hunting tab list

## [0.4.4] - 2024-09-14

### Fixed

- Handle the situation where a ThingDef used as a HistoryLabel doesn't have a valid label.

## [0.4.3] - 2024-09-11

### Fixed

- Don't list non-huntable pawns in the hunting tab animal list.
- No meats or leathers were being listed in the threshold filters on the hunting tab due to a bug.
- Hunting tab animal list tooltips were showing meat yields even when leather was the chosen resource.
- Butchered body parts can be misconfigured to produce a null thing. While technically a bug in the other mod, it causes us to throw an exception, so handle it.

## [0.4.2] - 2024-09-07

### Fixed

- There was some error in the logic for designating in the resource gathering job which led to various bizarre behaviors like marking unmined stone for deconstruction and constantly re-designating already existing desigations for hauling/deconstruction/mining that have now been corrected.

## [0.4.1] - 2024-09-07

### Fixed

- Attempt to fix bug reported by player who experienced that the hunting job started causing exceptions. It appears to have been caused by either meatDef or leatherDef being null on some animals, so make a null check before setting either as allowed.

## [0.4.0] - 2024-09-04

### Added

- Colony Manager Redux research tab now has a title and description.
- Show an alert if jobs aren't being updated in a reasonable amount of time.
- Added a gizmo to the manager buildings that takes you to the manager tab.
- When the Ideology DLC is active, the hunting and livestock tabs will show warnings on animals who at least one colonist venerates.
- Refresh button added to forestry tab.
- Setting to allow the resource gathering job to assume ownership of mining jobs. Disabled by default as it could be very frustrating behavior for somebody not prepared for it.

### Changed

- Make the sorting of potential targets for various jobs a multi-tick operation. This should help substantially with performance when on large maps or when enabling the 'Calculate distance based on actual path' setting.
- The way job exceptions were rendered wasn't very nice. It's been improved substantially now, and also includes a "copy to clipboard" button now.
- Livestock jobs no longer get marked as complete. They are a bit complicated to reasonably determine completeness for, and the earlier logic was definitely not right.
- Jobs will now reduce the number of designations so that the number achieved at the end is only slightly higher than the target.

### Fixed

- Livestock tab's main section was blank when no animal was selected; made it look a bit nicer.
- Due to an oversight, chunks that are processed by smelting (such as steel slag chunks) were not being marked for collection when the 'Designate chunks on the map for hauling' setting was enabled.
- The mechanism to skip a history update was missing the escape hatch so it queued them up anyway.
- Animals and plants weren't being sorted properly in their respective lists. Animals could also show up as duplicated.
- When using path based distance, a thing's map can sometimes be null; this would cause an exception.
- Include all pawn kinds, not just ones from animals, otherwise players can't select things like human meat when said pawn kinds are available.
- Recalculate treshold filters on refresh. By not doing this, any new kinds of resources that became available would not show up in lists and threshold filters.
- Only count corpses' resources when they match the threshold filter for hunting jobs.
- When leather was chosen as the target resource, the Hunting tab was still showing counts for meat.
- The resource gathering job for detecting designations didn't catch designations that weren't initiated by its own processes.
- Don't attempt to count the yield of plants that have despawned.

## [0.3.0] - 2024-08-24

### Dependencies

- ilyvion's Laboratory: v0.11
    > [!IMPORTANT]  
    > This release requires an update to ilyvion's Laboratory!

### Added

- FinalizeInit methods added to jobs and manager comps
- You can now cull your excess animals by releasing them into the wild instead of slaughtering them. This can be handy if you're playing with an ideoligion that causes your pawns to frown upon animal cruelty of any kind or one that celebrates releasing animals.
- Managers can now be hidden in the settings. This is only visual; their functionality will still remain even if hidden.
- Setting in hunting jobs to unforbid all corpses, not just animals selected for hunting. Since corpes (typically) can't fight back, it can often be safe to collect those even when hunting the same animals isn't.

### Fixed

- Jobs whose managers get interrupted in their work for whatever reasons now stop running as soon as it happens; before this they would run to completion even when interrupted, which isn't very appropriate.
- The synchronize logic fix from 0.2.0 wasn't correctly implemented for the resource gathering job, but has been fixed now.

## [0.2.4] - 2024-08-21

### Fixed

- A player has reported an error where one of their jobs had become null after a load. This shouldn't be possible, but since it happened anyway, let's code so that we can at least recover from it if it does happen.

## [0.2.3] - 2024-08-21

### Fixed

- A player has reported an error where their jobs list had become null. This shouldn't be possible, but since it happened anyway, let's code so that we can at least recover from it if it does happen.
- When the setting 'Mine thick roofs' was disabled, attempting to check whether or not the mod could mine cells without roofs would cause an exception. This has been remedied.

## [0.2.2] - 2024-08-21

### Fixed

- Discovered and fixed another source of exceptions in Forestry jobs. Hopefully this is the last one. 🤞

## [0.2.1] - 2024-08-21

### Fixed

- Forgot to make sure a certain operation doesn't happen during load which caused an exception in any Forestry jobs on load.

## [0.2.0] - 2024-08-20

### Dependencies

- ilyvion's Laboratory: v0.6
    > [!IMPORTANT]  
    > This release requires an update to ilyvion's Laboratory!

### Added

- Major performance optimization/overhaul #1: Manager jobs now spread their work across multiple ticks rather than trying to do everything in a single tick. This should massively improve any hiccups/TPS issues that were caused by these jobs trying to do too much at a time.
- Major performance optimization/overhaul #2: Manager job history trackers now spread their work across multiple ticks rather than trying to do everything in a single tick. This should massively improve any hiccups/TPS issues that were caused by these history trackers trying to do too much at a time.
- The hunting job can now focus on leather as the target resource instad of meat.

### Changed

- Don't hard code wood as the only possible resource produced by the forestry job. It now handles multiple different kinds of tree products by reading them out of the actual plants on the map rather than just hardcoding it.

### Fixed

- When looking at the yields of things that had multiple resources, there'd be an extra dash at the beginning of the list. This is now gone.
- If you opened the overview tab before your colonists had landed in a new game, you'd get an exception. This has been fixed.
- Synchronize threshold logic wasn't quite right; if you allowed one thing and disallowed another that produced the same resource, the filter would be removed. Now the logic only removes the filter if _no_ selected things produce a given resource.

## [0.1.5] - 2024-08-18

### Changed

- Make use of the culling feature of GUIScope.ScrollView so we don't spend resources on rendering something that isn't even on screen. This improves performance a lot when there are a large number of logs in the log list.

### Fixed

- The log message produced when the Livestock job was taming past targets was incorrect and has been corrected.

## [0.1.4] - 2024-08-18

### Added

- Added iconPath to MainButtonDef for better compatibility with Vanilla Texture Expanded's usage of icons on main buttons.

### Fixed

- Assumed that a map could only have zero or one ancient dangers; that was an incorrect assumption and caused exceptions. Now supports any number of ancient dangers.

## [0.1.3] - 2024-08-16

### Changed

- With the new textures, it makes more sense for the AI manager building to be 2x1 instead of 2x2

## [0.1.2] - 2024-08-16

### Changed

- Using new textures for the work benches and AI manager based on the [[JGH] Colony Manager retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2603340242) mod. Used with permission; license unknown.

## [0.1.1] - 2024-08-16

### Removed

- Outdated translations were removed from the Languages directory and placed in OldLanguages as reference for any new translators.

## [0.1.0] - 2024-08-16

### Added

- Job designations are now saved in the save file so job histories don't get messed up each time the game is loaded.
- Every non-game-specific aspect (specific pawns, specific storage areas, etc.) of a manager job can now have its default values configured in the mod's settings.
- Reinvented/reimplemented import/export feature that existed in a very early version of the original Fluffy's Colony Manager.
- History now records targets as well, so they render with changes over time just like values do
- The inline legend in history graphs are now interactive and can be clicked to show/hide that chapter or be right-clicked to hide every chapter but the right-clicked one.
- Added trainability icons and aggression icons to the available livestock animal list.
- When a job is selected in the overview tab, show the workers for that job in the work panel.
- Show progress bars on livestock tab.
- Show an alert when an AI manager has been constructed and there are still manager's desks constructed.
- Made it so that you can have overrides of default values per animal type for the Livestock jobs.
- Added expected resource icons to the available livestock animal list.
- Mining jobs can now designate resulting chunks for hauling automatically. Whether to do this is controllable through a setting in the mining job.
- Jobs can be forced to update immediately.
- Show an alert if a player configures both auto-slaughter (RimWorld feature) and 'butcher excess' (Colony Manager feature) for the same animal type.
- Power management now adds its own job once it's been unlocked by research. This job is responsible for counting up the various buildings involved in power production/consumption/storage in the colony.
- Attempting to import an exported job list with a different mod list now produces the same kind of warning as other save/load features in the game.
- Recording historical data can now be disabled, which might help with performance.
- Setting for continuing to tame animals past targets for the Livestock jobs.
- Setting for mining thick roofs for Resource Gathering jobs.
- Jobs now log what they've done, which can be reviewed at a later time. Adds a new tab next to the overview tab for this purpose.
- Resource Gathering jobs now avoid deconstructing the ancient danger by default. This can be overridden with a setting.
- Manager job should fail if all jobs suddenly become paused for whatever reason.

### Changed

- Manager tabs are now defined using ManagerDefs; this makes it much easier for third party mods to add additional tabs without having to resort to patching.
- Newly created jobs are now marked as immediately needing to be updated by managers. This avoids having to wait as long as the update interval before it is tended to. A setting has been added to make it work as before, if desirable.
- Added comp support to ManagerDefs using the ManagerJobComp as the base comp class.
- Job history chapters are now defined using ManagerJobHistoryChapterDefs; this was done so that the ManagerDefs could have a CompManagerJobHistory be responsible for recording history.
- Use a proper PawnTable for rendering pawn details in the overview tab rather than a custom table.
- Suspended job stamp now has priority over job completed/not completed stamps.
- Show progress bars even when a job is suspended/completed.
- AI manager gives off a bit of heat.
- AI manager now costs 750 W when doing work, but only 250 W when idle.
- Use a proper PawnTable for rendering animal details in the livestock tab rather than a custom table.
- Manager job is now a higher priority job (placed between Warden and Handle). Managing is essential for running the colony well, so it being behind research in default priority makes it rather unlikely to happen in a busy colony. A player can always manually make it a lower priority job if they so desire.
- The list of stockpiles to pick from in the threshold trigger now splits them up into rows so you don't get an impossibly narrow selection box for each stockpile if you have more than a few.
- Mining jobs have been modified to handle chunks differently. They no longer directly allow using chunks as a resource to configure thresholds on, chunks are now instead considered a resource from which to gather stone, much like mining and building deconstruction already was. Among other changes, this means that mining jobs can now automatically mark chunks for hauling as the threshold requirements require.
- Mining has been renamed to Resource Gathering, which is a more broadly applying description of that job type.
- The UI layout of jobs has been reworked to be more flexible/less rigid.
- The rule for which suffix to use in graphs now differs between the y axis and the chapter values; this lets you do things like have both power producers/consumers (using W) and batteries (using Wd) in the same graph having the right units.
- New graphics for the basic manager desk.
- Guest animals are not included in target counts for livestock, but are still managed by job settings for things like training and area restrictions.
- Unforbid corpses before designating hunting; already dead animals are easier/faster/safer source of food and the unforbidding was previously gated behind the check for huntable animals, meaning that if there were no animals to hunt, no corpses would be unforbidden either.
- Job state changes (active/completed) now happens as part of managerial work and not magically whenever the threshold changes. This also means that jobs that enter their completed state can do cleanup of their designations, to prevent already set designations from making the stock going way above targets post-completion.
- The alerts for missing managers and work tables now feature useful actions on click.
- When we allow slaughtering trained animals, prioritize slaughtering the least trained ones first.
- Moved our own ManagerDef classes into its own project so they can't accidentally access internal features which would give them an advantage over third-party implementations. This way we make sure all the features required for making the various features are correctly accessible from the outside.
- Base the filters for the resource gathering job on the actual available minerals and materials on the map, not hard-coded thing categories.
- The list of areas to pick from in the area selectors now splits them up into rows so you don't get an impossibly narrow selection box for each area if you have more than a few.

### Fixed

- History labels now store translation keys instead of finished translations so that changing the UI language also changes the label values where appropriate.
- Threshold details window wasn't working properly, but is now fixed.
- Numerous minor interactivity and rendering bugs in the history graph and the power tab
- The power tab now properly saves and loads its history
- Render the training job selectors over multiple lines (3 jobs per line) so they're not so crowded, which is especially relevant if mods add additional TrainableDefs or uses a UI language more verbose than English.
- It is no longer possible to attempt to assign a master to animals that cannot be trained in guarding/obedience.
- Make all rendering work properly even if the "Disable tiny font" setting is enabled.
- Don't rely on a static field to know whether power has been researched. It causes issues if you start a new game without restarting the game first.
- Logic for detecting whether training had been assigned was backwards.
- Various caches used game-specific values that would persist between saves/loads and even different games that led to various odd/hard to understand bugs. These caches have been made to be per-game instance instead.
- Properly handle areas that are in use being deleted by setting them to null/unrestricted.

[Unreleased]: https://github.com/ilyvion/colony-manager-redux/compare/v0.14.7...HEAD
[0.14.7]: https://github.com/ilyvion/colony-manager-redux/compare/v0.14.6..v0.14.7
[0.14.6]: https://github.com/ilyvion/colony-manager-redux/compare/v0.14.5..v0.14.6
[0.14.5]: https://github.com/ilyvion/colony-manager-redux/compare/v0.14.4..v0.14.5
[0.14.4]: https://github.com/ilyvion/colony-manager-redux/compare/v0.14.3..v0.14.4
[0.14.3]: https://github.com/ilyvion/colony-manager-redux/compare/v0.14.2..v0.14.3
[0.14.2]: https://github.com/ilyvion/colony-manager-redux/compare/v0.14.1..v0.14.2
[0.14.1]: https://github.com/ilyvion/colony-manager-redux/compare/v0.14.0..v0.14.1
[0.14.0]: https://github.com/ilyvion/colony-manager-redux/compare/v0.13.2..v0.14.0
[0.13.2]: https://github.com/ilyvion/colony-manager-redux/compare/v0.13.1..v0.13.2
[0.13.1]: https://github.com/ilyvion/colony-manager-redux/compare/v0.13.0..v0.13.1
[0.13.0]: https://github.com/ilyvion/colony-manager-redux/compare/v0.12.4..v0.13.0
[0.12.4]: https://github.com/ilyvion/colony-manager-redux/compare/v0.12.3..v0.12.4
[0.12.3]: https://github.com/ilyvion/colony-manager-redux/compare/v0.12.2..v0.12.3
[0.12.2]: https://github.com/ilyvion/colony-manager-redux/compare/v0.12.1..v0.12.2
[0.12.1]: https://github.com/ilyvion/colony-manager-redux/compare/v0.12.0..v0.12.1
[0.12.0]: https://github.com/ilyvion/colony-manager-redux/compare/v0.11.1..v0.12.0
[0.11.1]: https://github.com/ilyvion/colony-manager-redux/compare/v0.11.0..v0.11.1
[0.11.0]: https://github.com/ilyvion/colony-manager-redux/compare/v0.10.0..v0.11.0
[0.10.0]: https://github.com/ilyvion/colony-manager-redux/compare/v0.9.0..v0.10.0
[0.9.0]: https://github.com/ilyvion/colony-manager-redux/compare/v0.8.0..v0.9.0
[0.8.0]: https://github.com/ilyvion/colony-manager-redux/compare/v0.7.0..v0.8.0
[0.7.0]: https://github.com/ilyvion/colony-manager-redux/compare/v0.6.1..v0.7.0
[0.6.1]: https://github.com/ilyvion/colony-manager-redux/compare/v0.6.0..v0.6.1
[0.6.0]: https://github.com/ilyvion/colony-manager-redux/compare/v0.5.4..v0.6.0
[0.5.4]: https://github.com/ilyvion/colony-manager-redux/compare/v0.5.3...v0.5.4
[0.5.3]: https://github.com/ilyvion/colony-manager-redux/compare/v0.5.2...v0.5.3
[0.5.2]: https://github.com/ilyvion/colony-manager-redux/compare/v0.5.1...v0.5.2
[0.5.1]: https://github.com/ilyvion/colony-manager-redux/compare/v0.5.0...v0.5.1
[0.5.0]: https://github.com/ilyvion/colony-manager-redux/compare/v0.4.4...v0.5.0
[0.4.4]: https://github.com/ilyvion/colony-manager-redux/compare/v0.4.3...v0.4.4
[0.4.3]: https://github.com/ilyvion/colony-manager-redux/compare/v0.4.2...v0.4.3
[0.4.2]: https://github.com/ilyvion/colony-manager-redux/compare/v0.4.1...v0.4.2
[0.4.1]: https://github.com/ilyvion/colony-manager-redux/compare/v0.4.0...v0.4.1
[0.4.0]: https://github.com/ilyvion/colony-manager-redux/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/ilyvion/colony-manager-redux/compare/v0.2.4...v0.3.0
[0.2.4]: https://github.com/ilyvion/colony-manager-redux/compare/v0.2.3...v0.2.4
[0.2.3]: https://github.com/ilyvion/colony-manager-redux/compare/v0.2.2...v0.2.3
[0.2.2]: https://github.com/ilyvion/colony-manager-redux/compare/v0.2.1...v0.2.2
[0.2.1]: https://github.com/ilyvion/colony-manager-redux/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/ilyvion/colony-manager-redux/compare/v0.1.5...v0.2.0
[0.1.5]: https://github.com/ilyvion/colony-manager-redux/compare/v0.1.4...v0.1.5
[0.1.4]: https://github.com/ilyvion/colony-manager-redux/compare/v0.1.3...v0.1.4
[0.1.3]: https://github.com/ilyvion/colony-manager-redux/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/ilyvion/colony-manager-redux/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/ilyvion/colony-manager-redux/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/ilyvion/colony-manager-redux/compare/pre-redux...v0.1.0
