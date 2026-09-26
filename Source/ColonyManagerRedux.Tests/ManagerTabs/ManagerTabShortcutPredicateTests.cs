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

    // Shared shortcut rows

    [Test]
    public static void ShortcutRowWithNoMatchesIsHidden()
    {
        Expect.IsFalse(ManagerTab.IsShortcutRowVisible(0, 5), "some options");
        Expect.IsFalse(ManagerTab.IsShortcutRowVisible(0, 0), "no options");
        Expect.IsFalse(ManagerTab.IsShortcutRowVisible(0, 5, hideWhenAllMatch: true), "hide all");
    }

    [Test]
    public static void ShortcutRowWithMatchesIsShown()
    {
        Expect.IsTrue(ManagerTab.IsShortcutRowVisible(2, 5), "partial");
        Expect.IsTrue(ManagerTab.IsShortcutRowVisible(5, 5), "all match");
    }

    [Test]
    public static void ShortcutRowMatchingEverythingIsHiddenOnlyWhenRequested()
    {
        Expect.IsFalse(ManagerTab.IsShortcutRowVisible(5, 5, hideWhenAllMatch: true), "all match");
        Expect.IsTrue(ManagerTab.IsShortcutRowVisible(4, 5, hideWhenAllMatch: true), "partial");
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
