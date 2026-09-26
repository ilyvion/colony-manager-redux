// ManagerTabShortcutPredicateTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The predicates behind the shortcut group toggles of the Hunting, Forestry, Foraging and
/// Mining tabs.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class ManagerTabShortcutPredicateTests
{
    private static PawnKindDef Kind(string name) => DefDatabase<PawnKindDef>.GetNamed(name);

    private static PawnKindDef KindWithManhunterChance(float chance) =>
        new()
        {
            race = new ThingDef { race = new RaceProperties { manhunterOnDamageChance = chance } },
        };

    // Hunting

    [Test]
    public static void WolfIsAPredatorAndMuffaloIsNot()
    {
        Expect.IsTrue(ManagerTab_Hunting.IsPredator(Kind("Wolf_Timber")));
        Expect.IsFalse(ManagerTab_Hunting.IsPredator(Kind("Muffalo")));
    }

    [Test]
    public static void MuffaloIsAHerdAnimalAndWolfIsNot()
    {
        Expect.IsTrue(ManagerTab_Hunting.IsHerdAnimal(Kind("Muffalo")));
        Expect.IsFalse(ManagerTab_Hunting.IsHerdAnimal(Kind("Wolf_Timber")));
    }

    [Test]
    public static void BoomalopeAndBoomratAreExplodingAndMuffaloIsNot()
    {
        Expect.IsTrue(ManagerTab_Hunting.IsExploding(Kind("Boomalope")));
        Expect.IsTrue(ManagerTab_Hunting.IsExploding(Kind("Boomrat")));
        Expect.IsFalse(ManagerTab_Hunting.IsExploding(Kind("Muffalo")));
    }

    [Test]
    public static void AggressiveStartsAtFivePercentManhunterChance()
    {
        Expect.IsTrue(ManagerTab_Hunting.IsAggressive(KindWithManhunterChance(0.05f)), "0.05");
        Expect.IsTrue(ManagerTab_Hunting.IsAggressive(KindWithManhunterChance(0.5f)), "0.5");
        Expect.IsFalse(ManagerTab_Hunting.IsAggressive(KindWithManhunterChance(0.049f)), "0.049");
        Expect.IsFalse(ManagerTab_Hunting.IsAggressive(KindWithManhunterChance(0f)), "0");
    }

    [Test]
    public static void FleshFollowsTheRaceFleshType()
    {
        Expect.IsTrue(ManagerTab_Hunting.IsFlesh(Kind("Muffalo")));
        Expect.IsFalse(ManagerTab_Hunting.IsFlesh(Kind("Mech_Lancer")));
    }

    [Test]
    public static void MegascarabIsAnInsectAndMuffaloIsNot()
    {
        Expect.IsTrue(ManagerTab_Hunting.IsInsect(Kind("Megascarab")));
        Expect.IsFalse(ManagerTab_Hunting.IsInsect(Kind("Muffalo")));
    }

    [Test]
    public static void HasMeatAndHasLeatherFollowTheRaceDefs()
    {
        var meatOnly = new PawnKindDef
        {
            race = new ThingDef { race = new RaceProperties { meatDef = ThingDefOf.Meat_Human } },
        };
        var leatherOnly = new PawnKindDef
        {
            race = new ThingDef
            {
                race = new RaceProperties { leatherDef = ThingDefOf.Leather_Plain },
            },
        };

        Expect.IsTrue(ManagerTab_Hunting.HasMeat(meatOnly));
        Expect.IsFalse(ManagerTab_Hunting.HasLeather(meatOnly));
        Expect.IsTrue(ManagerTab_Hunting.HasLeather(leatherOnly));
        Expect.IsFalse(ManagerTab_Hunting.HasMeat(leatherOnly));
    }

    // Forestry

    private static ThingDef Plant(
        TreeCategory category = TreeCategory.None,
        string? harvestTag = null,
        ThingDef? harvested = null
    ) =>
        new()
        {
            plant = new PlantProperties
            {
                treeCategory = category,
                harvestTag = harvestTag,
                harvestedThingDef = harvested,
            },
        };

    [Test]
    public static void TreeCategoryMatchesOnlyItsOwnCategory()
    {
        var full = Plant(TreeCategory.Full);

        Expect.IsTrue(ManagerTab_Forestry.IsTreeOfCategory(full, TreeCategory.Full));
        Expect.IsFalse(ManagerTab_Forestry.IsTreeOfCategory(full, TreeCategory.Mini));
        Expect.IsFalse(ManagerTab_Forestry.IsTreeOfCategory(full, TreeCategory.Super));
    }

    [Test]
    public static void DefWithoutPlantPropertiesIsNoTreeAndDoesNotThrow()
    {
        var noPlant = new ThingDef();

        Expect.IsFalse(ManagerTab_Forestry.IsTreeOfCategory(noPlant, TreeCategory.Full));
        Expect.IsFalse(ManagerTab_Forestry.IsWoodTree(noPlant));
    }

    [Test]
    public static void WoodTreesHaveTheWoodTagOrDropLogs()
    {
        Expect.IsTrue(ManagerTab_Forestry.IsWoodTree(Plant(harvestTag: "Wood")), "tag");
        Expect.IsTrue(ManagerTab_Forestry.IsWoodTree(Plant(harvested: ThingDefOf.WoodLog)), "logs");
        Expect.IsFalse(
            ManagerTab_Forestry.IsWoodTree(
                Plant(harvestTag: "Standard", harvested: ThingDefOf.Steel)
            ),
            "neither"
        );
    }

    [Test]
    public static void OnlyNegativeBeautyIsUgly()
    {
        static ThingDef WithBeauty(float value)
        {
            return new()
            {
                statBases = [new StatModifier { stat = StatDefOf.Beauty, value = value }],
            };
        }

        Expect.IsTrue(ManagerTab_Forestry.IsUgly(WithBeauty(-1f)), "-1");
        Expect.IsFalse(ManagerTab_Forestry.IsUgly(WithBeauty(0f)), "0");
        Expect.IsFalse(ManagerTab_Forestry.IsUgly(WithBeauty(2f)), "2");
        Expect.IsFalse(ManagerTab_Forestry.IsUgly(new ThingDef()), "no stat");
    }

    [Test]
    public static void CoverNeedsSomeFill()
    {
        Expect.IsFalse(ManagerTab_Forestry.ProvidesCover(new ThingDef { fillPercent = 0f }), "0");
        Expect.IsTrue(
            ManagerTab_Forestry.ProvidesCover(new ThingDef { fillPercent = 0.25f }),
            ".25"
        );
        Expect.IsTrue(ManagerTab_Forestry.ProvidesCover(new ThingDef { fillPercent = 1f }), "1");
    }

    // Foraging

    [Test]
    public static void PlantYieldingFoodIsEdible()
    {
        Expect.IsTrue(
            ManagerTab_Foraging.IsEdible(Plant(harvested: ThingDefOf.MealSimple)),
            "meal"
        );
        Expect.IsFalse(ManagerTab_Foraging.IsEdible(Plant(harvested: ThingDefOf.WoodLog)), "wood");
        Expect.IsFalse(ManagerTab_Foraging.IsEdible(Plant()), "nothing harvested");
        Expect.IsFalse(ManagerTab_Foraging.IsEdible(new ThingDef()), "no plant");
    }

    [Test]
    public static void CavePlantsAreMushrooms()
    {
        Expect.IsTrue(
            ManagerTab_Foraging.IsMushroom(
                new ThingDef { plant = new PlantProperties { cavePlant = true } }
            )
        );
        Expect.IsFalse(ManagerTab_Foraging.IsMushroom(Plant()));
        Expect.IsFalse(ManagerTab_Foraging.IsMushroom(new ThingDef()));
    }

    [Test]
    public static void MedicineYieldingPlantIsMedicinal()
    {
        Expect.IsTrue(
            ManagerTab_Foraging.IsMedicinal(Plant(harvested: ThingDefOf.MedicineHerbal)),
            "herbal medicine"
        );
        Expect.IsFalse(
            ManagerTab_Foraging.IsMedicinal(Plant(harvested: ThingDefOf.WoodLog)),
            "wood"
        );
        Expect.IsFalse(ManagerTab_Foraging.IsMedicinal(Plant()), "nothing harvested");
        Expect.IsFalse(ManagerTab_Foraging.IsMedicinal(new ThingDef()), "no plant");
    }

    [Test]
    public static void DrugYieldingPlantIsDrugPlant()
    {
        Expect.IsTrue(
            ManagerTab_Foraging.IsDrugPlant(
                Plant(harvested: DefDatabase<ThingDef>.GetNamed("SmokeleafLeaves"))
            ),
            "smokeleaf"
        );
        Expect.IsFalse(
            ManagerTab_Foraging.IsDrugPlant(Plant(harvested: ThingDefOf.WoodLog)),
            "wood"
        );
        Expect.IsFalse(ManagerTab_Foraging.IsDrugPlant(Plant()), "nothing harvested");
        Expect.IsFalse(ManagerTab_Foraging.IsDrugPlant(new ThingDef()), "no plant");
    }

    [Test]
    public static void FabricYieldingPlantIsTextile()
    {
        Expect.IsTrue(ManagerTab_Foraging.IsTextile(Plant(harvested: ThingDefOf.Cloth)), "cotton");
        Expect.IsTrue(
            ManagerTab_Foraging.IsTextile(
                Plant(harvested: DefDatabase<ThingDef>.GetNamed("DevilstrandCloth"))
            ),
            "devilstrand"
        );
        Expect.IsFalse(ManagerTab_Foraging.IsTextile(Plant(harvested: ThingDefOf.WoodLog)), "wood");
        Expect.IsFalse(ManagerTab_Foraging.IsTextile(Plant()), "nothing harvested");
        Expect.IsFalse(ManagerTab_Foraging.IsTextile(new ThingDef()), "no plant");
    }

    [Test]
    public static void NonEdibleIsTheComplementOfEdible()
    {
        Expect.IsFalse(
            ManagerTab_Foraging.IsNonEdible(Plant(harvested: ThingDefOf.MealSimple)),
            "meal"
        );
        Expect.IsTrue(
            ManagerTab_Foraging.IsNonEdible(Plant(harvested: ThingDefOf.WoodLog)),
            "wood"
        );
        Expect.IsTrue(ManagerTab_Foraging.IsNonEdible(Plant()), "nothing harvested");
    }

    // Mining

    private static ThingDef Vein(string name) => DefDatabase<ThingDef>.GetNamed(name);

    [Test]
    public static void RockThatIsNotAResourceIsStone()
    {
        Expect.IsTrue(ManagerTab_Mining.IsStone(Vein("Granite")));
        Expect.IsFalse(ManagerTab_Mining.IsStone(Vein("MineableSteel")));
    }

    [Test]
    public static void SteelVeinIsMetalButNotPrecious()
    {
        var steel = Vein("MineableSteel");

        Expect.IsTrue(ManagerTab_Mining.IsMetalMineral(steel), "metal");
        Expect.IsFalse(ManagerTab_Mining.IsPreciousMineral(steel), "precious");
    }

    [Test]
    public static void GoldVeinIsPrecious()
    {
        var gold = Vein("MineableGold");

        Expect.IsTrue(ManagerTab_Mining.IsPreciousMineral(gold), "precious");
        Expect.IsFalse(ManagerTab_Mining.IsStone(gold), "stone");
    }

    [Test]
    public static void StoneIsNeitherMetalNorPrecious()
    {
        var granite = Vein("Granite");

        Expect.IsFalse(ManagerTab_Mining.IsMetalMineral(granite), "metal");
        Expect.IsFalse(ManagerTab_Mining.IsPreciousMineral(granite), "precious");
    }

    [Test]
    public static void SteelIsDeepAndDirectYielding()
    {
        var steel = Vein("MineableSteel");

        Expect.IsTrue(ManagerTab_Mining.IsDeepMineral(steel), "deep");
        Expect.IsTrue(ManagerTab_Mining.IsDirectYieldingMineral(steel), "direct");
        Expect.IsFalse(ManagerTab_Mining.IsChunkYieldingMineral(steel), "chunk");
    }

    [Test]
    public static void GraniteIsChunkYieldingAndNotDeep()
    {
        var granite = Vein("Granite");

        Expect.IsTrue(ManagerTab_Mining.IsChunkYieldingMineral(granite), "chunk");
        Expect.IsFalse(ManagerTab_Mining.IsDirectYieldingMineral(granite), "direct");
        Expect.IsFalse(ManagerTab_Mining.IsDeepMineral(granite), "deep");
    }

    [Test]
    public static void BuildingsAreGroupedByDesignationCategoryAndUncategorizedAreSkipped()
    {
        var structure = new DesignationCategoryDef { defName = "TestStructure" };
        var power = new DesignationCategoryDef { defName = "TestPower" };
        var wall = new ThingDef { defName = "TestWall", designationCategory = structure };
        var door = new ThingDef { defName = "TestDoor", designationCategory = structure };
        var battery = new ThingDef { defName = "TestBattery", designationCategory = power };
        var ruin = new ThingDef { defName = "TestRuin" };

        var groups = ManagerTab_Mining
            .GroupBuildingsByDesignationCategory([wall, battery, ruin, door])
            .ToDictionary(g => g.Key, g => g.ToList());

        Expect.AreEqual(2, groups.Count, "group count");
        Expect.AreEqual(2, groups[structure].Count, "structure count");
        Expect.IsTrue(
            groups[structure].Contains(wall) && groups[structure].Contains(door),
            "structure"
        );
        Expect.AreEqual(1, groups[power].Count, "power count");
        Expect.IsTrue(groups[power].Contains(battery), "power");
    }

    // Shortcut sets

    private static void ExpectDistinctResolvableLabels<T>(
        string tab,
        params ShortcutCategory<T>[][] sets
    )
    {
        var seen = new HashSet<string> { "ColonyManagerRedux.Shortcuts.All".Translate().Resolve() };
        foreach (var category in sets.SelectMany(s => s))
        {
            Expect.IsTrue(category.LabelKey.CanTranslate(), $"{tab}: {category.LabelKey} exists");
            Expect.IsTrue(category.TipKey.CanTranslate(), $"{tab}: {category.TipKey} exists");
            var label = category.LabelKey.Translate().Resolve();
            Expect.IsTrue(seen.Add(label), $"{tab}: label '{label}' is unique");
        }
    }

    [Test]
    public static void EachTabsShortcutRowsHaveDistinctResolvableLabels()
    {
        ExpectDistinctResolvableLabels("Hunting", ManagerTab_Hunting._animalShortcuts);
        ExpectDistinctResolvableLabels("Foraging", ManagerTab_Foraging._plantShortcuts);
        ExpectDistinctResolvableLabels(
            "Forestry",
            ManagerTab_Forestry._treeShortcuts,
            ManagerTab_Forestry._clearAreaTreeShortcuts
        );
        ExpectDistinctResolvableLabels("Mining", ManagerTab_Mining._mineralShortcuts);
    }

    // Shared shortcut rows

    [Test]
    public static void ShortcutClickModesFollowHeldModifiers()
    {
        Expect.AreEqual(
            ManagerTab.ShortcutClickMode.Toggle,
            ManagerTab.ShortcutClickModeFromModifiers(false, false, false)
        );
        Expect.AreEqual(
            ManagerTab.ShortcutClickMode.Restrict,
            ManagerTab.ShortcutClickModeFromModifiers(true, false, false)
        );
        Expect.AreEqual(
            ManagerTab.ShortcutClickMode.Remove,
            ManagerTab.ShortcutClickModeFromModifiers(false, true, false)
        );
        Expect.AreEqual(
            ManagerTab.ShortcutClickMode.Add,
            ManagerTab.ShortcutClickModeFromModifiers(false, false, true)
        );
    }

    private static HashSet<int> ClickShortcut(
        HashSet<int> selected,
        List<int> matching,
        ManagerTab.ShortcutClickMode mode,
        bool plainTurnsOn = true
    )
    {
        var all = new List<int> { 1, 2, 3, 4, 5 };
        ManagerTab.ApplyShortcutClick(
            matching,
            all,
            (o, on) =>
            {
                _ = on ? selected.Add(o) : selected.Remove(o);
            },
            mode,
            plainTurnsOn
        );
        return selected;
    }

    [Test]
    public static void ShortcutClickRestrictKeepsOnlyMatchingOptions()
    {
        var result = ClickShortcut([1, 2, 3, 4], [2, 3, 5], ManagerTab.ShortcutClickMode.Restrict);
        Expect.IsTrue(result.SetEquals([2, 3, 5]));
    }

    [Test]
    public static void ShortcutClickAddKeepsOtherOptionsAndTurnsMatchingOn()
    {
        var result = ClickShortcut([1], [2, 3], ManagerTab.ShortcutClickMode.Add);
        Expect.IsTrue(result.SetEquals([1, 2, 3]));
    }

    [Test]
    public static void ShortcutClickRemoveKeepsOtherOptionsAndTurnsMatchingOff()
    {
        var result = ClickShortcut([1, 2, 3, 4], [2, 3, 5], ManagerTab.ShortcutClickMode.Remove);
        Expect.IsTrue(result.SetEquals([1, 4]));
    }

    [Test]
    public static void ShortcutClickToggleTurnsMatchingOnOrOff()
    {
        Expect.IsTrue(
            ClickShortcut([1], [2, 3], ManagerTab.ShortcutClickMode.Toggle).SetEquals([1, 2, 3])
        );
        Expect.IsTrue(
            ClickShortcut([1, 2, 3], [2, 3], ManagerTab.ShortcutClickMode.Toggle, false)
                .SetEquals([1])
        );
    }

    [Test]
    public static void ShortcutRowWithNoMatchesIsHidden()
    {
        Expect.IsFalse(ManagerTab.IsShortcutRowVisible(0, 5), "some options");
        Expect.IsFalse(ManagerTab.IsShortcutRowVisible(0, 0), "no options");
        Expect.IsFalse(ManagerTab.IsShortcutRowVisible(0, 5, isAllRow: true), "all row");
    }

    [Test]
    public static void ShortcutRowMatchingSomeButNotAllOptionsIsShown()
    {
        Expect.IsTrue(ManagerTab.IsShortcutRowVisible(2, 5));
        Expect.IsTrue(ManagerTab.IsShortcutRowVisible(1, 2));
    }

    [Test]
    public static void ShortcutRowMatchingEveryOptionIsHiddenUnlessItIsTheAllRow()
    {
        Expect.IsFalse(ManagerTab.IsShortcutRowVisible(5, 5), "category");
        Expect.IsTrue(ManagerTab.IsShortcutRowVisible(5, 5, isAllRow: true), "all row");
    }

    [Test]
    public static void OnlyPositiveFlammabilityIsFlammable()
    {
        static ThingDef WithFlammability(float value)
        {
            return new()
            {
                statBases = [new StatModifier { stat = StatDefOf.Flammability, value = value }],
            };
        }

        Expect.IsTrue(ManagerTab_Forestry.IsFlammable(WithFlammability(0.5f)), "0.5");
        Expect.IsFalse(ManagerTab_Forestry.IsFlammable(WithFlammability(0f)), "0");
    }
}
