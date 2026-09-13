# Production manager rework (GitHub issue #5)

Reference doc for re-implementing a production manager job. Written so future work
sessions don't have to re-derive the git-archaeology and architecture research behind
it. Keep this updated (check off / amend) as steps land — it's a living roadmap, not a
one-time spec.

## Background

There is currently **no** production manager in the active codebase. Colony Manager
Redux's `ManagerJob_Production`/`ManagerTab_Production`/helpers were carried over from
the pre-Redux mod already fully commented out (dead code), and deleted outright in
commit `939bd56` ("chore: clean out unused files/code"). So this is a from-scratch
(re)implementation, informed by what the old, *working*, pre-Redux version did, but
built on the current architecture instead of ported as-is.

Reference points in git history:
- `939bd56~1` — last commit before deletion; `Source/ColonyManagerRedux/ManagerJobs/ManagerJob_Production.cs`
  etc. at this revision, but entirely commented out. Confirms the Redux-era version was
  a near-1:1 port/rename of the original, not a redesign.
- `03e85fa` — last commit touching the original, working, pre-Redux implementation
  under `Source/Manager/Production/*` (`ManagerJobProduction.cs`, `ManagerTabProduction.cs`,
  `BillGiverTracker.cs`, `MainProductTracker.cs`, `UtilitiesProduction.cs`,
  `UI/WindowBillGiverDetails.cs`). This is the actual behavioral reference.

### What the old job did

One `ManagerJob_Production` wrapped a single `Bill_Production` for one `RecipeDef`.
The player picked a recipe, configured a `Trigger_Threshold` (stock threshold +
`ThingFilter`), optionally restricted which workbenches ("bill givers") could run it,
then the mod injected/queued the bill onto eligible `Building_WorkTable`s until the
threshold was met.

Key pieces:
- **`MainProductTracker`** — resolved what a recipe "really" produces for
  threshold-watching purposes, since bills can have no listed product (smelting) or
  special products. Handled normal `recipe.products`, `SpecialProductType.Butchery`
  (with a meat-category/plasteel fallback for corpses), `SpecialProductType.Smelted`,
  and a hardcoded stonecutting special case (`"MakeStoneBlocks"` → `StoneBlocks`
  category, count 20).
- **`BillGiverTracker`** — `AssignedBillGiverOptions {All, Count, Specific}`: restrict
  to all eligible workbenches, a user-set count (optionally filtered by an Area), or a
  specific list of buildings. Tracked placed bills in a
  `Dictionary<Bill_Production, Building_WorkTable>` with a painful post-load
  re-linking step (buildings re-found by string ID matching; failures silently
  swallowed).
- **`Utilities_Production.CountPerWorker`** — split the bill's target count across
  assigned workers, with special "destructive" handling when the trigger's diff was
  negative (consuming stock rather than producing it — e.g. corpse/meat consumption
  via butchery bills).
- **`Dialog_CreateJobsForIngredients`** — opened on "Manage" for recipes with
  meaningful ingredient choices; let the player recursively pick, per required
  ingredient, a raw material and a sub-recipe, auto-creating/bumping threshold jobs for
  the whole production chain.
- **`Window_BillGiverDetails`** — minimal popup editing the bill-giver count via a raw
  text field.
- A static `GlobalWork()`/`prioritizeManual` mechanism reflectively reordered each
  workbench's `BillStack` so manager-added bills interleaved with manually-added ones.

### What to explicitly avoid porting as-is

These were flagged as hacky/fragile in the old code and should not be carried forward:
1. Reflection-based mutation of `BillStack`'s private `bills` field to reorder bills.
2. The reference-scribing workaround for `BillGiverTracker` post-load (string ID
   matching with silently swallowed exceptions).
3. `WorkTypeDef` resolution via brute-force `WorkGiverDef` scanning, including
   instantiating a phony `Building_WorkTable` if none exist yet.
4. Hardcoded special-case strings (`"MakeStoneBlocks"`, `"Plasteel"`) in
   `MainProductTracker`.
5. Skill-range UI wired directly to `Bill.allowedSkillRange` via a magic control ID.
6. Debug-only `#if DEBUG_JOBS`/`#if DEBUG_SCRIBE` blocks and bare `catch (Exception e)`
   with only debug logging.

## Current architecture grounding

The closest structurally-similar *existing* manager job is `ManagerJob_Mining`
(`Source/ColonyManagerRedux.Managers/ManagerJobs/ManagerJob_Mining.cs`) — also
threshold/count driven ("mine ore until you have X"). Use it as the template.

- **Base contract** — `Source/ColonyManagerRedux/ManagerJobs/ManagerJob.cs`. Concrete
  jobs extend `ManagerJob<TSettings, TWorkData>` and implement:
  - `protected abstract Coroutine GatherJobDataCoroutine(ManagerLog jobLog, AnyBoxed<TWorkData?> data)`
    — read-only planning phase, decides what work is needed without mutating the game.
  - `protected abstract Coroutine ExecuteJobDataCoroutine(ManagerLog jobLog, TWorkData data, Boxed<bool> workDone)`
    — applies the decisions.
  - `public abstract void CleanUp(ManagerLog? jobLog = null)` — undoes managed state
    when the job is deleted/disabled.
  - `public abstract IEnumerable<string> Targets { get; }`, `public abstract WorkTypeDef? WorkTypeDef { get; }`.
  - `public virtual bool IsValid { get; }` (override to also require the trigger to be
    non-null, e.g. Mining's `base.IsValid && TriggerThreshold != null`).
  - `public virtual void PostMake()` — seed defaults from the job's `ManagerSettings`.
  - `public override void ExposeData()` — save/load fields; re-wire trigger callbacks
    and `RestrictSupportedOps` in the `PostLoadInit` branch.
  - `public virtual int ExpectedAdditionalCount { get; }` — "pending but not yet
    realized" count layered on top of `TriggerThreshold.GetCurrentCount()` for the
    progress bar (Mining sums queued chunks + designated-but-unmined amounts; a
    production job's analog is work already queued in bills but not yet produced).
  - `JobState` (`Active`/`Completed`, from `ManagerJobState.cs`) set inside
    `GatherJobDataCoroutine` based on `TriggerThreshold.State`.

- **`Trigger_Threshold`** — `Source/ColonyManagerRedux/Triggers/Trigger_Threshold.cs`.
  API: `TargetCount`, `Op` (`LowerThan/Equals/HigherThan/NotEquals`), `ThresholdFilter`
  (a `ThingFilter`), `GetCurrentCount(cached=true)`/`GetCurrentCountCoroutine`, `State`
  (bool = "not yet at target"), `DoesCountMeetTarget(int)`, `GetDirective(int)` →
  `Directive.Increase/Decrease/Hold`, `DrawTriggerConfig(...)` for UI, and
  `RestrictSupportedOps(IReadOnlyList<Ops>)` to limit supported comparisons (Mining
  uses `Trigger_Threshold.AccumulationOnlyOps` since it can only add/remove
  designations, never actively deplete — a production job wanting "make more until X"
  semantics should do the same).

- **Gather/execute pattern** (from `ManagerJob_Mining.GatherJobDataCoroutine`):
  1. If `!TriggerThreshold.State`: mark `JobState = Completed`, plan cleanup, `yield break`.
  2. Else `JobState = Active`.
  3. Recompute current stock: `TriggerThreshold.GetCurrentCount()` plus any "in
     progress" amount not yet reflected in stock.
  4. `TriggerThreshold.GetDirective(count)` decides whether to add or remove
     work/designations.
  5. Respect `ColonyManagerReduxMod.Settings.CanAddMoreDesignations`/
     `ShouldRemoveMoreDesignations` caps.
  6. `ExecuteJobDataCoroutine` applies the plan (mutates game state).

  Mining tracks its own designations in a `List<Designation> _designations` (scribed
  via `Utilities.Scribe_Designations`), reconciles dead/foreign designations each pass,
  and cleans them up in `CleanUp()`. A production job's analog is tracking its own
  managed `Bill_Production` instances per workbench.

- **History** — `HistoryWorker<TJob>` nested class (see `ManagerJob_Mining.History`),
  overriding `GetCountForHistoryChapterCoroutine`/`GetTargetForHistoryChapterCoroutine`,
  keyed on `ManagerJobHistoryChapterDef` (XML defs like `CM_HistoryStock`). Attached via
  `CompProperties_ManagerJobHistory` (`workerClass` + `chapters`) in the job's Def XML.

- **Def registration** — `Common/Defs/ManagerDefs/Manager_Mining.xml`: a
  `ColonyManagerRedux.ManagerDef` with `defName`, `order`, `managerJobClass`,
  `managerSettingsClass`, `managerTabClass`, `iconPath`, `label`, `jobComps` (history),
  and optional `managerComps`. A production Def needs the same shape pointing at new
  `ManagerJob_Production`/`ManagerSettings_Production`/`ManagerTab_Production` classes
  plus its own history chapter defs.

- **Tab UI** — `ManagerTab_Mining.cs` extends `ManagerTab<ManagerJob_Mining>`,
  overrides `DoMainContent(Rect)`, builds sections via
  `Widgets_Section.BeginSectionColumn`/`DrawSection`/`EndSectionColumn`, and calls
  `job.TriggerThreshold.DrawTriggerConfig(...)` for the threshold UI. Manage/Delete
  buttons call `Manager.JobTracker.Add`/`Delete`.

## Staged roadmap

Each step lists what capability/UI must exist to consider it done. Deliberately not
fully fleshed out — architectural decisions for a given step should happen when that
step is actually picked up, not preemptively here.

- **Step 0 — Def-driven skeleton.** `ManagerJob_Production`, `ManagerSettings_Production`,
  `ManagerTab_Production` classes registered via a new `Manager_Production.xml`
  `ManagerDef`, same shape as Mining's. Job can be created/deleted/suspended from the
  UI and does nothing yet (empty gather/execute). **Done when:** a production job can
  be added to the job list, saved/loaded, and shows up in the manager tab bar.

- **Step 1 — MVP: single-recipe threshold job.** One `ManagerJob_Production` wraps one
  `RecipeDef` with an explicit static product list (recipes with no product list, like
  smelting, are filtered out of the recipe picker for now — that's Step 4's problem).
  `Trigger_Threshold` (restricted to `AccumulationOnlyOps`, like Mining) drives a
  target count against the recipe's product `ThingFilter`. Bill-giver scope is
  unrestricted (every eligible workbench on the map may run it) — no
  count/specific-building restriction yet. No skill-range restriction, no ingredient
  auto-chaining. Bill management approach: for each eligible workbench, ensure exactly
  one manager-owned `Bill_Production` exists and is `suspended = false` while
  `Trigger_Threshold.State` is true, `suspended = true` (or removed) once satisfied —
  avoid reimplementing the old `CountPerWorker`/reflection-based `BillStack`
  reordering; let the game's normal bill-repeat/worker-assignment logic do the rest.
  **Done when:** player picks a recipe, sets a threshold, and colonists actually keep
  the target resource stocked via that recipe across every eligible workbench, with
  save/load and a working manager-tab UI (recipe picker, `DrawTriggerConfig`,
  ingredient filter, Manage/Delete).

  **Landed:** recipe picker, `Trigger_Threshold`-driven target, and per-workbench
  managed-bill add/activate/suspend (via `Bill_Production.repeatMode = Forever` +
  toggling `suspended`, referenced by `ILoadReferenceable` across save/load — no
  reference-scribing hack needed) are implemented and unit-tested
  (`ManagerJob_Production.DecideBillAction`). No ingredient filter UI beyond what
  `DrawTriggerConfig` already exposes.

  The recipe picker went through a design iteration after initial user testing: a flat
  `FloatMenu` of every `RecipeDef` in the game doesn't scale (hundreds of entries,
  no search, overflows the screen) and a modal search dialog was still worse than
  what the old production manager did. It's now an embedded Available/Current
  tab split on the job-list column (mirroring `ManagerTab_Livestock`'s pattern) — an
  "Available" tab lists not-yet-managed recipes with a live search filter
  (`RimWorld.QuickSearchWidget`, the same widget vanilla's own bill-adding list
  uses) and a workbench sub-label per row; clicking a row creates the job with that
  recipe already set and switches to "Current" (the normal job list). Recipe is fixed
  at job creation, matching Livestock's PawnKind-at-creation model — Step 5 (recipe
  swap) is where changing it later belongs. This also structurally fixed a bug where
  "Manage!" could be clicked before a recipe was chosen, creating a useless job: since
  a job no longer exists until a recipe is picked from the Available tab, "Manage!"
  is now unreachable without one.

  The Available tab also filters out recipes with no built work table on the map
  (`ManagerTab_Production.Refresh`, checked against
  `Manager.map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>()` —
  the same source `ManagerJob_Production.GatherJobDataCoroutine` already uses to find
  eligible workbenches) — there's little point offering a recipe that can't be worked
  yet. This only filters the picker; it doesn't touch job *execution* or *import*,
  since a `ManagerJob_Production`'s `Recipe` is a scribed field set independent of the
  picker, so importing a job (e.g. from a saved template) still works for a recipe
  whose workbench hasn't been built yet — the job just won't produce anything until
  one is.

  Also fixed in passing: `Trigger_Threshold.DrawTriggerConfig`'s default label
  ("Active if: {0} {1}") was rendering the raw target count without the comparison
  operator (e.g. "Active if: 0 497" instead of "Active if: 0 < 497") because it built
  the label from a raw int instead of `TargetLabel` — every other job supplies an
  explicit label so this had gone unnoticed; Production is the first job to rely on
  the default. Fixed at the `Trigger_Threshold` level since it's a general bug, not
  Production-specific.

  **Known follow-ups, deliberately deferred rather than fixed now** (flagging here so
  they aren't lost, not because they're blocked on anything):
  - "Managed bills: N" is a placeholder status line, not real visibility into what's
    happening across workbenches — a later step should show something more useful
    (e.g. per-workbench status, matching how other jobs surface their designations).
  - `Bill_Production.repeatMode = Forever` means a bill keeps producing at full speed
    until the next `GatherJobDataCoroutine` pass notices the trigger is satisfied and
    suspends it — for fast/cheap recipes this will likely overshoot the target
    noticeably before the job catches up. `BillRepeatModeDefOf.TargetCount` (vanilla's
    own self-limiting repeat mode) was considered and rejected for Step 1 to keep bill
    lifecycle centrally driven by `Trigger_Threshold.State` like the rest of the
    codebase, but the overshoot behavior should be revisited — possibly in whichever
    step ends up refining bill-giver scope/timing (Step 2) or as its own follow-up.

  Not yet verified in a live game beyond this iteration — a human still needs to
  click through the manager tab in RimWorld to confirm the Available/Current tabs,
  search filtering, and actual bill behavior end-to-end before calling this step
  fully done.

- **Step 2 — Bill-giver scope restriction.** Reinstate the old job's three-mode
  restriction (`BillGiverTracker.AssignedBillGiverOptions`: `All`/`Count`/`Specific` at
  `03e85fa:Source/Manager/Production/BillGiverTracker.cs`), reworked for this
  architecture rather than ported as-is:
  - **All** (default) — every eligible workbench on the map participates; equivalent to
    no filter, same as Step 1's current unrestricted behavior.
  - **Area** — restrict by `Area` (`InvertArea` pair, same convention as
    `ManagerJob_Mining.MiningArea`/`InvertMiningArea`); only workbenches within (or
    outside, if inverted) the given area participate. Replaces the old `Count` mode,
    which just took the first N workbenches in map-iteration order with no player
    control over *which* N — an area gives the player that control directly.
  - **Specific** — the player explicitly checks which individual workbenches (from the
    set of eligible-by-recipe, built workbenches) participate. Needed because
    Area alone can't cleanly separate benches that sit clustered on adjacent tiles
    in the same room (e.g. two of three smithies in one workshop). Note: the old
    `Specific` mode's UI (`WindowBillGiverDetails`) was never actually finished — it's
    a stub `Window` with a raw int text field for the `Count` mode, no working
    checklist — so there's no working reference to port; this needs a fresh UI design,
    e.g. a checkbox list in a style consistent with the Available/Current tab split
    from Step 1.

  **Done when:** all three modes are selectable from the tab, default to `All`, and each
  correctly limits which workbenches receive/keep the managed bill.

  **Landed:** `ManagerJob_Production.WorkbenchAssignmentMode` (`All`/`Area`/`Specific`,
  default `All`), with `WorkbenchArea`/`InvertWorkbenchArea` (same `Area`/`InvertArea`
  convention as `ManagerJob_Mining.MiningArea`) and a `HashSet<Building_WorkTable>
  SpecificWorkbenches` for the Specific mode. The pure scope decision
  (`ManagerJob_Production.IsWorkTableInScope(mode, inArea, isSpecificallySelected)`) is
  unit-tested independent of live `Building_WorkTable`/`Area` instances, mirroring
  `DecideBillAction`'s split. `GatherJobDataCoroutine` now computes eligibility as
  "built + recipe-compatible + in scope", and treats a managed bill whose work table
  fell out of scope (area/selection changed) the same as a dead bill — it's deleted and
  forgotten via a new `ProductionWorkData.OutOfScopeBillsToRemove`, so a bench reverts to
  fully unmanaged instead of getting stuck suspended/active. `SpecificWorkbenches` is
  opportunistically pruned of destroyed/despawned buildings each gather pass. The tab
  adds a "Workbenches" section: the mode selector (`DrawAssignmentModeSelector` in
  `ManagerTab_Production.cs`) follows the same precedent as Forestry's "Clear areas |
  Wood logging" job-type row (`ManagerTab_Forestry.DrawJobType`) — one
  `Utilities.DrawToggle` cell per enum value in a single row, rather than a bespoke
  highlighted-button control — and per mode draws either the existing `AreaAllowedGUI`
  selector or a `Specific`-mode checklist (`DrawSpecificWorkbenches`). The checklist is
  a bespoke loop (not the generic `Utilities.DrawToggleDefList<T>` helper, which has no
  hover hook) so each row can pan/point the camera at its `Building_WorkTable` on
  mouseover, the same "hover to jump the camera" behavior already used by the trigger's
  target search FloatMenu (`Trigger_Threshold.DrawTriggerConfig`'s `onHover`), just
  applied to a persistent list row instead of a menu option. Confirmed via
  `git show 03e85fa` that the old mod's `Specific`-mode UI
  (`WindowBillGiverDetails`) was never actually finished (a stub `Window` with a raw
  int text field, not a checklist), so there was no working reference to port from.

  Specific work table references have no cross-map/template identity the way an Area's
  label does (`Utilities.Scribe_AreaByLabel`), so on template export/import
  (`Manager.ScribeSameMapData == false`) `SpecificWorkbenches` is deliberately not
  scribed at all — the job keeps `AssignmentMode == Specific` but the selection comes
  back empty, and the player re-picks work tables in the tab after importing (decided
  explicitly over silently falling back to `All`, to avoid an imported job silently
  becoming unrestricted).

  Verified via the RimTest Redux suite (`.vscode/build.sh 1.6` then a GABS-launched
  run watching for `TESTING END`): 386 tests passing, including the three new
  `IsWorkTableInScope` cases. Not yet verified in a live game — a human still needs to
  click through the new Workbenches section (mode switching, area selection, specific
  checkboxes, and a bench actually losing its managed bill when it falls out of scope)
  before calling this step fully done.

- **Step 3 — Skill-range restriction.** Expose `Bill.allowedSkillRange` cleanly per job
  (stored on `ManagerJob_Production`, applied to each managed bill on creation/update)
  instead of the old magic-control-ID UI hack. **Done when:** a min/max skill slider in
  the tab actually constrains who can work the bill.

  **Landed:** `ManagerJob_Production.AllowedSkillRange` (an `IntRange`, default `(0, 20)`
  matching vanilla `Bill.allowedSkillRange`'s own field default) is scribed on the job
  and pushed onto every bill it manages: set at bill creation
  (`ExecuteJobDataCoroutine`'s `WorkTablesNeedingNewBill` loop) and kept in sync
  afterwards — `GatherJobDataCoroutine` now also collects any still-live managed bill
  whose `allowedSkillRange` has drifted from the job's value into a new
  `ProductionWorkData.BillsNeedingSkillRangeUpdate`, applied by `ExecuteJobDataCoroutine`
  the same way bill activation/suspension already is. The comparison is a pure,
  unit-tested static (`ManagerJob_Production.BillNeedsSkillRangeUpdate(IntRange,
  IntRange)`), mirroring `DecideBillAction`/`IsWorkTableInScope`'s split.

  Unlike the old mod's magic-control-ID hack, the UI reuses the vanilla widget directly
  (`Verse.Widgets.IntRange`, the same one `Dialog_BillConfig` draws
  `bill.allowedSkillRange` with, `min`/`max` bounds `0`/`20`), with a `job.GetHashCode()`
  control ID — this codebase's existing convention for per-widget-instance IDs (see
  `ManagerTab_Hunting`/`ManagerTab_Forestry`'s `TipSignal` ids). The section
  (`ManagerTab_Production.DrawSkillRange`) is only added to the tab's section column
  when `job.Recipe.workSkill != null`, mirroring `Dialog_BillConfig`'s own
  `bill.recipe.workSkill != null` guard (a recipe with no work skill, e.g. some
  simple/manual recipes, has nothing for a skill range to restrict) — following the
  existing conditional-section precedent in `ManagerTab_Forestry.DoMainContent` (wrap
  the whole `DrawSection(...)` call in an `if`, rather than having the drawer return 0
  height, since `Widgets_Section.Section` draws its header/background box regardless of
  what the drawer returns).

  Verified via the RimTest Redux suite (`.vscode/build.sh 1.6` then a GABS-launched run
  watching for `TESTING END`): 388 tests passing (36 suites), including the two new
  `BillNeedsSkillRangeUpdate` cases. Not yet verified in a live game — a human still
  needs to click through the new skill-range slider (including confirming it's hidden
  for no-work-skill recipes, and that dragging it actually updates already-placed
  managed bills) before calling this step fully done.

  **Landed (expansion):** the user flagged, while reviewing Step 3, that this "shared
  bill settings" category also covers vanilla's ingredient search radius and
  store-mode (drop on floor / best stockpile / specific stockpile) — folded into this
  step rather than deferred, since it's the same category of setting as skill range.
  Added `ManagerJob_Production.IngredientSearchRadius` (`float`, default `999f` =
  unlimited, mirrors `Bill.ingredientSearchRadius`) and `StoreMode`/`StoreGroup`
  (mirrors `Bill_Production.GetStoreMode()`/`SetStoreMode`, default
  `BillStoreModeDefOf.BestStockpile`), scribed and pushed onto every managed bill the
  same way `AllowedSkillRange` is (`BillNeedsIngredientRadiusUpdate`/
  `BillNeedsStoreModeUpdate` pure comparisons, `ProductionWorkData.
  BillsNeedingIngredientRadiusUpdate`/`BillsNeedingStoreModeUpdate`). `StoreGroup` (an
  `ISlotGroup`) is scribed via the same `ILoadReferenceable`-casting pattern vanilla's
  own `Bill_Production.SaveSlotReferencable`/`LoadSlotReferencable` uses (scribe the
  zone's `.parent` when the group wraps a `Zone_Stockpile`, the group itself
  otherwise), and re-validated lazily each gather pass
  (`ManagerJob_Production.IsStoreGroupStillValid`) since — confirmed via research —
  there's no live "zone/storage removed" notification hook in this codebase to hook
  into (the analogous `Notify_AreaRemoved` pattern exists for jobs/areas, but no
  equivalent exists for `ISlotGroup`); this mirrors vanilla's own lazy
  `Bill_Production.ValidateGroup` approach.

  The tab UI mirrors vanilla's own `Dialog_BillConfig` widgets directly: a slider for
  ingredient radius (`ManagerTab_Production.DrawIngredientRadius`, same 3–100 range
  snapping to "Unlimited" at the top), and a store-mode button opening a `FloatMenu`
  (`DrawStoreMode`/`BuildStoreModeOptions`/`FillSpecificStockpileOptions`/
  `FillSlotGroupOptions`) with full vanilla parity — zones, storage buildings, *and*
  storage groups (shelves etc.), not just zones, per explicit user choice when asked.
  `FillSlotGroupOptions` also mirrors vanilla's incompatibility tagging
  (`RecipeWorkerCounter.CanPossiblyStore`): a storage destination that can't accept the
  recipe's product is listed but disabled, suffixed `" (Incompatible)"`, rather than
  silently offered. After user testing surfaced two follow-ups, all four
  skill-range/ingredient-radius/store-mode settings were consolidated into a single
  "Job settings" tab section instead of one section (with its own header) per setting —
  each sub-widget already carries its own inline label, so per-setting section headers
  were just wasted vertical space.

  Verified via the RimTest Redux suite: 392/392 tests passing at the time (36 suites,
  4 new: `MatchingIngredientRadiusNeedsNoUpdate`/`MismatchedIngredientRadiusNeedsUpdate`/
  `MatchingStoreModeNeedsNoUpdate`/`MismatchedStoreModeNeedsUpdate`). Not yet verified
  in a live game.

- **Step 4 — Pluggable product resolution.** Replace the old `MainProductTracker`
  special-casing (recipes with no explicit product, like smelting; special product
  types like butchery/stonecutting) with an extension point analogous to how
  `ManagerDef` turned job types into plug-ins, rather than a hardcoded if/switch chain
  — e.g. a resolver type/Def that maps a `RecipeDef` (+ `Bill` context) to the
  `ThingDef`/category actually being tracked, with built-ins for
  smelting/butchery/stonecutting registered the same way a third party would register
  their own. This step needs its own dedicated design pass.
  **Done when:** recipes without an explicit product list can be selected and tracked
  correctly, and the resolution mechanism itself is not a closed hardcoded set.

  **Landed:** a new extension point, `RecipeProductResolverDef`/`RecipeProductResolver`
  (`Source/ColonyManagerRedux/ProductResolvers/`), deliberately placed in the **core**
  `ColonyManagerRedux` project rather than `ColonyManagerRedux.Managers` — third-party
  mods adding their own resolver should only need to depend on the small core
  assembly, never on the concrete job implementations in Managers, mirroring exactly
  why `ManagerDef` itself lives in core while `ManagerJob_Mining`/`ManagerJob_Production`
  don't. `RecipeProductResolverDef` mirrors `ManagerDef`'s own `Type`-field +
  `ConfigErrors` + lazy `Activator.CreateInstance`-backed `Resolver` property idiom
  (same pattern as `CompProperties_ManagerJobHistory.workerClass`/`Worker`).
  `RecipeProductResolvers.ResolverFor(RecipeDef)` looks up the first registered
  resolver (ascending `order`) whose `CanResolve` returns true, via
  `DefDatabase<RecipeProductResolverDef>`.

  Four built-in resolvers, in `Source/ColonyManagerRedux.Managers/ProductResolvers/`
  (concrete implementations, not part of the extension point itself):
  - `RecipeProductResolver_Simple` — the pre-existing single-product behavior
    (`recipe.ProducedThingDef`), now expressed as the generic fallback (`order = 1000`).
  - `RecipeProductResolver_ButcherAnimals` — matches
    `RecipeWorkerCounter_ButcherAnimals`; allows `ThingCategoryDefOf.MeatRaw` (`order =
    100`).
  - `RecipeProductResolver_MakeStoneBlocks` — matches
    `RecipeWorkerCounter_MakeStoneBlocks`; allows `ThingCategoryDefOf.StoneBlocks`
    (`order = 100`).
  - `RecipeProductResolver_Smelted` — matches
    `recipe.specialProducts.Contains(SpecialProductType.Smelted)`; allows every
    `ThingDef` that is both `IsStuff` and `smeltable` (`order = 100`).

  The three category-based resolvers (`ButcherAnimals`/`MakeStoneBlocks` by
  `RecipeDef.workerCounterClass` — a `Type` equality check against vanilla's own
  `RecipeWorkerCounter` subclass field, so any modded recipe using a custom
  `RecipeWorkerCounter` subclass is automatically resolvable too, not just these two)
  resolve to a **fixed category/set**, not a per-bill/per-ingredient dynamic value — so
  no bill-creation-time "which stuff" configuration UI was needed to support them,
  unlike what the old mod's `MainProductTracker` complexity implied.

  Registered via `Common/Defs/RecipeProductResolverDefs/RecipeProductResolvers_Core.xml`.

  **Smelting was initially scoped out**, then reinstated after the user pushed back on
  the reasoning: the only actual blocker cited (no `RecipeWorkerCounter` subclass exists
  for `SpecialProductType.Smelted`, so vanilla's own `TargetCount` bill-repeat mode
  can't count its output) turned out not to apply to this mod's mechanism at all —
  `ManagerJob_Production.ExecuteJobDataCoroutine` always creates managed bills with
  `BillRepeatModeDefOf.Forever` and toggles them active/suspended itself based on
  `Trigger_Threshold.State`, exactly the "on/off, not produce-exactly-N" mechanism the
  user proposed as an alternative — it never relied on vanilla's per-bill product
  counting in the first place, for *any* recipe. The real remaining question was just
  what `ThingFilter` to populate for a `Smelted` recipe, since the actual output item
  depends on whatever gets fed into the smelter. Decompiling `Thing.SmeltProducts()`
  showed it always yields from the smelted item's (adjusted) cost list filtered to defs
  that are themselves stuff and `smeltable`, plus whatever a raw material's own
  `ThingDef.smeltProducts` lists (e.g. slag → Steel) — and cross-checking the shipped
  defs (`grep`ing `Data/**/ThingDefs*/*.xml` for `<smeltable>true</smeltable>`) confirmed
  the *stuff-and-smeltable* subset is a small, static, correct set: Steel, Plasteel,
  Silver, Gold, Uranium, Bioferrite (weapons/apparel are also flagged `smeltable=true`,
  but that flag means "eligible as smelter **input**" for them, not "possible output" —
  `IsStuff && smeltable` is what disambiguates the two uses of the same field). Recipes
  like "Smelt metal from slag" that already have a single fixed `products` entry were
  unaffected either way — they were already handled by `RecipeProductResolver_Simple`.

  Two existing call sites were switched from the direct `ProducedThingDef` check to a
  resolver lookup: `ManagerJob_Production.ConfigureThresholdTriggerFilter` (populates
  `Trigger_Threshold`'s filter via `resolver.ConfigureFilter`) and
  `ManagerTab_Production.Refresh` (the Available-recipes picker now offers any recipe
  with a registered resolver, not just `ProducedThingDef != null` ones). No changes
  needed to `Trigger_Threshold` itself (it already sums map stock against whatever
  `ThingFilter` it's given) or to bill creation (both built-in special recipes work as
  an ordinary `Bill_Production`, no extra per-bill config). `ManagerTab_Production.
  CanPossiblyStore` (the incompatibility tagging added in Step 3's expansion above) was
  also updated in the same pass to check a resolver-built filter instead of only
  `recipe.products[0]`, so butchery/stonecutting recipes now get correct
  "(Incompatible)" tagging in the store-mode picker too, instead of always being
  treated as compatible.

  Verified via the RimTest Redux suite: 404/404 tests passing (38 suites, 12 new:
  `RecipeProductResolverDefTests` for `ValidateRecipeProductResolverDefTypes`, plus
  `RecipeProductResolverTests` covering each built-in's `CanResolve`/`ConfigureFilter`).
  Not yet verified in a live game — a human still needs to confirm a butchery or
  stonecutting recipe now actually appears in the Available list, tracks stock
  correctly, and shows correct storage incompatibility tags, before calling this step
  fully done.

  **Smelting reinstated:** initially scoped out (see superseded note below), then added
  back as a fourth built-in, `RecipeProductResolver_Smelted`, after the user pushed back
  on the exclusion reasoning. The cited blocker — no `RecipeWorkerCounter` subclass for
  `SpecialProductType.Smelted`, so vanilla's own `TargetCount` bill-repeat mode can't
  count its output — turned out not to matter here, because managed bills don't use
  vanilla's per-bill target-count tracking for *any* recipe; they use
  `BillRepeatModeDefOf.Forever` plus this mod's own threshold-driven suspend/resume
  (see the "Known deficiency" note just below — this turns out to be exactly the
  mechanism in question). The only real gap was the output `ThingFilter`: decompiling
  `Thing.SmeltProducts()` showed it always draws from the smelted item's cost list,
  filtered to defs that are themselves `IsStuff && smeltable` (cross-checked against the
  shipped defs: Steel, Plasteel, Silver, Gold, Uranium, Bioferrite — weapons/apparel are
  *also* flagged `smeltable=true`, but that flag means "eligible smelter input" for
  them, not "possible output"). Registered at `order = 100`, same tier as the other two
  category resolvers. Verified via the RimTest Redux suite: 407/407 tests passing (3
  more new: `SmeltedResolverResolvesRecipeWithSmeltedSpecialProduct`/
  `SmeltedResolverDoesNotResolveRecipeWithoutSmeltedSpecialProduct`/
  `SmeltedResolverConfiguresFilterWithSmeltableStuff`).

  **Known deficiency, flagged by the user, not yet fixed:** every managed bill
  currently uses `BillRepeatModeDefOf.Forever` and is suspended/resumed by polling
  `Trigger_Threshold.State` (see `ManagerJob_Production.ExecuteJobDataCoroutine`/
  `DecideBillAction`). That was a simplification for getting the threshold-job shape
  working at all, not the intended final behavior — the user was explicit that the
  finished mechanism should instead produce *exactly* enough to fill the threshold,
  including splitting the remaining needed amount across multiple eligible work tables
  rather than letting every in-scope bench independently run "forever" against a shared
  trigger (which can overshoot: several benches can each finish an item before the next
  threshold poll notices the trigger is satisfied). This needs its own design pass —
  likely leaning on vanilla's own `TargetCount` repeat mode (and `RecipeWorkerCounter`)
  for recipes that have one, since vanilla already tracks *shared* map stock per bill
  and stops each bench independently once satisfied, which is closer to the desired
  behavior than this mod's own approximate polling loop. Recipes resolved via
  `RecipeProductResolver_Smelted` have no underlying `RecipeWorkerCounter` to lean on,
  so they'll need their own exact-counting strategy on top of whatever mechanism is
  chosen for the others. Tracked as an open item, not scheduled yet.

  **Landed (expansion) — mode toggle, Increment A:** while discussing the "Known
  deficiency" note above, the user pointed out that `ManagerJob_Production` is really
  serving two different mental models forced through one mechanism — "maintain a
  stock of this recipe's own output" (trigger == output, today's only case) vs.
  "consume a surplus of some unrelated resource" (e.g. "when cotton > 100, turn it
  into cotton dusters" — trigger and output are unrelated, and there's no target
  *amount* to hit, just an on/off condition). Forcing the second use case through the
  first's UI was a hack. Investigation confirmed the two share almost all existing
  machinery (bill-giver scope, ingredient radius, skill range, store mode, the
  gather/execute coroutine skeleton) — the only place "trigger == output" is actually
  assumed is `ConfigureThresholdTriggerFilter` — so this became a mode toggle on the
  existing job rather than a second `ManagerJob` type (which would've meant
  duplicating that machinery or extracting a shared base class anyway).

  Added `ManagerJob_Production.ProductionMode` (`MaintainStock`/`ConsumeSurplus`,
  default `MaintainStock`, scribed with a post-load `TriggerThreshold.
  RestrictSupportedOps` re-sync since the constructor always restricts to
  `MaintainStock`'s op set before the saved mode value loads). `ConfigureThresholdTriggerFilter`
  is now a no-op outside `MaintainStock` mode, so recipe changes stop clobbering a
  `ConsumeSurplus` job's independently configured filter; switching mode itself
  re-derives the filter when entering `MaintainStock` and leaves it untouched when
  entering `ConsumeSurplus` (so toggling back and forth to compare doesn't discard a
  manually configured filter). `Trigger_Threshold.RestrictSupportedOps` now also
  switches between `AccumulationOnlyOps` (`MaintainStock`, unchanged from before) and
  `AllOps` (`ConsumeSurplus`, needed for `Ops.HigherThan` — "cotton > 100").

  Tab UI: a new mode-selector row (`ManagerTab_Production.DrawModeSelector`, same
  `DrawToggle`-per-enum-value shape as `DrawAssignmentModeSelector`) above the
  Threshold section. `DrawThreshold` is now mode-conditional: `ConsumeSurplus` draws
  the existing, fully editable `Trigger_Threshold.DrawTriggerConfig` unchanged;
  `MaintainStock` draws a new, deliberately locked-down `DrawThresholdReadOnly`
  instead — same current/target readout, target-count slider, and count-all-on-map
  toggle, but without the cog icon that used to let a player override the
  auto-derived filter directly (inconsistent with "trigger == output" now that the
  mode is explicit) or the now-irrelevant "allow any threshold" toggle.
  `ManagerTab_Production.CanPossiblyStore`'s incompatibility-tagging check was also
  updated to look at the job's own `Mode`: resolver-derived filter in `MaintainStock`
  (unchanged), the job's own `TriggerThreshold.ThresholdFilter` in `ConsumeSurplus`
  (the resolver-derived filter is meaningless there, since trigger and output aren't
  the same thing).

  `ConsumeSurplus` ships fully working this increment (it reuses the existing
  `Forever`+suspend/resume mechanics almost as-is, so this was low-risk).
  `MaintainStock` stays behaviorally identical to today — the "Known deficiency"
  above isn't fixed yet, just no longer silently forced onto the surplus-consumption
  use case. That's Increment B, and during its design the user rejected switching
  `MaintainStock` bills to vanilla's `TargetCount` repeat mode (the direction the
  "Known deficiency" note above speculated about): `TargetCount` is fully
  self-governing once configured (vanilla's own `Bill_Production.ShouldDoNow`
  self-pauses via `RecipeWorkerCounter.CountProducts`), which would leave nothing for
  the manager job to actively do — contrary to this mod's whole premise. Increment B
  will instead use `BillRepeatModeDefOf.RepeatCount`, with the manager actively
  recomputing and rewriting each bill's `repeatCount` every gather pass (confirmed via
  decompiling `Bill_Production`: `RepeatCount`'s `ShouldDoNow`/`Notify_IterationCompleted`
  only ever check/decrement `repeatCount`, with no `RecipeWorkerCounter` dependency at
  all — so unlike the rejected `TargetCount` approach, it applies uniformly to every
  recipe including smelting, and the `RecipeProductResolver_Smelted`-specific caveat in
  the "Known deficiency" note above no longer applies).

  Verified via the RimTest Redux suite: 409/409 tests passing (38 suites, 2 new:
  `MaintainStockModeOnlySupportsAccumulationOps`/`ConsumeSurplusModeSupportsAllOps`).
  Not yet verified in a live game — a human still needs to click through the new mode
  toggle (including confirming the locked-down `MaintainStock` threshold widget and a
  working `ConsumeSurplus` job end-to-end) before calling this increment fully done.

  **Landed (expansion) — exact-fill scheduling, Increment B. Resolves the "Known
  deficiency" note above.** `MaintainStock` mode no longer uses `Forever`+suspend/
  resume at all; each gather pass now computes the shortfall
  (`max(0, TriggerThreshold.TargetCount - TriggerThreshold.GetCurrentCount())`) and
  splits it across every in-scope work table via two new pure functions:
  `SplitShortfall(int shortfall, int tableCount)` (even split, remainder to the first
  `shortfall % tableCount` tables, `[]` for zero/negative table count, all-zeros for a
  non-positive shortfall) and `SharesToIterations(int share, int yieldPerIteration)`
  (`ceil(share / max(1, yieldPerIteration))`). Each table's bill is then created or
  synced with `repeatMode = BillRepeatModeDefOf.RepeatCount` and `repeatCount` set to
  its computed iteration count — a hard per-poll cap on how much a single bench can
  overshoot by, replacing the old unbounded "run forever until the next poll notices
  and suspends everyone" behavior.

  `YieldPerIteration(RecipeDef recipe)` supplies the per-iteration yield: confirmed via
  decompiling `RecipeDef.ProducedThingDef` that it's non-null exactly when
  `specialProducts == null && products?.Count == 1`, so `recipe.ProducedThingDef !=
  null` is a safe guard for reading the exact yield off `recipe.products[0].count`;
  every other recipe (`_ButcherAnimals`, `_MakeStoneBlocks`, `_Smelted` — genuinely
  variable output) falls back to a conservative estimate of `1`. Since the split is
  recomputed from fresh stock counts every pass rather than trusted once, an inexact
  estimate self-corrects over successive passes instead of compounding — the same
  self-correcting property the old polling mechanism relied on, just with a much
  tighter per-poll bound.

  `BillNeedsRepeatCountUpdate(BillRepeatModeDef billRepeatMode, int billRepeatCount,
  int targetRepeatCount)` doubles as the migration path for bills placed before this
  increment (or loaded from an older save still in `Forever` mode): any bill not
  already in `RepeatCount` mode with the right count is flagged for sync, at which
  point `ExecuteJobDataCoroutine` also force-unsuspends it (`bill.suspended = false`)
  since `MaintainStock` no longer uses `suspended` at all — a `repeatCount` of `0`
  already leaves a bench idle without it. A table with no managed bill yet only gets
  one created once its computed share is actually positive, so an idle `MaintainStock`
  job (shortfall already zero) doesn't clutter every eligible bench with a no-op bill.
  `GatherJobDataCoroutine`/`ExecuteJobDataCoroutine`'s per-table loop now branches on
  `Mode`: this new split/sync logic for `MaintainStock`, the original
  `DecideBillAction`/`Forever`+suspend path unchanged for `ConsumeSurplus` (still the
  right fit there — no target amount to hit, just on/off). Bill-creation boilerplate
  shared by both paths (skill range, ingredient radius, store mode) was extracted into
  `AddManagedBill(Building_WorkTable)`; each caller still sets its own
  `repeatMode`/`repeatCount` afterward.

  Verified via the RimTest Redux suite: 424/424 tests passing (38 suites, 15 new,
  covering `SplitShortfall`'s even/remainder/zero-table/non-positive-shortfall/single-
  table/shortfall-smaller-than-table-count cases, `SharesToIterations`'s exact/
  remainder-rounds-up/zero-share/non-positive-yield cases, `BillNeedsRepeatCountUpdate`'s
  matching/mismatched-count/`Forever`-migration cases, and `YieldPerIteration`'s
  single-product vs. variable-yield cases). Not yet verified in a live game — a human
  still needs to confirm exact-fill scheduling across multiple work tables in practice,
  including a variable-yield recipe like smelting, before calling this increment fully
  done.

  **Bugfix — `ConsumeSurplus` trigger filter defaulted to the recipe's output, not its
  raw materials.** `ConfigureThresholdTriggerFilter` was a no-op whenever
  `Mode != MaintainStock`, so a freshly created job (always starting in `MaintainStock`,
  which auto-derives the filter from the recipe's *output* via
  `RecipeProductResolvers`) kept that same output-item filter untouched after the
  player switched it to `ConsumeSurplus` — e.g. a "cotton > 100 → cotton dusters" job
  would show dusters, not cotton, as the threshold item. Fixed by branching
  `ConfigureThresholdTriggerFilter` on `Mode`: `MaintainStock` is unchanged (re-derives
  from the resolver every call), while `ConsumeSurplus` now seeds the filter once (via
  a new `ConfigureIngredientFilter(RecipeDef, ThingFilter)`, unioning every
  `IngredientCount.filter.AllowedThingDefs` across `recipe.ingredients` — this covers
  fixed ingredients too, since `IngredientCount.IsFixedIngredient` is just the case
  where that filter happens to allow exactly one def) from the recipe's raw materials,
  tracked by a new `_consumeSurplusFilterInitialized` flag so repeated `Mode` toggling
  afterward never clobbers a player's manual edits to the filter. The flag resets
  whenever `Recipe` changes (so switching recipes re-seeds instead of keeping a stale
  filter). It isn't itself scribed — `_mode` can only ever reach `ConsumeSurplus`
  through the `Mode` setter (the field starts at `MaintainStock`, and `Scribe_Values`
  assigns the backing field directly, bypassing the setter), and that setter always
  seeds the filter before `ConsumeSurplus` becomes observable. So a loaded job's
  `_mode == ConsumeSurplus` already implies the seeding happened at some point (this
  session or an earlier one, with the result — possibly since edited by the player —
  already sitting in the loaded `ThresholdFilter`); `ExposeData`'s `PostLoadInit` block
  just reconstructs the flag as `_mode == ProductionMode.ConsumeSurplus` rather than
  persisting a second, redundant value. Verified via the RimTest Redux suite: 427/427
  tests passing (38 suites, 3 new, covering the ordinary-ingredient, fixed-ingredient,
  and multi-ingredient-slot union cases for `ConfigureIngredientFilter`). Not yet
  verified in a live game.

  **Landed (expansion) — separate, linked job/bill ingredient filter.** Fixing the
  `ConsumeSurplus` bug above surfaced a broader gap the user flagged: nothing ever set
  `Bill_Production.ingredientFilter` at all — `AddManagedBill` only applied
  `AllowedSkillRange`/`IngredientSearchRadius`/`StoreMode`, so every managed bill (in
  either mode) was free to consume *any* material the recipe's ingredient slots allow,
  regardless of what the trigger was actually measuring. Two concrete cases motivated
  fixing this generally rather than just for `ConsumeSurplus`: (1) `MaintainStock`
  jobs (e.g. "keep 5 steel knives in stock") had no way to restrict which materials
  fed the recipe (e.g. steel only, not silver) since `ThresholdFilter` there is
  output-typed, not ingredient-typed; (2) even within `ConsumeSurplus`, the trigger
  resource and the consumed resource aren't always the same thing — e.g. triggering
  carnivore-meal production on `hay > 1000` (to protect meat stores when hay runs low)
  while still only ever consuming meat, never hay.

  Added `ManagerJob_Production.AllowedIngredients` (`HashSet<ThingDef>`), applied to
  every managed bill's `ingredientFilter` via a new `AddManagedBill`/
  `ApplyAllowedIngredients` step and kept in sync via a new
  `BillNeedsIngredientFilterUpdate(IEnumerable<ThingDef> billAllowed, HashSet<ThingDef>
  jobAllowed)` reconciliation entry (same shape as the existing skill-range/ingredient-
  radius/store-mode sync checks). Defaults to every ingredient option for the recipe
  (a new `AllRecipeIngredientOptions(RecipeDef)`, factored out of the existing
  `ConfigureIngredientFilter`) whenever `Recipe` changes — matching an unrestricted
  vanilla bill's behavior until a player narrows it, so this is a pure addition with
  no default-behavior change.

  This is deliberately a *separate* field from `TriggerThreshold.ThresholdFilter`,
  not a reuse of it, following the same "allowed list synced with a filter" pattern
  already used by Mining (`AllowedMinerals`/`SyncFilterAndAllowed`), Foraging
  (`AllowedPlants`), Forestry, and Hunting: `Sync` (`Utilities.SyncDirection`) and
  `SyncFilterAndAllowed` fields, `Notify_ThresholdFilterChanged` (pushes threshold-
  filter edits into `AllowedIngredients`) and `SetIngredientAllowed` (pushes
  `AllowedIngredients` edits into the threshold filter), each guarded so the side that
  didn't just change doesn't fight back. Critically, this syncing is **only wired up in
  `ConsumeSurplus` mode** — in `MaintainStock`, `ThresholdFilter` is output-typed
  (what's produced) while `AllowedIngredients` is input-typed (what's consumed), so
  they're not even the same kind of list and syncing them would be meaningless;
  `AllowedIngredients` there is purely an independent, always-editable restriction.
  Turning `SyncFilterAndAllowed` off in `ConsumeSurplus` mode is exactly what the
  hay/meat example above needs: the trigger stays on hay while `AllowedIngredients`
  stays independently set to meat.

  Tab UI: a new always-visible "Ingredients" section (`Utilities.DrawToggleDefList`,
  the same checkbox-list widget Mining uses for `AllowedMinerals`), with the
  `SyncFilterAndAllowed` toggle row shown only in `ConsumeSurplus` mode (mirrors how
  `DrawThreshold` already branches its own widget on `Mode`). Following a live-UI
  complaint that a long ingredient list (e.g. every meat type) was cramped and
  scroll-heavy stacked below the other options, `ManagerTab_Production.DoMainContent`
  was restructured into Mining's two/three-column pattern: `optionsColumnRect` (3/5
  width) plus a new dedicated `ingredientsColumnRect` (2/5 width, its own
  `Widgets_Section` column keyed `Production.Ingredients`) holding just the
  ingredients list, so it scrolls independently of the rest of the job's options.

  A follow-up request added shortcut toggles above the flat ingredient list, matching
  Foraging's "All"/"Edible"/"Mushrooms" pattern — but since Production covers
  arbitrary recipes, category names can't be hardcoded. Instead, a new
  `GroupIngredientsByCategory(IEnumerable<ThingDef>)` groups a recipe's ingredient
  options by each def's own `ThingDef.thingCategories[0]` — the direct category link
  `ThingCategoryDef.childThingDefs` itself uses, i.e. "the node right above the item"
  in the category tree, not a full ancestor chain, and not a hardcoded list. A def
  with no category is simply omitted from the shortcuts (it's still in the full list);
  a def with more than one category is only grouped under the first, so a shortcut
  toggle never silently touches a def that looks like it belongs elsewhere. `ManagerTab`
  gained a second `DrawShortcutToggle` overload taking a `TaggedString` label directly
  instead of a translation key, for these runtime-derived groups (refactored to share
  its toggle-drawing logic with the existing translation-key overload via a private
  `DrawShortcutToggleCore`).

  Verified via the RimTest Redux suite: 432/432 tests passing (38 suites, 5 new,
  covering `AllRecipeIngredientOptions`'s union/dedup across ingredient slots,
  `BillNeedsIngredientFilterUpdate`'s matching/mismatched-set cases, and
  `GroupIngredientsByCategory`'s direct-category grouping including the
  multiple-categories case). Not yet verified visually in a live game.

  **Landed (expansion) — multiple jobs per recipe.** While designing the mode
  toggle above, the user floated a deferred idea: "one might want to have both
  'maintain stock' and 'consume surplus' for the same recipe" — e.g. keep 15
  steel knives around (`MaintainStock`) while also turning surplus cotton into
  cotton dusters (`ConsumeSurplus`) from the same Tailor bench recipe, as two
  independent jobs; more generally, two `ConsumeSurplus` jobs on the same
  recipe with different thresholds. This was previously impossible:
  `ManagerTab_Production.Refresh()` excluded any recipe already claimed by an
  existing job from the "add a new job" picker.

  Investigation found this exclusion was the *only* place in the codebase
  assuming "one job per recipe" — bill ownership (`_managedBills`,
  `AddManagedBill`, the gather/execute reconciliation loop) is already fully
  scoped to each job instance, matching bills to work tables via each job's
  own bill list rather than scanning a work table's whole `billStack`, and
  `JobTracker.Add`/`ExposeData` impose no uniqueness of their own. So the fix
  was just removing the `recipesInUse` filter from `Refresh()`. (Livestock has
  an analogous per-`PawnKind` exclusion, but that's a domain-specific fit — a
  population target like "keep 5 Huskies" is inherently singular, with no
  equivalent of Production's `Mode` giving two jobs on the same target a
  genuinely distinct purpose.)

  Removing the exclusion surfaced one real gap: two jobs on the same recipe
  were indistinguishable in the job list (`ManagerJob_Production` has no
  `Label` override, and its `Targets`/sub-label was just the recipe name).
  Fixed with a new `ManagerTab_Production.GetSubLabel` override — following
  `ManagerTab_Mining.GetSubLabel`'s precedent of appending detail onto
  `base.GetSubLabel(job)` — appending the job's `Mode` label and its trigger's
  current status (`TriggerThreshold.StatusTooltip`), e.g. "Cotton Dusters |
  Consume surplus (50 / > 100)" vs. "Cotton Dusters | Maintain stock (12 /
  15)".

  Verified via the RimTest Redux suite: 432/432 tests passing (38 suites, no
  new tests — the change is a filter removal plus label composition, neither
  of which introduces branching logic to unit-test, consistent with
  `GetSubLabel` overrides elsewhere in this codebase having no dedicated
  tests). Not yet verified visually in a live game.

- **Step 5 — Recipe swap ("other recipe available").** Parity with the old
  `_otherRecipeAvailable` FloatMenu (`8c58376`, "Add button to change recipe on
  bill if another recipe with the same output is available. Resolves #8" — the
  actual origin of this feature; predates the `03e85fa` reference snapshot cited
  above, so it's not visible there). **Done when:** switching recipes preserves
  the job's trigger/history/identity.

  **What the old mechanism did**, for context: each job wrapped exactly one
  `RecipeDef`. A throttled (~1000-tick cached) scan of `DefDatabase<RecipeDef>`
  looked for other recipes producing the same `MainProduct.ThingDef` with at
  least one buildable work table on the map. If any existed, the tab showed an
  "Other recipes available" row; clicking it opened a `FloatMenu` of
  alternatives (current recipe plus each candidate, labeled with which
  work tables can run it). Picking one called `SetNewRecipe`, which manually
  tore down every placed bill (`CleanUp()`), swapped `Bill` for a fresh
  `Bill_Production` on the new recipe, and rebuilt `BillGiverTracker` — while
  deliberately leaving `MainProduct`/`Trigger` (the tracked target and its
  history) untouched, since the whole point is "same target, different means
  of production."

  **This is substantially simpler to implement now than it was in the old
  mod**, because the pieces `SetNewRecipe` had to hand-roll are already load-
  bearing parts of this codebase for unrelated reasons:
  - `ManagerJob_Production.Recipe`'s setter (`ManagerJob_Production.cs`)
    *already* does everything `SetNewRecipe`/`CleanUp` did by hand: it calls
    `RemoveAllManagedBills()`, reseeds `ConfigureThresholdTriggerFilter()` and
    `ResetAllowedIngredientsToDefault()`, and fires `Notify_TargetsChanged()`
    — all while leaving `TriggerThreshold` itself (identity, `TargetCount`,
    history chapter, which is keyed off the job, not the recipe) completely
    untouched. **So "swap recipe" is just `job.Recipe = candidate;`** — no new
    plumbing needed for the actual swap, only for detection and UI.
  - Newly-placed bills for the new recipe aren't rebuilt eagerly by the swap
    itself; the very next `GatherJobDataCoroutine`/`ExecuteJobDataCoroutine`
    pass repopulates them for whichever work tables are in scope, the same
    reconciliation loop that already runs every pass for any other reason.
    That loop already ANDs "work table's def can run `Recipe`" with
    `IsWorkTableInScope(wt)` (see the eligibility check just before the
    `foreach` in `GatherJobDataCoroutine`), so even a stale `SpecificWorkbenches`
    entry left over from the old recipe is harmless — it just won't produce
    the new recipe's def, no extra pruning step required.
  - `AllowedSkillRange`/`IngredientSearchRadius`/`StoreMode`/`StoreGroup` are
    bill-config, not recipe-derived, and the `Recipe` setter correctly leaves
    them alone — they carry over across the swap as-is (the tab already hides
    the skill-range section when `Recipe.workSkill == null`, so a swap to a
    no-skill recipe degrades gracefully with no extra logic).
  - `AssignmentMode`/`WorkbenchArea`/`InvertWorkbenchArea` also carry over
    unchanged — "which work tables participate" is an orthogonal concern to
    "which recipe runs on them."

  **What actually needs building:**
  1. **Detection.** Scope this to `Mode == ProductionMode.MaintainStock` only
     — in `ConsumeSurplus` mode, trigger and output are deliberately unrelated
     (see Step 4's mode-toggle expansion above), so "another recipe with the
     same output" isn't a meaningful notion there; don't show the swap
     affordance at all when `Mode == ConsumeSurplus`. For a `MaintainStock`
     job, reuse `ManagerTab_Production`'s existing `_availableRecipes` (already
     "has a registered `RecipeProductResolver` and at least one built,
     recipe-compatible work table on the map," refreshed on `PreOpen`/tab
     selection — see `Refresh()`) as the candidate pool, rather than
     reinventing the old mod's own `DefDatabase` scan or its manual
     `_timeSinceLastOtherRecipeCheck`/`ForceRecache()` tick-based cache (that
     ad-hoc throttling predates this codebase's `Refresh()`-on-open
     convention and shouldn't be ported). Filter that pool to recipes whose
     resolved output overlaps the job's current tracked output: resolve each
     candidate via `RecipeProductResolvers.ResolverFor(recipe)!.ConfigureFilter(recipe,
     candidateFilter)` and check for any shared `AllowedThingDefs` against the
     job's own `TriggerThreshold.ThresholdFilter` (do **not** compare a single
     `ThingDef`, unlike the old `rd.products.Any(tc => tc.thingDef ==
     MainProduct.ThingDef)` check — that doesn't generalize to the
     category-based resolvers from Step 4, e.g. two different butchery-style
     recipes should both resolve to the `MeatRaw` category and match each
     other even though neither has a literal `products` entry).
  2. **UI.** A row/button in the tab, shown only when `Mode == MaintainStock`
     and the candidate set (computed above, excluding `Recipe` itself) is
     non-empty — same shape as the old "Other recipes available" row: label +
     tooltip + a small icon, opening a `FloatMenu` of candidates (each labeled
     with recipe name and which work tables can run it, mirroring the old
     mod's label format). Selecting an option is just `job.Recipe = candidate;`
     — no extra cleanup call needed, per the setter behavior above. Place it
     as its own `DrawXxx` method following the existing per-concern-method
     convention (`DrawModeSelector`, `DrawAssignmentModeSelector`,
     `DrawThreshold`/`DrawThresholdReadOnly`), most likely folded into the
     "Job settings" section from Step 3's UI consolidation, or immediately
     above/below the threshold section since it's conceptually about the same
     "what am I producing" decision.

  **Landed.** `ManagerJob_Production.RecipeSharesOutput(IEnumerable<ThingDef>
  candidateOutputs, IEnumerable<ThingDef> currentOutputs)` is the pure,
  unit-tested detection primitive — a plain set-intersection check, deliberately
  comparing whole output sets rather than a single `ThingDef` so it generalizes to
  the category-based resolvers from Step 4 (two different butchery-style recipes
  both resolve to the `MeatRaw` category and correctly match each other despite
  neither having a literal `products` entry). `ManagerTab_Production.
  ComputeRecipeSwapCandidates` does the actual candidate-pool computation (not unit
  tested, same as the tab's other DefDatabase/live-game-dependent helpers like
  `BuildStoreModeOptions`): it reuses `_availableRecipes` (the same
  built-work-table-filtered pool the Available tab's picker already computes in
  `Refresh()`, per the plan's explicit steer away from a fresh `DefDatabase` scan
  or the old mod's tick-based cache), resolves each candidate's output via
  `RecipeProductResolvers.ResolverFor`, and keeps only those overlapping the job's
  current `TriggerThreshold.ThresholdFilter.AllowedThingDefs`.

  UI: folded into the end of the existing `DrawRecipeInfo` section (not a separate
  `DrawSection`) — shown only in `MaintainStock` mode and only when the candidate
  set is non-empty, as a button opening a `FloatMenu` of alternatives (recipe name
  plus which work tables can run it, mirroring the old mod's label format and this
  tab's existing `BuildStoreModeOptions`/`FillSlotGroupOptions` FloatMenu idiom).
  Folding into `DrawRecipeInfo` rather than a standalone section avoids drawing an
  empty header/box when there's nothing to swap to (`Widgets_Section.Section`
  always renders its header/background regardless of drawer height). Picking an
  option is exactly `job.Recipe = candidateLocal` — no extra cleanup call, per the
  setter's existing behavior of tearing down/reseeding everything recipe-derived
  while leaving the trigger's identity, target count, and history untouched.

  Verified via the RimTest Redux suite: 447/447 tests passing (38 suites, 3 new:
  `RecipeSharesOutputWithOverlappingSetsReturnsTrue`/`WithDisjointSetsReturnsFalse`/
  `WithEmptyCandidateOutputsReturnsFalse`). Not yet verified visually in a live
  game — a human still needs to click through the new "Other recipes available"
  button (including confirming it's hidden in `ConsumeSurplus` mode and when no
  candidate recipe exists) before calling this step fully done.

- **Step 6 — Job linking ("ingredient auto-chaining").** The old reference
  (`Dialog_CreateJobsForIngredients`/`IngredientSelector`/`RecipeSelector`,
  decompiled at `939bd56^:Source/ColonyManagerRedux/Helpers/Production/Dialog_CreateJobsForIngredients.cs`)
  was a single modal dialog that recursively let the player pick, per ingredient
  slot, a raw material and a sub-recipe all the way down, seeding each level's
  target count with a `Math.Sqrt(count) * baseCount` heuristic. The user
  explicitly rejected porting that UI (cryptic, one-shot, disconnected from the
  job list) in favor of a different shape: let an *existing or new* job be
  **linked** as the ingredient source for another existing job's ingredient,
  discoverable/editable from the normal per-job tab UI one ingredient at a time,
  not a wizard. **Done when:** an ingredient on a job's tab can be linked to a
  production job (existing or newly created) whose output covers it, and that
  producer's target count can be automatically derived from its linked
  consumers' demand.

  **Landed.** `ManagerJob_Production.LinkedIngredientSources`
  (`Dictionary<ThingDef, ManagerJob_Production>`, keyed the same way
  `AllowedIngredients` is — a specific raw material, not an ingredient slot) is
  the core relationship, stored on the *consumer* side; the producer side has
  no reverse-index field and is resolved on demand by scanning
  `Manager.JobTracker.JobsOfType<ManagerJob_Production>()`, the same
  no-second-collection-to-keep-in-sync approach `FindOwningJob` already uses
  for bills.

  Demand is computed by two new pure functions deliberately replacing the old
  mod's `Math.Sqrt` heuristic with a transparent formula reusing existing
  machinery verbatim: `IngredientCountPerIteration(RecipeDef, ThingDef)` sums
  `IngredientCount.GetBaseCount()` across every slot allowing that def, and
  `ComputeIngredientDemand(RecipeDef, int consumerTargetCount, ThingDef)` is
  `SharesToIterations(consumerTargetCount, YieldPerIteration(recipe)) *
  IngredientCountPerIteration(...)` — the buffer needed to fully refill the
  consumer's own target from empty. Only a `MaintainStock` consumer's target
  is a bounded quantity this can be computed from; a `ConsumeSurplus`
  consumer can still link an ingredient (for traceability), it just
  contributes no demand number.

  Multiple consumers linked to the same producer are combined by
  `AggregateLinkedDemand(IEnumerable<int>, LinkedDemandAggregation)`, a new
  `Sum`/`MaxOfConsumers` enum answering the "should a producer sized for
  three consumers needing 20/40/60 target 60 or 120?" question the user
  raised: made player-configurable per producer job
  (`ManagerJob_Production.DemandAggregation`), defaulting to `Sum` — RimWorld
  can run multiple consuming bills concurrently on separate work tables, so
  `Sum` is the only option that avoids one consumer starving another, while
  `MaxOfConsumers` is offered as the deliberate "economize colonist effort,
  accept occasional contention" alternative. A new
  `AutoTargetFromLinks` bool (default `false`) gates whether a producer's
  `TriggerThreshold.TargetCount` is actually recomputed from this aggregate
  every `GatherJobDataCoroutine` pass (before the existing shortfall/split
  logic runs, so Step 4's exact-fill scheduling keeps working unmodified
  downstream) — deliberately opt-in rather than "linked implies automatic,"
  since a player may want to hand-set a higher target even while linked
  from other jobs. A job created via a link's "create new job" option
  defaults `AutoTargetFromLinks = true` (the entire point of that menu
  option); linking to an *already existing* job never touches its toggle.

  Cycle prevention: `WouldCreateCycle<TJob>(TJob consumer, TJob
  candidateProducer, Func<TJob, IEnumerable<TJob>> linkedSources)` is a
  plain DFS with a visited set, generic over the selector (same trick
  `FindOwningJob<TJob, TItem>` uses) so it's unit-testable with plain fakes
  instead of live jobs — a real `ManagerJob_Production` needs a live
  `Manager`/`Map` to construct, same constraint `FindOwningJob`'s own tests
  worked around. Any candidate that would create a cycle is simply omitted
  from the link `FloatMenu`, never surfaced as an error. `CleanUp()` was
  extended to scrub a deleted producer out of every other job's
  `LinkedIngredientSources` (iterating `JobsOfType<ManagerJob_Production>()`
  and removing matching values), so demand computation never needs a
  defensive null-check for a dangling reference.

  UI: `ManagerTab_Production.DrawIngredientList` (now an instance method,
  needed for `_availableRecipes` access) wires up
  `Utilities.DrawToggleDefList`'s previously-unused `drawExtraIcons` hook to
  draw a small chain-link icon per ingredient row — filled
  (`Resources.LinkLinked`) when linked, outlined (`Resources.LinkUnlinked`)
  when linkable but not linked, absent when no producing recipe exists for
  that def at all. Two new 64×64 SVG-sourced icons
  (`Common/Textures/UI/Icons/CMR_link_linked.png`/`CMR_link_unlinked.png`,
  neutral `#E4E1D8` stroke, generated via the standard
  `magick -density 384 ... -resize 64x64` pipeline) follow the existing
  `CMR_padlock_closed`/`_open` two-state-icon precedent. Clicking opens a
  `FloatMenu` (`BuildIngredientLinkOptions`, same idiom as
  `BuildRecipeSwapOptions`/`BuildStoreModeOptions`): unlink (if linked), link
  to an existing eligible `MaintainStock` job (`ComputeExistingProducerCandidates`,
  filtered by resolved-output match and `WouldCreateCycle`), or create a new
  job for any producing recipe (`ComputeIngredientSourceCandidates`, the
  reverse-lookup counterpart to Step 5's `ComputeRecipeSwapCandidates` —
  same "resolve via `RecipeProductResolvers`, check against `_availableRecipes`"
  shape, just testing `Allows(ingredient)` instead of set-intersecting two
  filters).

  A consuming job is not limited to a single linked producer — a recipe can
  need several ingredients, each independently linkable — so beyond the
  per-row icon, `DrawIngredientList` also appends a consolidated "linked
  from" row per active link (ingredient → producer sub-label, click-to-jump
  the job-list selection to that producer), avoiding the need to hover every
  row to reconstruct a job's whole supply picture. Symmetrically, a
  producer's own tab gets a `DrawLinkedConsumers` block folded into the
  existing `DrawThresholdReadOnly` (`MaintainStock`-only, and only rendered
  when at least one job currently links to it — same "avoid an empty
  section" reasoning Step 5 used for the recipe-swap button): the
  `AutoTargetFromLinks` toggle, the `Sum`/`MaxOfConsumers` choice (shown only
  when the toggle is on, same one-cell-per-enum-value `DrawToggle` shape
  `DrawModeSelector`/`DrawAssignmentModeSelector` already use), and a row per
  linked consumer with its individual computed demand.

  New `ManagerJobProductionTests` cases cover `IngredientCountPerIteration`
  (multi-slot sum, no-match-is-zero), `ComputeIngredientDemand`,
  `AggregateLinkedDemand` (`Sum`/`MaxOfConsumers`/empty), and
  `WouldCreateCycle` (direct cycle, transitive cycle through a chain, a
  legitimate non-cyclic chain that must *not* be rejected, and linking a job
  to itself) — the last group using a plain `FakeLinkedJob` fake, not a real
  `ManagerJob_Production`, per the constructability constraint above.

  Deliberately out of scope for this pass, but not abandoned — see Step 7
  below, added specifically so this doesn't get lost: no recursive
  "auto-create the whole chain in one click" wizard, and no per-consumer
  demand weighting (flat `Sum`/`MaxOfConsumers` only).

  Not yet verified in a live game — a human still needs to click through
  linking (both "link to existing" and "create new job"), confirm the
  auto-target number updates as a consumer's own target changes, confirm the
  `Sum`/`MaxOfConsumers` toggle changes the number with two+ consumers
  linked, confirm unlinking and deleting a linked job both behave cleanly,
  and confirm a cyclic link attempt is simply never offered in the menu,
  before calling this step fully done.

- **Step 7 — Create production chain.** Not implemented; written down here so
  the idea isn't lost after being explicitly deferred out of Step 6's scope.
  An action that starts from a job's currently-*unlinked* ingredients and
  recursively walks down, creating and linking producer jobs for the whole
  unmet chain in one action (e.g. ore → smelted metal → component from one
  starting recipe) — the recursive "auto-chain" capability the old mod's
  `Dialog_CreateJobsForIngredients` provided, but built directly on top of
  Step 6's primitives (`WouldCreateCycle`, the "create new job" link option,
  `AutoTargetFromLinks`) rather than a new bespoke dialog/data model. In other
  words: repeatedly do what Step 6's "create new job" link option already
  does, recursing into each newly created job's own unlinked ingredients —
  not a parallel mechanism requiring its own design pass for the underlying
  data model. **Done when:** a multi-step production chain can be set up from
  one starting recipe with a single action, using only Step 6's existing
  linking/demand machinery.

## Notes for future sessions

Each step should get its own focused planning/implementation pass rather than being
tackled all at once. Update this document (check off steps, amend design decisions) as
work lands, so it stays a useful map rather than going stale.
