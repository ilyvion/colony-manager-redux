// ManagerTab_Hunting.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

using System.Text;
using static ColonyManagerRedux.Constants;
using static ColonyManagerRedux.Managers.ManagerJob_Hunting;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerTab_Hunting(Manager manager) : ManagerTab<ManagerJob_Hunting>(manager)
{
    public ManagerJob_Hunting SelectedHuntingJob => SelectedJob!;

    private const string HuntingOptions = "Hunting.Options";

    protected override void DoMainContent(Rect rect)
    {
        // layout: settings | animals
        // draw background
        Widgets.DrawMenuSection(rect);

        // rects
        var optionsColumnRect = new Rect(
            rect.xMin,
            rect.yMin,
            rect.width * 3 / 5f,
            rect.height - Margin - ButtonSize.y
        );
        var animalsColumnRect = new Rect(
            optionsColumnRect.xMax,
            rect.yMin,
            rect.width * 2 / 5f,
            rect.height - Margin - ButtonSize.y
        );
        var buttonRect = new Rect(
            rect.xMax - ButtonSize.x,
            rect.yMax - ButtonSize.y,
            ButtonSize.x - Margin,
            ButtonSize.y - Margin
        );

        // options
        Widgets_Section.BeginSectionColumn(
            optionsColumnRect,
            HuntingOptions,
            out var position,
            out var width
        );
        DrawSection(
            HuntingOptions,
            "TargetResource",
            ref position,
            width,
            DrawTargetResource,
            "ColonyManagerRedux.Hunting.TargetResource".Translate()
        );
        DrawSection(
            HuntingOptions,
            "Threshold",
            ref position,
            width,
            DrawThresholdSettings,
            "ColonyManagerRedux.Threshold".Translate()
        );
        DrawSection(HuntingOptions, "UnforbidCorpses", ref position, width, DrawUnforbidCorpses);
        DrawSection(
            HuntingOptions,
            "HuntingGrounds",
            ref position,
            width,
            DrawHuntingGrounds,
            "ColonyManagerRedux.Hunting.AreaRestriction".Translate()
        );
        Widgets_Section.EndSectionColumn(HuntingOptions, position);

        // animals
        Widgets_Section.BeginSectionColumn(
            animalsColumnRect,
            "Hunting.Animals",
            out position,
            out width
        );
        var refreshRect = new Rect(
            position.x + width - SmallIconSize - (2 * Margin),
            position.y + Margin,
            SmallIconSize,
            SmallIconSize
        );
        if (Widgets.ButtonImage(refreshRect, Resources.Refresh, Color.grey))
        {
            SelectedHuntingJob.RefreshAllAnimals();
        }

        var padlockRect = new Rect(
            refreshRect.x - (SmallIconSize + 1) - (2 * Margin),
            position.y + Margin,
            SmallIconSize + 1,
            SmallIconSize
        );
        if (
            Widgets.ButtonImage(
                padlockRect,
                SelectedHuntingJob.AnimalsLockedToMap
                    ? Resources.PadlockClosed
                    : Resources.PadlockOpen,
                Color.grey
            )
        )
        {
            SelectedHuntingJob.AnimalsLockedToMap = !SelectedHuntingJob.AnimalsLockedToMap;
        }

        Widgets_Section.Section(
            ref position,
            width,
            DrawAnimalShortcuts,
            "ColonyManagerRedux.Hunting.Animals".Translate()
        );
        Widgets_Section.Section(ref position, width, DrawAnimalList);
        Widgets_Section.EndSectionColumn("Hunting.Animals", position);

        // do the button
        if (!SelectedHuntingJob.IsManaged)
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.Common.Manage".Translate()))
            {
                // activate job, add it to the stack
                SelectedHuntingJob.IsManaged = true;
                Manager.JobTracker.Add(SelectedHuntingJob);

                // refresh source list
                Refresh();
            }
        }
        else
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.Common.Delete".Translate()))
            {
                // inactivate job, remove from the stack.
                Manager.JobTracker.Delete(SelectedHuntingJob);

                // remove content from UI
                Selected = MakeNewJob();

                // refresh source list
                Refresh();
            }
        }
    }

    public float DrawAnimalList(Vector2 pos, float width)
    {
        var allowedAnimals = SelectedHuntingJob.AllowedAnimals;

        return Utilities.DrawToggleDefList(
            pos,
            width,
            SelectedHuntingJob.AllAnimals,
            allowedAnimals.Contains,
            (animalDef, allow) => SelectedHuntingJob.SetAnimalAllowed(animalDef, allow),
            animalDef => animalDef.LabelCap,
            animalDef => new TipSignal(
                GetAnimalKindTooltip(animalDef, SelectedHuntingJob.TargetResource),
                animalDef.GetHashCode()
            ),
            (rect, animalDef) => Widgets.InfoCardButton(rect, animalDef.race),
            DrawAnimalListExtraIcons
        );
    }

    private void DrawAnimalListExtraIcons(Rect toggleRect, PawnKindDef animalDef, bool allowed)
    {
        var iconRect = new Rect(
            toggleRect.xMax - (2 * (SmallIconSize + Margin)) - Margin,
            toggleRect.yMin + ((toggleRect.height - SmallIconSize) / 2),
            SmallIconSize,
            SmallIconSize
        );

        // if aggressive, draw warning icon
        if (animalDef.RaceProps.manhunterOnDamageChance >= 0.1)
        {
            var color = GUI.color;
            GUI.color = Utilities_Hunting.GetManhunterIconColor(
                allowed,
                animalDef.RaceProps.manhunterOnDamageChance
            );
            GUI.DrawTexture(iconRect, Resources.ClawIcon);
            GUI.color = color;

            iconRect.x -= Margin + SmallIconSize;
        }

        if (ModsConfig.IdeologyActive)
        {
            var atLeastOneVenerated = false;
            var allVenerated = true;
            foreach (var item in Manager.map.mapPawns.FreeColonistsSpawned)
            {
                var isVenerated = item.Ideo != null && item.Ideo.IsVeneratedAnimal(animalDef.race);
                atLeastOneVenerated |= isVenerated;
                allVenerated &= isVenerated;
            }

            if (atLeastOneVenerated)
            {
                var color = GUI.color;
                GUI.color = Utilities_Hunting.GetVeneratedIconColor(allowed, allVenerated);
                GUI.DrawTexture(iconRect, Resources.Venerated);
                GUI.color = color;

                TooltipHandler.TipRegion(
                    iconRect,
                    "ColonyManagerRedux.Hunting.VeneratedAnimal.Tip".Translate(
                        allVenerated
                            ? "ColonyManagerRedux.Misc.All".Translate()
                            : "ColonyManagerRedux.Misc.Some".Translate()
                    )
                );

                iconRect.x -= Margin + SmallIconSize;
            }
        }
    }

    private static readonly List<string> _tmpAnimalKindTooltipYields = [];

    public static string GetAnimalKindTooltip(
        PawnKindDef kind,
        HuntingTargetResource targetResource
    )
    {
        var sb = new StringBuilder();
        _ = sb.Append(kind.race.description);

        if (kind?.race?.race != null)
        {
            _ = sb.Append("\n\n");

            _tmpAnimalKindTooltipYields.Clear();

            var resourceDef =
                targetResource == HuntingTargetResource.Meat
                    ? kind.race.race.meatDef
                    : kind.race.race.leatherDef;
            if (resourceDef != null)
            {
                _tmpAnimalKindTooltipYields.Add(
                    YieldLine(kind.EstimatedYield(targetResource), resourceDef.label)
                );
            }

            // butcherProducts (only used for buildings in vanilla)
            if (!kind.race.butcherProducts.NullOrEmpty())
            {
                foreach (var product in kind.race.butcherProducts)
                {
                    _tmpAnimalKindTooltipYields.Add(
                        YieldLine(product.count, product.thingDef.label)
                    );
                }
            }

            // killedLeavings (only used for buildings in vanilla)
            if (!kind.race.killedLeavings.NullOrEmpty())
            {
                foreach (var leaving in kind.race.killedLeavings)
                {
                    _tmpAnimalKindTooltipYields.Add(
                        YieldLine(leaving.count, leaving.thingDef.label)
                    );
                }
            }

            // butcherBodyPart(s)
            if (!kind.lifeStages.NullOrEmpty())
            {
                for (var i = 0; i < kind.lifeStages.Count; i++)
                {
                    var stage = kind.lifeStages[i];
                    var part = stage?.butcherBodyPart;
                    if (part == null || stage == null || part.thing == null)
                    {
                        continue;
                    }

                    var label = stage.label;
                    if (label.NullOrEmpty())
                    {
                        label = kind.race.race.lifeStageAges[i].def.label;
                    }

                    if (part.allowFemale && !part.allowMale)
                    {
                        label += ", ";
                        label += kind.labelFemale.NullOrEmpty()
                            ? $"ColonyManagerRedux.Info.Gender.{Gender.Female}".Translate()
                            : kind.labelFemale;
                    }
                    else if (part.allowMale && !part.allowFemale)
                    {
                        label += ", ";
                        label += kind.labelMale.NullOrEmpty()
                            ? $"ColonyManagerRedux.Info.Gender.{Gender.Male}".Translate()
                            : kind.labelFemale;
                    }

                    _tmpAnimalKindTooltipYields.Add($"{part.thing.label} ({label})");
                }
            }

            if (_tmpAnimalKindTooltipYields.Count == 1)
            {
                _ = sb.AppendLine(I18n.YieldOne(_tmpAnimalKindTooltipYields.First()));
                _ = sb.AppendLine();
            }
            else if (_tmpAnimalKindTooltipYields.Count > 1)
            {
                _ = sb.AppendLine(I18n.YieldMany(_tmpAnimalKindTooltipYields));
                _ = sb.AppendLine();
            }

            _ = sb.Append(I18n.Aggressiveness(kind.race.race.manhunterOnDamageChance));
        }

        return sb.ToString();
    }

    public static string YieldLine(int count, string label) =>
        count > 1 ? $"{count}x {label}" : label;

    private readonly List<PawnKindDef> _tmpPawnKinds = [];

    public float DrawAnimalShortcuts(Vector2 pos, float width)
    {
        using var _ = new DoOnDispose(_tmpPawnKinds.Clear);

        var start = pos;

        // list of keys in allowed animals list (all animals in biome + visible animals on map)
        var allowedAnimals = SelectedHuntingJob.AllowedAnimals;
        var allAnimals = SelectedHuntingJob.AllAnimals;

        // toggle all
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        DrawShortcutToggle(
            allAnimals,
            allowedAnimals,
            (a, v) => SelectedHuntingJob.SetAnimalAllowed(a, v),
            rowRect,
            "ColonyManagerRedux.Shortcuts.All",
            null
        );

        // toggle predators
        rowRect.y += ListEntryHeight;
        _tmpPawnKinds.Clear();
        _tmpPawnKinds.AddRange(allAnimals.Where(a => a.RaceProps.predator));
        DrawShortcutToggle(
            _tmpPawnKinds,
            allowedAnimals,
            (a, v) => SelectedHuntingJob.SetAnimalAllowed(a, v),
            rowRect,
            "ColonyManagerRedux.Hunting.Predators",
            "ColonyManagerRedux.Hunting.Predators.Tip"
        );

        // aggressive animals
        rowRect.y += ListEntryHeight;
        _tmpPawnKinds.Clear();
        _tmpPawnKinds.AddRange(allAnimals.Where(a => a.RaceProps.manhunterOnDamageChance >= 0.05));
        DrawShortcutToggle(
            _tmpPawnKinds,
            allowedAnimals,
            (a, v) => SelectedHuntingJob.SetAnimalAllowed(a, v),
            rowRect,
            "ColonyManagerRedux.Hunting.Aggressive",
            "ColonyManagerRedux.Hunting.Aggressive.Tip"
        );

        // toggle herd animals
        rowRect.y += ListEntryHeight;
        _tmpPawnKinds.Clear();
        _tmpPawnKinds.AddRange(allAnimals.Where(a => a.RaceProps.herdAnimal));
        DrawShortcutToggle(
            _tmpPawnKinds,
            allowedAnimals,
            (a, v) => SelectedHuntingJob.SetAnimalAllowed(a, v),
            rowRect,
            "ColonyManagerRedux.Hunting.HerdAnimals",
            "ColonyManagerRedux.Hunting.HerdAnimals.Tip"
        );

        // exploding animals
        _tmpPawnKinds.Clear();
        _tmpPawnKinds.AddRange(
            allAnimals.Where(a =>
                a.RaceProps.deathAction.workerClass == typeof(DeathActionWorker_SmallExplosion)
                || a.RaceProps.deathAction.workerClass == typeof(DeathActionWorker_BigExplosion)
            )
        );

        if (_tmpPawnKinds.Count > 0)
        {
            rowRect.y += ListEntryHeight;
            DrawShortcutToggle(
                _tmpPawnKinds,
                allowedAnimals,
                (a, v) => SelectedHuntingJob.SetAnimalAllowed(a, v),
                rowRect,
                "ColonyManagerRedux.Hunting.Exploding",
                "ColonyManagerRedux.Hunting.Exploding.Tip"
            );
        }

        return rowRect.yMax - start.y;
    }

    public float DrawHuntingGrounds(ManagerJob_Hunting job, Vector2 pos, float width)
    {
        var start = pos;
        AreaAllowedGUI.DoAllowedAreaSelectors(ref pos, width, ref job.HuntingGrounds, 5, Manager);
        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.InvertArea".Translate(),
            "ColonyManagerRedux.InvertArea.Tip".Translate(),
            ref job.InvertHuntingGrounds
        );
        return pos.y - start.y;
    }

    public float DrawTargetResource(ManagerJob_Hunting job, Vector2 pos, float width)
    {
        var targetResource = (HuntingTargetResource[])Enum.GetValues(typeof(HuntingTargetResource));

        var cellWidth = width / targetResource.Length;

        var cellRect = new Rect(pos.x, pos.y, cellWidth, ListEntryHeight);

        foreach (var type in targetResource)
        {
            Utilities.DrawToggle(
                cellRect,
                $"ColonyManagerRedux.Hunting.TargetResource.{type}".Translate(),
                $"ColonyManagerRedux.Hunting.TargetResource.{type}.Tip".Translate(),
                job.TargetResource == type,
                () => job.TargetResource = type,
                () => { },
                wrap: false
            );
            cellRect.x += cellWidth;
        }

        return ListEntryHeight;
    }

    public float DrawThresholdSettings(ManagerJob_Hunting job, Vector2 pos, float width)
    {
        var start = pos;

        // target count (1)
        var currentCount = job.TriggerThreshold.GetCurrentCount();
        var corpsesCache = job.GetYieldInCorpsesCache();
        _ = corpsesCache.DoUpdateIfNeeded();
        var corpseCount = corpsesCache.Value;
        var designationsCache = job.GetYieldInDesignationsCache();
        _ = designationsCache.DoUpdateIfNeeded();
        var designatedCount = designationsCache.Value;
        var targetLabel = job.TriggerThreshold.TargetLabel;

        job.TriggerThreshold.DrawTriggerConfig(
            ref pos,
            width,
            ListEntryHeight,
            "ColonyManagerRedux.Hunting.TargetCount".Translate(
                currentCount,
                corpseCount,
                designatedCount,
                targetLabel
            ),
            "ColonyManagerRedux.Hunting.TargetCountTooltip".Translate(
                currentCount,
                corpseCount,
                designatedCount,
                targetLabel
            ),
            job.Designations,
            delegate
            {
                job.Sync = Utilities.SyncDirection.FilterToAllowed;
            },
            job.DesignationLabel
        );

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.SyncFilterAndAllowed".Translate(),
            "ColonyManagerRedux.Hunting.SyncFilterAndAllowed.Tip".Translate(),
            ref job.SyncFilterAndAllowed
        );

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.Threshold.PathBasedDistance".Translate(),
            "ColonyManagerRedux.Threshold.PathBasedDistance.Tip".Translate(),
            ref job.UsePathBasedDistance,
            true
        );
        Utilities.DrawReachabilityToggle(ref pos, width, ref job.ShouldCheckReachable);

        if (job.TargetResource == HuntingTargetResource.Meat)
        {
            // allow human & insect meat (2)
            Utilities.DrawToggle(
                ref pos,
                width,
                "ColonyManagerRedux.Hunting.AllowHumanMeat".Translate(),
                "ColonyManagerRedux.Hunting.AllowHumanMeat.Tip".Translate(),
                job.AllowAllHumanLikeMeat,
                job.AllowNoneHumanLikeMeat,
                () => job.AllowHumanLikeMeat = true,
                () => job.AllowHumanLikeMeat = false
            );

            Utilities.DrawToggle(
                ref pos,
                width,
                "ColonyManagerRedux.Hunting.AllowInsectMeat".Translate(),
                "ColonyManagerRedux.Hunting.AllowInsectMeat.Tip".Translate(),
                job.TriggerThreshold.ThresholdFilter.Allows(ManagerThingDefOf.Meat_Megaspider),
                () => job.AllowInsectMeat = true,
                () => job.AllowInsectMeat = false
            );

            if (ModsConfig.AnomalyActive)
            {
                Utilities.DrawToggle(
                    ref pos,
                    width,
                    "ColonyManagerRedux.Hunting.AllowTwistedMeat".Translate(),
                    "ColonyManagerRedux.Hunting.AllowTwistedMeat.Tip".Translate(),
                    job.TriggerThreshold.ThresholdFilter.Allows(ManagerThingDefOf.Meat_Twisted),
                    () => job.AllowTwistedMeat = true,
                    () => job.AllowTwistedMeat = false
                );
            }
        }

        return pos.y - start.y;
    }

    public float DrawUnforbidCorpses(ManagerJob_Hunting job, Vector2 pos, float width)
    {
        var start = pos;

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.Hunting.UnforbidCorpses".Translate(),
            "ColonyManagerRedux.Hunting.UnforbidCorpses.Tip".Translate(),
            ref job.UnforbidCorpses
        );

        if (job.UnforbidCorpses)
        {
            Utilities.DrawToggle(
                ref pos,
                width,
                "ColonyManagerRedux.Hunting.UnforbidAllCorpses".Translate(),
                "ColonyManagerRedux.Hunting.UnforbidAllCorpses.Tip".Translate(),
                ref job.UnforbidAllCorpses
            );

            if (job.UnforbidAllCorpses)
            {
                Utilities.DrawToggle(
                    ref pos,
                    width,
                    "ColonyManagerRedux.Hunting.UnforbidHumanCorpses".Translate(),
                    "ColonyManagerRedux.Hunting.UnforbidHumanCorpses.Tip".Translate(),
                    ref job.UnforbidHumanCorpses
                );
            }
        }

        return pos.y - start.y;
    }

    public override void PreOpen() => Refresh();

    protected override void Refresh()
    {
        // update pawnkind options
        foreach (var job in Manager.JobTracker.JobsOfType<ManagerJob_Hunting>())
        {
            job.RefreshAllAnimals();
        }

        SelectedHuntingJob?.RefreshAllAnimals();
    }
}
