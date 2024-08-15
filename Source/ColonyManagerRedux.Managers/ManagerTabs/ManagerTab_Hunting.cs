﻿// ManagerTab_Hunting.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Text;
using static ColonyManagerRedux.Constants;

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
        Widgets_Section.BeginSectionColumn(optionsColumnRect, "Hunting.Options", out Vector2 position, out float width);
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
                new TipSignal(GetAnimalKindTooltip(animalDef),
                    animalDef.GetHashCode()), allowedAnimals.Contains(animalDef),
                () =>
                {
#pragma warning disable CA1868 // Unnecessary call to 'Contains(item)'
                    if (allowedAnimals.Contains(animalDef))
                    {
                        allowedAnimals.Remove(animalDef);
                    }
                    else
                    {
                        allowedAnimals.Add(animalDef);
                    }
#pragma warning restore CA1868 // Unnecessary call to 'Contains(item)'
                });

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

                GUI.DrawTexture(
                    new Rect(rowRect.xMax - 2 * (SmallIconSize + Margin) - Margin,
                        rowRect.yMin + (rowRect.height - SmallIconSize) / 2,
                        SmallIconSize, SmallIconSize),
                    Resources.ClawIcon);
                GUI.color = color;
            }

            rowRect.y += ListEntryHeight;
        }

        return rowRect.yMin - start.y;
    }

    public static string GetAnimalKindTooltip(PawnKindDef kind)
    {
        var sb = new StringBuilder();
        sb.Append(kind.race.description);

        if (kind?.race?.race != null)
        {
            sb.Append("\n\n");

            var yields = new List<string>
            {
                YieldLine(kind.EstimatedMeatCount(), kind.race.race.meatDef.label)
            };

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
                    if (part == null || stage == null)
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
            }
            else if (yields.Count > 1)
            {
                sb.AppendLine(I18n.YieldMany(yields));
            }

            sb.Append(I18n.Aggressiveness(kind.race.race.manhunterOnDamageChance));
        }

        return sb.ToString();
    }

    public static string YieldLine(int count, string label)
    {
        return count > 1 ? $"{count}x {label}" : label;
    }

    public float DrawAnimalShortcuts(Vector2 pos, float width)
    {
        var start = pos;

        // list of keys in allowed animals list (all animals in biome + visible animals on map)
        var allowedAnimals = SelectedHuntingJob.AllowedAnimals;
        var allAnimals = SelectedHuntingJob.AllAnimals;

        // toggle all
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        DrawShortcutToggle(allAnimals, allowedAnimals, SelectedHuntingJob.SetAnimalAllowed, rowRect,
            "ColonyManagerRedux.Shortcuts.All", null);

        // toggle predators
        rowRect.y += ListEntryHeight;
        var predators = allAnimals.Where(a => a.RaceProps.predator).ToList();
        DrawShortcutToggle(predators, allowedAnimals, SelectedHuntingJob.SetAnimalAllowed, rowRect,
            "ColonyManagerRedux.Hunting.Predators", "ColonyManagerRedux.Hunting.Predators.Tip");

        // aggressive animals
        rowRect.y += ListEntryHeight;
        var aggressive = allAnimals.Where(a => a.RaceProps.manhunterOnDamageChance >= 0.05).ToList();
        DrawShortcutToggle(aggressive, allowedAnimals, SelectedHuntingJob.SetAnimalAllowed, rowRect,
            "ColonyManagerRedux.Hunting.Aggressive", "ColonyManagerRedux.Hunting.Aggressive.Tip");

        // toggle herd animals
        rowRect.y += ListEntryHeight;
        var herders = allAnimals.Where(a => a.RaceProps.herdAnimal).ToList();
        DrawShortcutToggle(herders, allowedAnimals, SelectedHuntingJob.SetAnimalAllowed, rowRect,
            "ColonyManagerRedux.Hunting.HerdAnimals", "ColonyManagerRedux.Hunting.HerdAnimals.Tip");

        // exploding animals
        var exploding = allAnimals
            .Where(a => a.RaceProps.deathAction.workerClass == typeof(DeathActionWorker_SmallExplosion)
                        || a.RaceProps.deathAction.workerClass == typeof(DeathActionWorker_BigExplosion))
            .ToList();

        if (exploding.Count > 0)
        {
            rowRect.y += ListEntryHeight;
            DrawShortcutToggle(exploding, allowedAnimals, SelectedHuntingJob.SetAnimalAllowed, rowRect,
                "ColonyManagerRedux.Hunting.Exploding", "ColonyManagerRedux.Hunting.Exploding.Tip");
        }

        return rowRect.yMax - start.y;
    }

    public float DrawHuntingGrounds(Vector2 pos, float width)
    {
        var start = pos;
        AreaAllowedGUI.DoAllowedAreaSelectors(ref pos, width, ref SelectedHuntingJob.HuntingGrounds, 5, Manager);
        return pos.y - start.y;
    }

    public float DrawThresholdSettings(Vector2 pos, float width)
    {
        var start = pos;

        // target count (1)
        var currentCount = SelectedHuntingJob.TriggerThreshold.GetCurrentCount();
        var corpseCount = SelectedHuntingJob.GetMeatInCorpses();
        var designatedCount = SelectedHuntingJob.GetMeatInDesignations();
        var targetCount = SelectedHuntingJob.TriggerThreshold.TargetCount;

        SelectedHuntingJob.TriggerThreshold.DrawTriggerConfig(ref pos, width, ListEntryHeight,
            "ColonyManagerRedux.Hunting.TargetCount".Translate(
                currentCount, corpseCount, designatedCount, targetCount),
            "ColonyManagerRedux.Hunting.TargetCountTooltip".Translate(
                currentCount, corpseCount, designatedCount, targetCount),
            SelectedHuntingJob.Designations, null, SelectedHuntingJob.DesignationLabel);

        // allow human & insect meat (2)
        Utilities.DrawToggle(ref pos, width, "ColonyManagerRedux.Threshold.PathBasedDistance".Translate(),
            "ColonyManagerRedux.Threshold.PathBasedDistance.Tip".Translate(), ref SelectedHuntingJob.UsePathBasedDistance, true);
        Utilities.DrawReachabilityToggle(ref pos, width, ref SelectedHuntingJob.ShouldCheckReachable);
        Utilities.DrawToggle(ref pos, width, "ColonyManagerRedux.Hunting.AllowHumanMeat".Translate(),
            "ColonyManagerRedux.Hunting.AllowHumanMeat.Tip".Translate(),
            SelectedHuntingJob.TriggerThreshold.ThresholdFilter.Allows(ThingDefOf.Meat_Human),
            () => SelectedHuntingJob.AllowHumanLikeMeat = true,
            () => SelectedHuntingJob.AllowHumanLikeMeat = false);
        Utilities.DrawToggle(ref pos, width, "ColonyManagerRedux.Hunting.AllowInsectMeat".Translate(),
            "ColonyManagerRedux.Hunting.AllowInsectMeat.Tip".Translate(),
            SelectedHuntingJob.TriggerThreshold.ThresholdFilter.Allows(ManagerThingDefOf.Meat_Megaspider),
            () => SelectedHuntingJob.AllowInsectMeat = true,
            () => SelectedHuntingJob.AllowInsectMeat = false);

        return pos.y - start.y;
    }

    public float DrawUnforbidCorpses(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect, "ColonyManagerRedux.Hunting.UnforbidCorpses".Translate(), "ColonyManagerRedux.Hunting.UnforbidCorpses.Tip".Translate(),
            ref SelectedHuntingJob.UnforbidCorpses);
        return ListEntryHeight;
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