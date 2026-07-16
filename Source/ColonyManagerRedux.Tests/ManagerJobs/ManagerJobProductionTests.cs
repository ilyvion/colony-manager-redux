// ManagerJobProductionTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;
using static ColonyManagerRedux.Managers.ManagerJob_Production;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerJobProductionTests
{
    [Test]
    public static void NoManagedBillAndInactiveTriggerDoesNothing() =>
        Assert
            .That(
                DecideBillAction(
                    hasManagedBill: false,
                    managedBillSuspended: false,
                    triggerActive: false
                )
            )
            .Is.EqualTo(ProductionBillDecision.None);

    [Test]
    public static void NoManagedBillAndActiveTriggerCreatesNewBill() =>
        Assert
            .That(
                DecideBillAction(
                    hasManagedBill: false,
                    managedBillSuspended: false,
                    triggerActive: true
                )
            )
            .Is.EqualTo(ProductionBillDecision.CreateNew);

    [Test]
    public static void SuspendedManagedBillWithActiveTriggerIsActivated() =>
        Assert
            .That(
                DecideBillAction(
                    hasManagedBill: true,
                    managedBillSuspended: true,
                    triggerActive: true
                )
            )
            .Is.EqualTo(ProductionBillDecision.Activate);

    [Test]
    public static void ActiveManagedBillWithActiveTriggerIsLeftAlone() =>
        Assert
            .That(
                DecideBillAction(
                    hasManagedBill: true,
                    managedBillSuspended: false,
                    triggerActive: true
                )
            )
            .Is.EqualTo(ProductionBillDecision.None);

    [Test]
    public static void ActiveManagedBillWithInactiveTriggerIsSuspended() =>
        Assert
            .That(
                DecideBillAction(
                    hasManagedBill: true,
                    managedBillSuspended: false,
                    triggerActive: false
                )
            )
            .Is.EqualTo(ProductionBillDecision.Suspend);

    [Test]
    public static void SuspendedManagedBillWithInactiveTriggerIsLeftAlone() =>
        Assert
            .That(
                DecideBillAction(
                    hasManagedBill: true,
                    managedBillSuspended: true,
                    triggerActive: false
                )
            )
            .Is.EqualTo(ProductionBillDecision.None);

    [Test]
    public static void AllModeIsAlwaysInScope() =>
        Assert
            .That(
                IsWorkTableInScope(
                    WorkbenchAssignmentMode.All,
                    inArea: false,
                    isSpecificallySelected: false
                )
            )
            .Is.True();

    [Test]
    public static void AreaModeFollowsAreaMembership()
    {
        Assert
            .That(
                IsWorkTableInScope(
                    WorkbenchAssignmentMode.Area,
                    inArea: true,
                    isSpecificallySelected: false
                )
            )
            .Is.True();
        Assert
            .That(
                IsWorkTableInScope(
                    WorkbenchAssignmentMode.Area,
                    inArea: false,
                    isSpecificallySelected: true
                )
            )
            .Is.False();
    }

    [Test]
    public static void SpecificModeFollowsExplicitSelection()
    {
        Assert
            .That(
                IsWorkTableInScope(
                    WorkbenchAssignmentMode.Specific,
                    inArea: true,
                    isSpecificallySelected: false
                )
            )
            .Is.False();
        Assert
            .That(
                IsWorkTableInScope(
                    WorkbenchAssignmentMode.Specific,
                    inArea: false,
                    isSpecificallySelected: true
                )
            )
            .Is.True();
    }

    [Test]
    public static void MatchingSkillRangeNeedsNoUpdate() =>
        Assert.That(BillNeedsSkillRangeUpdate(new IntRange(0, 20), new IntRange(0, 20))).Is.False();

    [Test]
    public static void MismatchedSkillRangeNeedsUpdate() =>
        Assert.That(BillNeedsSkillRangeUpdate(new IntRange(0, 20), new IntRange(5, 15))).Is.True();

    [Test]
    public static void MatchingIngredientRadiusNeedsNoUpdate() =>
        Assert.That(BillNeedsIngredientRadiusUpdate(999f, 999f)).Is.False();

    [Test]
    public static void MismatchedIngredientRadiusNeedsUpdate() =>
        Assert.That(BillNeedsIngredientRadiusUpdate(999f, 12f)).Is.True();

    [Test]
    public static void MatchingStoreModeNeedsNoUpdate() =>
        Assert
            .That(
                BillNeedsStoreModeUpdate(
                    BillStoreModeDefOf.BestStockpile,
                    BillStoreModeDefOf.BestStockpile
                )
            )
            .Is.False();

    [Test]
    public static void MismatchedStoreModeNeedsUpdate() =>
        Assert
            .That(
                BillNeedsStoreModeUpdate(
                    BillStoreModeDefOf.DropOnFloor,
                    BillStoreModeDefOf.BestStockpile
                )
            )
            .Is.True();

    [Test]
    public static void MaintainStockModeOnlySupportsAccumulationOps() =>
        Assert
            .ThatCollection(SupportedOpsForMode(ProductionMode.MaintainStock))
            .Does.Not.Contain(Trigger_Threshold.Ops.HigherThan);

    [Test]
    public static void ConsumeSurplusModeSupportsAllOps() =>
        Assert
            .ThatCollection(SupportedOpsForMode(ProductionMode.ConsumeSurplus))
            .Does.Contain(Trigger_Threshold.Ops.HigherThan);

    [Test]
    public static void EnteringMaintainStockClearsConsumeSurplusFilterInitialized() =>
        Assert
            .That(NextConsumeSurplusFilterInitialized(ProductionMode.MaintainStock))
            .Is.EqualTo(false);

    [Test]
    public static void EnteringConsumeSurplusSetsConsumeSurplusFilterInitialized() =>
        Assert
            .That(NextConsumeSurplusFilterInitialized(ProductionMode.ConsumeSurplus))
            .Is.EqualTo(true);

    // Regression test: toggling MaintainStock -> ConsumeSurplus -> MaintainStock ->
    // ConsumeSurplus used to leave the second ConsumeSurplus entry believing its ingredient
    // filter was already seeded (stale from the first entry), so it kept showing MaintainStock's
    // output filter instead of re-deriving from the recipe's ingredients.
    [Test]
    public static void ConsumeSurplusFilterReseedsOnEverySeparateEntryAfterMaintainStock()
    {
        var initialized = NextConsumeSurplusFilterInitialized(ProductionMode.ConsumeSurplus);
        Assert.That(initialized).Is.EqualTo(true);

        initialized = NextConsumeSurplusFilterInitialized(ProductionMode.MaintainStock);
        Assert.That(initialized).Is.EqualTo(false);

        initialized = NextConsumeSurplusFilterInitialized(ProductionMode.ConsumeSurplus);
        Assert.That(initialized).Is.EqualTo(true);
    }

    private static void AssertShares(int[] actual, params int[] expected)
    {
        Assert.ThatCollection(actual).Has.Count(expected.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.That(actual[i]).Is.EqualTo(expected[i]);
        }
    }

    [Test]
    public static void SplitShortfallDividesEvenlyWithNoRemainder() =>
        AssertShares(SplitShortfall(15, 3), 5, 5, 5);

    [Test]
    public static void SplitShortfallDistributesRemainderToFirstTables() =>
        AssertShares(SplitShortfall(16, 3), 6, 5, 5);

    [Test]
    public static void SplitShortfallWithZeroTablesIsEmpty() =>
        Assert.ThatCollection(SplitShortfall(15, 0)).Is.Empty();

    [Test]
    public static void SplitShortfallWithZeroOrNegativeShortfallIsAllZero()
    {
        AssertShares(SplitShortfall(0, 3), 0, 0, 0);
        AssertShares(SplitShortfall(-5, 3), 0, 0, 0);
    }

    [Test]
    public static void SplitShortfallWithSingleTableGetsWholeShortfall() =>
        AssertShares(SplitShortfall(15, 1), 15);

    [Test]
    public static void SplitShortfallSmallerThanTableCountGivesEarlyTablesOneEach() =>
        AssertShares(SplitShortfall(2, 5), 1, 1, 0, 0, 0);

    [Test]
    public static void SharesToIterationsDividesExactly() =>
        Assert.That(SharesToIterations(10, 5)).Is.EqualTo(2);

    [Test]
    public static void SharesToIterationsRoundsRemainderUp() =>
        Assert.That(SharesToIterations(11, 5)).Is.EqualTo(3);

    [Test]
    public static void SharesToIterationsWithZeroShareIsZero() =>
        Assert.That(SharesToIterations(0, 5)).Is.EqualTo(0);

    [Test]
    public static void SharesToIterationsWithNonPositiveYieldFallsBackToOne() =>
        Assert.That(SharesToIterations(3, 0)).Is.EqualTo(3);

    [Test]
    public static void MatchingRepeatCountAndModeNeedsNoUpdate() =>
        Assert.That(BillNeedsRepeatCountUpdate(BillRepeatModeDefOf.RepeatCount, 4, 4)).Is.False();

    [Test]
    public static void MismatchedRepeatCountNeedsUpdate() =>
        Assert.That(BillNeedsRepeatCountUpdate(BillRepeatModeDefOf.RepeatCount, 4, 7)).Is.True();

    [Test]
    public static void ForeverModeAlwaysNeedsMigrationToRepeatCount() =>
        Assert.That(BillNeedsRepeatCountUpdate(BillRepeatModeDefOf.Forever, 0, 0)).Is.True();

    [Test]
    public static void YieldPerIterationUsesProductCountForSingleProductRecipe()
    {
        var thingDef = new ThingDef { defName = "CMR_TestYieldProduct" };
        var recipe = new RecipeDef { products = [new ThingDefCountClass(thingDef, 4)] };

        Assert.That(YieldPerIteration(recipe)).Is.EqualTo(4);
    }

    [Test]
    public static void YieldPerIterationFallsBackToOneForVariableYieldRecipe() =>
        Assert
            .That(
                YieldPerIteration(new RecipeDef { specialProducts = [SpecialProductType.Smelted] })
            )
            .Is.EqualTo(1);

    [Test]
    public static void ExpectedYieldFromBillsSumsRepeatCountModeBillsTimesYieldPerIteration() =>
        Assert
            .That(
                ExpectedYieldFromBills(
                    [(BillRepeatModeDefOf.RepeatCount, 3), (BillRepeatModeDefOf.RepeatCount, 2)],
                    yieldPerIteration: 4
                )
            )
            .Is.EqualTo(20);

    [Test]
    public static void ExpectedYieldFromBillsIgnoresForeverModeBills() =>
        Assert
            .That(
                ExpectedYieldFromBills(
                    [(BillRepeatModeDefOf.RepeatCount, 3), (BillRepeatModeDefOf.Forever, 100)],
                    yieldPerIteration: 4
                )
            )
            .Is.EqualTo(12);

    [Test]
    public static void ExpectedYieldFromBillsWithNoBillsIsZero() =>
        Assert.That(ExpectedYieldFromBills([], yieldPerIteration: 4)).Is.EqualTo(0);

    [Test]
    public static void ConfigureIngredientFilterAllowsEachIngredientOptionNotTheProduct()
    {
        var cotton = new ThingDef { defName = "CMR_TestCotton" };
        var duster = new ThingDef { defName = "CMR_TestDuster" };
        var ingredientFilter = new ThingFilter();
        ingredientFilter.SetAllow(cotton, true);
        var recipe = new RecipeDef
        {
            ingredients = [new IngredientCount { filter = ingredientFilter }],
            products = [new ThingDefCountClass(duster, 1)],
        };
        var filter = new ThingFilter();

        ConfigureIngredientFilter(recipe, filter);

        Assert.That(filter.Allows(cotton)).Is.True();
        Assert.That(filter.Allows(duster)).Is.False();
    }

    [Test]
    public static void ConfigureIngredientFilterAllowsFixedIngredient()
    {
        var wood = new ThingDef { defName = "CMR_TestFixedWood" };
        var fixedFilter = new ThingFilter();
        fixedFilter.SetAllow(wood, true);
        var recipe = new RecipeDef { ingredients = [new IngredientCount { filter = fixedFilter }] };
        var filter = new ThingFilter();

        ConfigureIngredientFilter(recipe, filter);

        Assert.That(filter.Allows(wood)).Is.True();
    }

    [Test]
    public static void ConfigureIngredientFilterUnionsMultipleIngredientSlots()
    {
        var cotton = new ThingDef { defName = "CMR_TestCotton2" };
        var leather = new ThingDef { defName = "CMR_TestLeather" };
        var cottonFilter = new ThingFilter();
        cottonFilter.SetAllow(cotton, true);
        var leatherFilter = new ThingFilter();
        leatherFilter.SetAllow(leather, true);
        var recipe = new RecipeDef
        {
            ingredients =
            [
                new IngredientCount { filter = cottonFilter },
                new IngredientCount { filter = leatherFilter },
            ],
        };
        var filter = new ThingFilter();

        ConfigureIngredientFilter(recipe, filter);

        Assert.That(filter.Allows(cotton)).Is.True();
        Assert.That(filter.Allows(leather)).Is.True();
    }

    [Test]
    public static void AllRecipeIngredientOptionsUnionsAndDeduplicatesAcrossSlots()
    {
        var cotton = new ThingDef { defName = "CMR_TestCotton3" };
        var leather = new ThingDef { defName = "CMR_TestLeather2" };
        var slot1Filter = new ThingFilter();
        slot1Filter.SetAllow(cotton, true);
        slot1Filter.SetAllow(leather, true);
        var slot2Filter = new ThingFilter();
        slot2Filter.SetAllow(cotton, true);
        var recipe = new RecipeDef
        {
            ingredients =
            [
                new IngredientCount { filter = slot1Filter },
                new IngredientCount { filter = slot2Filter },
            ],
        };

        var options = AllRecipeIngredientOptions(recipe).ToList();

        Assert.ThatCollection(options).Has.Count(2);
        Assert.ThatCollection(options).Does.Contain(cotton);
        Assert.ThatCollection(options).Does.Contain(leather);
    }

    [Test]
    public static void BillNeedsIngredientFilterUpdateWithMatchingSetsNeedsNoUpdate()
    {
        var cotton = new ThingDef { defName = "CMR_TestCotton4" };
        var leather = new ThingDef { defName = "CMR_TestLeather3" };

        Assert
            .That(BillNeedsIngredientFilterUpdate([cotton, leather], [cotton, leather]))
            .Is.False();
    }

    [Test]
    public static void BillNeedsIngredientFilterUpdateWithMismatchedSetsNeedsUpdate()
    {
        var cotton = new ThingDef { defName = "CMR_TestCotton5" };
        var leather = new ThingDef { defName = "CMR_TestLeather4" };

        Assert.That(BillNeedsIngredientFilterUpdate([cotton], [cotton, leather])).Is.True();
    }

    [Test]
    public static void GroupIngredientsByCategoryGroupsByDirectCategory()
    {
        var meat = new ThingCategoryDef { defName = "CMR_TestMeat" };
        var vegetarian = new ThingCategoryDef { defName = "CMR_TestVegetarian" };
        var bearMeat = new ThingDef { defName = "CMR_TestBearMeat", thingCategories = [meat] };
        var twistedMeat = new ThingDef
        {
            defName = "CMR_TestTwistedMeat",
            thingCategories = [meat],
        };
        var berries = new ThingDef { defName = "CMR_TestBerries", thingCategories = [vegetarian] };
        var uncategorized = new ThingDef { defName = "CMR_TestUncategorized" };

        var groups = GroupIngredientsByCategory([bearMeat, twistedMeat, berries, uncategorized]);

        Assert.ThatCollection(groups[meat]).Has.Count(2);
        Assert.ThatCollection(groups[meat]).Does.Contain(bearMeat);
        Assert.ThatCollection(groups[meat]).Does.Contain(twistedMeat);
        Assert.ThatCollection(groups[vegetarian]).Has.Count(1);
        Assert.ThatCollection(groups[vegetarian]).Does.Contain(berries);
        Assert.ThatCollection(groups.SelectMany(g => g)).Does.Not.Contain(uncategorized);
    }

    [Test]
    public static void GroupIngredientsByCategoryUsesOnlyFirstCategoryForMultiCategoryItems()
    {
        var meat = new ThingCategoryDef { defName = "CMR_TestMeat2" };
        var animalProducts = new ThingCategoryDef { defName = "CMR_TestAnimalProducts" };
        var eggs = new ThingDef
        {
            defName = "CMR_TestEggs",
            thingCategories = [animalProducts, meat],
        };

        var groups = GroupIngredientsByCategory([eggs]);

        Assert.ThatCollection(groups[animalProducts]).Has.Count(1);
        Assert.ThatCollection(groups[meat]).Has.Count(0);
    }
}
