// ManagerTab_Hunting.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Text;
using static ColonyManagerRedux.Constants;
using static ColonyManagerRedux.Managers.ManagerJob_Hunting;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerTab_Hunting(Manager manager) : ManagerTab<ManagerJob_Hunting>(manager)
{
    public ManagerJob_Hunting SelectedHuntingJob => SelectedJob!;

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
            rect.height - Margin - ButtonSize.y);
        var animalsColumnRect = new Rect(
            optionsColumnRect.xMax,
            rect.yMin,
            rect.width * 2 / 5f,
            rect.height - Margin - ButtonSize.y);
        var buttonRect = new Rect(
            rect.xMax - ButtonSize.x,
            rect.yMax - ButtonSize.y,
            ButtonSize.x - Margin,
            ButtonSize.y - Margin);


        // options
        Widgets_Section.BeginSectionColumn(
            optionsColumnRect, "Hunting.Options", out Vector2 position, out float width);
        Widgets_Section.Section(
            ref position,
            width,
            DrawTargetResource,
            "ColonyManagerRedux.Hunting.TargetResource".Translate());

        Widgets_Section.Section(ref position, width, DrawThresholdSettings, "ColonyManagerRedux.Threshold".Translate());
        Widgets_Section.Section(ref position, width, DrawUnforbidCorpses);
        Widgets_Section.Section(ref position, width, DrawHuntingGrounds,
            "ColonyManagerRedux.Hunting.AreaRestriction".Translate());
        Widgets_Section.EndSectionColumn("Hunting.Options", position);

        // animals
        Widgets_Section.BeginSectionColumn(animalsColumnRect, "Hunting.Animals", out position, out width);
        var refreshRect = new Rect(
            position.x + width - SmallIconSize - 2 * Margin,
            position.y + Margin,
            SmallIconSize,
            SmallIconSize);
        if (Widgets.ButtonImage(refreshRect, Resources.Refresh, Color.grey))
        {
            SelectedHuntingJob.RefreshAllAnimals();
        }

        Widgets_Section.Section(ref position, width, DrawAnimalShortcuts, "ColonyManagerRedux.Hunting.Animals".Translate());
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
        var start = pos;
        // list of keys in allowed animals list (all animals in biome + visible animals on map)
        var allowedAnimals = SelectedHuntingJob.AllowedAnimals;
        var allAnimals = SelectedHuntingJob.AllAnimals;

        // toggle for each animal
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        foreach (var animalDef in allAnimals)
        {
            // draw the toggle
            Utilities.DrawToggle(rowRect, animalDef.LabelCap,
                new TipSignal(GetAnimalKindTooltip(animalDef, SelectedHuntingJob.TargetResource),
                    animalDef.GetHashCode()), allowedAnimals.Contains(animalDef),
                () => SelectedHuntingJob
                    .SetAnimalAllowed(animalDef, !allowedAnimals.Contains(animalDef)));

            Rect iconRect = new Rect(
                rowRect.xMax - 2 * (SmallIconSize + Margin) - Margin,
                rowRect.yMin + (rowRect.height - SmallIconSize) / 2,
                SmallIconSize,
                SmallIconSize);

            // if aggressive, draw warning icon
            if (animalDef.RaceProps.manhunterOnDamageChance >= 0.1)
            {
                var color = GUI.color;
                if (allowedAnimals.Contains(animalDef))
                {
                    if (animalDef.RaceProps.manhunterOnDamageChance > 0.25)
                    {
                        GUI.color = Color.red;
                    }
                    else
                    {
                        GUI.color = Resources.Orange;
                    }
                }
                else
                {
                    GUI.color = Color.gray;
                }
                GUI.DrawTexture(iconRect, Resources.ClawIcon);
                GUI.color = color;

                iconRect.x -= Margin + SmallIconSize;
            }

            if (ModsConfig.IdeologyActive)
            {
                bool atLeastOneVenerated = false;
                bool allVenerated = true;
                foreach (Pawn item in Manager.map.mapPawns.FreeColonistsSpawned)
                {
                    var isVenerated =
                        item.Ideo != null && item.Ideo.IsVeneratedAnimal(animalDef.race);
                    atLeastOneVenerated |= isVenerated;
                    allVenerated &= isVenerated;
                }

                if (atLeastOneVenerated)
                {
                    var color = GUI.color;
                    if (allowedAnimals.Contains(animalDef))
                    {
                        if (allVenerated)
                        {
                            GUI.color = Color.red;
                        }
                        else
                        {
                            GUI.color = Resources.Orange;
                        }
                    }
                    else
                    {
                        GUI.color = Color.gray;
                    }
                    GUI.DrawTexture(iconRect, Resources.Venerated);
                    GUI.color = color;

                    TooltipHandler.TipRegion(iconRect,
                        "ColonyManagerRedux.Hunting.VeneratedAnimal.Tip".Translate(
                            allVenerated
                                ? "ColonyManagerRedux.Misc.All".Translate()
                                : "ColonyManagerRedux.Misc.Some".Translate()
                        ));

                    iconRect.x -= Margin + SmallIconSize;
                }
            }

            rowRect.y += ListEntryHeight;
        }

        return rowRect.yMin - start.y;
    }

    public static string GetAnimalKindTooltip(PawnKindDef kind, HuntingTargetResource targetResource)
    {
        var sb = new StringBuilder();
        sb.Append(kind.race.description);

        if (kind?.race?.race != null)
        {
            sb.Append("\n\n");

            List<string> yields = [];

            var resourceDef = targetResource == HuntingTargetResource.Meat
                ? kind.race.race.meatDef
                : kind.race.race.leatherDef;
            if (resourceDef != null)
            {
                yields.Add(YieldLine(kind.EstimatedYield(targetResource), resourceDef.label));
            }

            // butcherProducts (only used for buildings in vanilla)
            if (!kind.race.butcherProducts.NullOrEmpty())
            {
                foreach (var product in kind.race.butcherProducts)
                {
                    yields.Add(YieldLine(product.count, product.thingDef.label));
                }
            }

            // killedLeavings (only used for buildings in vanilla)
            if (!kind.race.killedLeavings.NullOrEmpty())
            {
                foreach (var leaving in kind.race.killedLeavings)
                {
                    yields.Add(YieldLine(leaving.count, leaving.thingDef.label));
                }
            }

            // butcherBodyPart(s)
            if (!kind.lifeStages.NullOrEmpty())
            {
                for (int i = 0; i < kind.lifeStages.Count; i++)
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

                    yields.Add($"{part.thing.label} ({label})");
                }
            }

            if (yields.Count == 1)
            {
                sb.AppendLine(I18n.YieldOne(yields.First()));
                sb.AppendLine();
            }
            else if (yields.Count > 1)
            {
                sb.AppendLine(I18n.YieldMany(yields));
                sb.AppendLine();
            }

            sb.Append(I18n.Aggressiveness(kind.race.race.manhunterOnDamageChance));
        }

        return sb.ToString();
    }

    public static string YieldLine(int count, string label)
    {
        return count > 1 ? $"{count}x {label}" : label;
    }

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
            null);

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
            "ColonyManagerRedux.Hunting.Predators.Tip");

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
            "ColonyManagerRedux.Hunting.Aggressive.Tip");

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
            "ColonyManagerRedux.Hunting.HerdAnimals.Tip");

        // exploding animals
        _tmpPawnKinds.Clear();
        _tmpPawnKinds.AddRange(allAnimals.Where(
            a => a.RaceProps.deathAction.workerClass == typeof(DeathActionWorker_SmallExplosion)
                || a.RaceProps.deathAction.workerClass == typeof(DeathActionWorker_BigExplosion)));

        if (_tmpPawnKinds.Count > 0)
        {
            rowRect.y += ListEntryHeight;
            DrawShortcutToggle(
                _tmpPawnKinds,
                allowedAnimals,
                (a, v) => SelectedHuntingJob.SetAnimalAllowed(a, v),
                rowRect,
                "ColonyManagerRedux.Hunting.Exploding",
                "ColonyManagerRedux.Hunting.Exploding.Tip");
        }

        return rowRect.yMax - start.y;
    }

    public float DrawHuntingGrounds(Vector2 pos, float width)
    {
        var start = pos;
        AreaAllowedGUI.DoAllowedAreaSelectors(ref pos, width, ref SelectedHuntingJob.HuntingGrounds, 5, Manager);
        return pos.y - start.y;
    }

    public float DrawTargetResource(Vector2 pos, float width)
    {
        var targetResource =
            (HuntingTargetResource[])
            Enum.GetValues(typeof(HuntingTargetResource));

        var cellWidth = width / targetResource.Length;

        var cellRect = new Rect(
            pos.x,
            pos.y,
            cellWidth,
            ListEntryHeight);

        foreach (var type in targetResource)
        {
            Utilities.DrawToggle(
                cellRect,
                $"ColonyManagerRedux.Hunting.TargetResource.{type}".Translate(),
                $"ColonyManagerRedux.Hunting.TargetResource.{type}.Tip".Translate(),
                SelectedHuntingJob.TargetResource == type,
                () => SelectedHuntingJob.TargetResource = type,
                () => { },
                wrap: false);
            cellRect.x += cellWidth;
        }

        return ListEntryHeight;
    }

    public float DrawThresholdSettings(Vector2 pos, float width)
    {
        var start = pos;

        // target count (1)
        var currentCount = SelectedHuntingJob.TriggerThreshold.GetCurrentCount();
        var corpsesCache = SelectedHuntingJob.GetYieldInCorpsesCache();
        corpsesCache.DoUpdateIfNeeded();
        var corpseCount = corpsesCache.Value;
        var designationsCache = SelectedHuntingJob.GetYieldInDesignationsCache();
        designationsCache.DoUpdateIfNeeded();
        var designatedCount = designationsCache.Value;
        var targetCount = SelectedHuntingJob.TriggerThreshold.TargetCount;

        SelectedHuntingJob.TriggerThreshold.DrawTriggerConfig(ref pos, width, ListEntryHeight,
            "ColonyManagerRedux.Hunting.TargetCount".Translate(
                currentCount, corpseCount, designatedCount, targetCount),
            "ColonyManagerRedux.Hunting.TargetCountTooltip".Translate(
                currentCount, corpseCount, designatedCount, targetCount),
            SelectedHuntingJob.Designations,
            delegate { SelectedHuntingJob.Sync = Utilities.SyncDirection.FilterToAllowed; },
            SelectedHuntingJob.DesignationLabel);

        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.SyncFilterAndAllowed".Translate(),
            "ColonyManagerRedux.Hunting.SyncFilterAndAllowed.Tip".Translate(),
            ref SelectedHuntingJob.SyncFilterAndAllowed);

        Utilities.DrawToggle(ref pos, width, "ColonyManagerRedux.Threshold.PathBasedDistance".Translate(),
            "ColonyManagerRedux.Threshold.PathBasedDistance.Tip".Translate(), ref SelectedHuntingJob.UsePathBasedDistance, true);
        Utilities.DrawReachabilityToggle(ref pos, width, ref SelectedHuntingJob.ShouldCheckReachable);

        if (SelectedHuntingJob.TargetResource == HuntingTargetResource.Meat)
        {
            // allow human & insect meat (2)
            Utilities.DrawToggle(ref pos, width, "ColonyManagerRedux.Hunting.AllowHumanMeat".Translate(),
                "ColonyManagerRedux.Hunting.AllowHumanMeat.Tip".Translate(),
                SelectedHuntingJob.AllowAllHumanLikeMeat,
                SelectedHuntingJob.AllowNoneHumanLikeMeat,
                () => SelectedHuntingJob.AllowHumanLikeMeat = true,
                () => SelectedHuntingJob.AllowHumanLikeMeat = false);
            Utilities.DrawToggle(ref pos, width, "ColonyManagerRedux.Hunting.AllowInsectMeat".Translate(),
                "ColonyManagerRedux.Hunting.AllowInsectMeat.Tip".Translate(),
                SelectedHuntingJob.TriggerThreshold.ThresholdFilter.Allows(ManagerThingDefOf.Meat_Megaspider),
                () => SelectedHuntingJob.AllowInsectMeat = true,
                () => SelectedHuntingJob.AllowInsectMeat = false);
        }

        return pos.y - start.y;
    }

    public float DrawUnforbidCorpses(Vector2 pos, float width)
    {
        var start = pos;

        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.Hunting.UnforbidCorpses".Translate(),
            "ColonyManagerRedux.Hunting.UnforbidCorpses.Tip".Translate(),
            ref SelectedHuntingJob.UnforbidCorpses);

        if (SelectedHuntingJob.UnforbidCorpses)
        {
            Utilities.DrawToggle(ref pos, width,
                "ColonyManagerRedux.Hunting.UnforbidAllCorpses".Translate(),
                "ColonyManagerRedux.Hunting.UnforbidAllCorpses.Tip".Translate(),
                ref SelectedHuntingJob.UnforbidAllCorpses);
        }

        return pos.y - start.y;
    }

    public override void PreOpen()
    {
        Refresh();
    }

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
