// ManagerJobProductionTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;
using static ColonyManagerRedux.Managers.ManagerJob_Production;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerJobProductionTests
{
    // RecipeDef.ingredientValueGetterClass is private (normally populated from XML/PostLoad) -
    // these tests need to control which IngredientValueGetter a fake recipe uses to exercise
    // both the item-count (Volume) and nutrition-conversion (Nutrition) code paths.
    private static void SetIngredientValueGetterClass(RecipeDef recipe, Type type) =>
        typeof(RecipeDef)
            .GetField("ingredientValueGetterClass", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(recipe, type);

    // IngredientValueGetter_Nutrition.ValuePerUnitOf returns 0 unless
    // ThingDef.IsNutritionGivingIngestible is true, which needs a non-null `ingestible` with its
    // `parent` back-reference set (normally wired up by ThingDef.PostLoad, which a bare
    // test-constructed def never goes through).
    private static ThingDef NutritionGivingThingDef(string defName, float nutrition)
    {
        var thingDef = new ThingDef
        {
            defName = defName,
            statBases = [new StatModifier { stat = StatDefOf.Nutrition, value = nutrition }],
            ingestible = new IngestibleProperties(),
        };
        thingDef.ingestible.parent = thingDef;
        return thingDef;
    }

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
            // A real, XML-loaded RecipeDef without a <fixedIngredientFilter> tag never runs the
            // field initializer new RecipeDef() does here (defs are instantiated by the XML
            // loader without running constructors), so it stays null. Match that explicitly,
            // since a non-null-but-empty ThingFilter (what new RecipeDef() actually leaves it as)
            // would disallow everything instead of imposing no extra restriction.
            fixedIngredientFilter = null,
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
        var recipe = new RecipeDef
        {
            ingredients = [new IngredientCount { filter = fixedFilter }],
            fixedIngredientFilter = null,
        };
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
            fixedIngredientFilter = null,
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
            fixedIngredientFilter = null,
        };

        var options = AllRecipeIngredientOptions(recipe).ToList();

        Assert.ThatCollection(options).Has.Count(2);
        Assert.ThatCollection(options).Does.Contain(cotton);
        Assert.ThatCollection(options).Does.Contain(leather);
    }

    // Regression test: "butcher creature"'s single ingredient slot only filters on the broad
    // "Corpses" category, so before this fix AllRecipeIngredientOptions offered mechanoid corpses
    // as a valid ingredient even though the recipe's fixedIngredientFilter explicitly disallows
    // them (mechanoid/drone corpses are butchered by a separate recipe). Any def excluded by
    // fixedIngredientFilter must not appear in the options, regardless of the slot's own filter.
    [Test]
    public static void AllRecipeIngredientOptionsExcludesDefsDisallowedByFixedIngredientFilter()
    {
        var fleshCorpse = new ThingDef { defName = "CMR_TestFleshCorpse" };
        var mechanoidCorpse = new ThingDef { defName = "CMR_TestMechanoidCorpse" };
        var slotFilter = new ThingFilter();
        slotFilter.SetAllow(fleshCorpse, true);
        slotFilter.SetAllow(mechanoidCorpse, true);
        var fixedFilter = new ThingFilter();
        fixedFilter.SetAllow(fleshCorpse, true);
        var recipe = new RecipeDef
        {
            ingredients = [new IngredientCount { filter = slotFilter }],
            fixedIngredientFilter = fixedFilter,
        };

        var options = AllRecipeIngredientOptions(recipe).ToList();

        Assert.ThatCollection(options).Does.Contain(fleshCorpse);
        Assert.ThatCollection(options).Does.Not.Contain(mechanoidCorpse);
    }

    // Plain fakes, not real Bill_Production/ManagerJob_Production: constructing a real
    // Bill_Production requires a loaded game (Bill.InitializeAfterClone calls
    // Find.UniqueIDsManager), which isn't available to this test suite.
    private sealed class FakeJob(List<object> managedItems)
    {
        public List<object> ManagedItems { get; } = managedItems;
    }

    [Test]
    public static void FindOwningJobFindsJobThatManagesTheItem()
    {
        var item = new object();
        var job = new FakeJob([item]);

        var found = FindOwningJob([job], j => j.ManagedItems, item);

        Assert.That(ReferenceEquals(found, job)).Is.True();
    }

    [Test]
    public static void FindOwningJobReturnsNullWhenNoJobManagesTheItem()
    {
        var managedItem = new object();
        var unmanagedItem = new object();
        var job = new FakeJob([managedItem]);

        var found = FindOwningJob([job], j => j.ManagedItems, unmanagedItem);

        Assert.That(found is null).Is.True();
    }

    [Test]
    public static void FindOwningJobReturnsTheCorrectJobAmongMultiple()
    {
        var item = new object();
        var otherJob = new FakeJob([new object()]);
        var owningJob = new FakeJob([item]);

        var found = FindOwningJob([otherJob, owningJob], j => j.ManagedItems, item);

        Assert.That(ReferenceEquals(found, owningJob)).Is.True();
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

    [Test]
    public static void RecipeSharesOutputWithOverlappingSetsReturnsTrue()
    {
        var steel = new ThingDef { defName = "CMR_TestSteel" };
        var plasteel = new ThingDef { defName = "CMR_TestPlasteel" };

        Assert.That(RecipeSharesOutput([steel], [steel, plasteel])).Is.True();
    }

    [Test]
    public static void RecipeSharesOutputWithDisjointSetsReturnsFalse()
    {
        var steel = new ThingDef { defName = "CMR_TestSteel2" };
        var meat = new ThingDef { defName = "CMR_TestMeat3" };

        Assert.That(RecipeSharesOutput([steel], [meat])).Is.False();
    }

    [Test]
    public static void RecipeSharesOutputWithEmptyCandidateOutputsReturnsFalse()
    {
        var steel = new ThingDef { defName = "CMR_TestSteel3" };

        Assert.That(RecipeSharesOutput([], [steel])).Is.False();
    }

    [Test]
    public static void IngredientCountPerIterationSumsMatchingSlotsOnly()
    {
        var steel = new ThingDef { defName = "CMR_TestLinkSteel" };
        var wood = new ThingDef { defName = "CMR_TestLinkWood" };
        var steelFilter = new ThingFilter();
        steelFilter.SetAllow(steel, true);
        var woodFilter = new ThingFilter();
        woodFilter.SetAllow(wood, true);
        var steelSlot1 = new IngredientCount { filter = steelFilter };
        steelSlot1.SetBaseCount(10);
        var steelSlot2 = new IngredientCount { filter = steelFilter };
        steelSlot2.SetBaseCount(5);
        var woodSlot = new IngredientCount { filter = woodFilter };
        woodSlot.SetBaseCount(20);
        var recipe = new RecipeDef
        {
            ingredients = [steelSlot1, steelSlot2, woodSlot],
            fixedIngredientFilter = null,
        };
        SetIngredientValueGetterClass(recipe, typeof(IngredientValueGetter_Volume));

        // Neither def is IsStuff, so IngredientValueGetter_Volume.ValuePerUnitOf is 1 for both -
        // CountRequiredOfFor reduces to the slot's raw base count, same as plain item-count
        // recipes (e.g. steel/component builds) behave.
        Assert.That(IngredientCountPerIteration(recipe, [steel])).Is.EqualTo(15);
    }

    [Test]
    public static void IngredientCountPerIterationWithNoMatchingSlotIsZero()
    {
        var steel = new ThingDef { defName = "CMR_TestLinkSteel2" };
        var wood = new ThingDef { defName = "CMR_TestLinkWood2" };
        var woodFilter = new ThingFilter();
        woodFilter.SetAllow(wood, true);
        var woodSlot = new IngredientCount { filter = woodFilter };
        woodSlot.SetBaseCount(20);
        var recipe = new RecipeDef { ingredients = [woodSlot], fixedIngredientFilter = null };
        SetIngredientValueGetterClass(recipe, typeof(IngredientValueGetter_Volume));

        Assert.That(IngredientCountPerIteration(recipe, [steel])).Is.EqualTo(0);
    }

    // Regression test: this recipe's slot count (0.5) is a nutrition value, not an item count -
    // treating it as one (the old `(int)ic.GetBaseCount()` behavior) truncated to 0, so a linked
    // butcher job's target silently got set to 0 (see the bug this guards against: a "cook
    // simple meal" job linked to "butcher creature" set the producer's target to 0 instead of
    // the correct 10 raw meat, because 0.5 nutrition needed / 0.05 nutrition-per-meat was never
    // computed - only the raw 0.5 was truncated to an int).
    [Test]
    public static void IngredientCountPerIterationConvertsNutritionToItemCount()
    {
        var meat = NutritionGivingThingDef("CMR_TestLinkMeat", 0.05f);
        var rawFoodFilter = new ThingFilter();
        rawFoodFilter.SetAllow(meat, true);
        var rawFoodSlot = new IngredientCount { filter = rawFoodFilter };
        rawFoodSlot.SetBaseCount(0.5f);
        var recipe = new RecipeDef { ingredients = [rawFoodSlot], fixedIngredientFilter = null };
        SetIngredientValueGetterClass(recipe, typeof(IngredientValueGetter_Nutrition));

        // 0.5 nutrition needed / 0.05 nutrition per meat = 10 meat.
        Assert.That(IngredientCountPerIteration(recipe, [meat])).Is.EqualTo(10);
    }

    // A slot is satisfied by any one matching def, not all of them at once - if two defs with
    // different nutrition values both cover the same slot, demand must be based on whichever
    // needs the most raw items (the conservative "enough regardless of which one is actually
    // used" amount), not the sum of each def's own requirement (which would multiply demand by
    // however many defs happen to be allowed).
    [Test]
    public static void IngredientCountPerIterationTakesWorstCaseAcrossCandidates()
    {
        var denseMeat = NutritionGivingThingDef("CMR_TestLinkDenseMeat", 0.1f);
        var sparseMeat = NutritionGivingThingDef("CMR_TestLinkSparseMeat", 0.05f);
        var rawFoodFilter = new ThingFilter();
        rawFoodFilter.SetAllow(denseMeat, true);
        rawFoodFilter.SetAllow(sparseMeat, true);
        var rawFoodSlot = new IngredientCount { filter = rawFoodFilter };
        rawFoodSlot.SetBaseCount(0.5f);
        var recipe = new RecipeDef { ingredients = [rawFoodSlot], fixedIngredientFilter = null };
        SetIngredientValueGetterClass(recipe, typeof(IngredientValueGetter_Nutrition));

        // 0.5 / 0.1 = 5 for denseMeat, 0.5 / 0.05 = 10 for sparseMeat - take the worst case (10),
        // not the sum (15).
        Assert.That(IngredientCountPerIteration(recipe, [denseMeat, sparseMeat])).Is.EqualTo(10);
    }

    [Test]
    public static void ComputeIngredientDemandScalesWithConsumerTarget()
    {
        var steel = new ThingDef { defName = "CMR_TestLinkSteel3" };
        var component = new ThingDef { defName = "CMR_TestLinkComponent" };
        var steelFilter = new ThingFilter();
        steelFilter.SetAllow(steel, true);
        var steelSlot = new IngredientCount { filter = steelFilter };
        steelSlot.SetBaseCount(10);
        var recipe = new RecipeDef
        {
            ingredients = [steelSlot],
            products = [new ThingDefCountClass(component, 1)],
            fixedIngredientFilter = null,
        };
        SetIngredientValueGetterClass(recipe, typeof(IngredientValueGetter_Volume));

        // 4 components needed, 1 produced per iteration -> 4 iterations -> 4 * 10 steel.
        Assert.That(ComputeIngredientDemand(recipe, 4, [steel])).Is.EqualTo(40);
    }

    // Regression test: a linked producer's target was being sized to fully refill a consumer's
    // *entire* target from empty (e.g. keeping enough raw meat in stock to cook 500 meals from
    // scratch), which is a wildly oversized buffer for the common case - only the number of
    // iterations configured via LinkedDemandBufferCount should count, capped down (never up) by
    // however large the consumer's own target actually is.
    [Test]
    public static void EffectiveLinkedDemandBufferCountCapsToTargetButNeverExceedsIt()
    {
        Assert.That(EffectiveLinkedDemandBufferCount(5, 500)).Is.EqualTo(5);
        Assert.That(EffectiveLinkedDemandBufferCount(5, 3)).Is.EqualTo(3);
        Assert.That(EffectiveLinkedDemandBufferCount(5, 0)).Is.EqualTo(0);
    }

    // Regression test: changing LinkedDemandBufferCount on a consumer had no visible effect on a
    // linked producer's computed target until that producer's own UpdateInterval next elapsed
    // (a full in-game day by default), since nothing told the producer its demand might have
    // changed. LinkedDemandBufferCount's setter now touches every linked producer when the value
    // actually changes, so it recomputes on the very next gather pass instead.
    [Test]
    public static void ProducersNeedingTouchOnBufferCountChangeReturnsProducersOnRealChange()
    {
        var producer = new FakeLinkedJob();

        var result = ProducersNeedingTouchOnBufferCountChange(5, 20, [producer]).ToList();

        Assert.ThatCollection(result).Has.Count(1);
        Assert.ThatCollection(result).Does.Contain(producer);
    }

    [Test]
    public static void ProducersNeedingTouchOnBufferCountChangeReturnsNothingWhenUnchanged()
    {
        var producer = new FakeLinkedJob();

        var result = ProducersNeedingTouchOnBufferCountChange(5, 5, [producer]);

        Assert.ThatCollection(result).Is.Empty();
    }

    [Test]
    public static void AggregateLinkedDemandSumsByDefault() =>
        Assert
            .That(AggregateLinkedDemand([20, 40, 60], LinkedDemandAggregation.Sum))
            .Is.EqualTo(120);

    [Test]
    public static void AggregateLinkedDemandTakesHighestForMaxOfConsumers() =>
        Assert
            .That(AggregateLinkedDemand([20, 40, 60], LinkedDemandAggregation.MaxOfConsumers))
            .Is.EqualTo(60);

    [Test]
    public static void AggregateLinkedDemandWithNoConsumersIsZero()
    {
        Assert.That(AggregateLinkedDemand([], LinkedDemandAggregation.Sum)).Is.EqualTo(0);
        Assert
            .That(AggregateLinkedDemand([], LinkedDemandAggregation.MaxOfConsumers))
            .Is.EqualTo(0);
    }

    // Regression test: a linked producer restricted to what its consumers actually need should
    // keep only the ingredients whose mapped output at least one consumer wants, not every
    // ingredient the recipe could otherwise accept (e.g. "butcher creature" shouldn't keep
    // accepting boar corpses just because it's still linked, if no linked consumer allows boar
    // meat).
    [Test]
    public static void FilterIngredientsProducingDesiredOutputsKeepsOnlyMatchingIngredients()
    {
        var bearCorpse = new ThingDef { defName = "Corpse_Bear" };
        var muffaloCorpse = new ThingDef { defName = "Corpse_Muffalo" };
        var boarCorpse = new ThingDef { defName = "Corpse_Boar" };
        var bearMeat = new ThingDef { defName = "Meat_Bear" };
        var muffaloMeat = new ThingDef { defName = "Meat_Muffalo" };
        var boarMeat = new ThingDef { defName = "Meat_Boar" };
        var ingredientToOutput = new Dictionary<ThingDef, ThingDef>
        {
            [bearCorpse] = bearMeat,
            [muffaloCorpse] = muffaloMeat,
            [boarCorpse] = boarMeat,
        };

        var result = FilterIngredientsProducingDesiredOutputs(
                [bearCorpse, muffaloCorpse, boarCorpse],
                ingredientToOutput,
                [bearMeat, muffaloMeat]
            )
            .ToList();

        Assert.ThatCollection(result).Has.Count(2);
        Assert.ThatCollection(result).Does.Contain(bearCorpse);
        Assert.ThatCollection(result).Does.Contain(muffaloCorpse);
    }

    [Test]
    public static void FilterIngredientsProducingDesiredOutputsExcludesUnmappedIngredients()
    {
        var fish = new ThingDef { defName = "Fish_Salmon" };
        var bearMeat = new ThingDef { defName = "Meat_Bear" };
        var ingredientToOutput = new Dictionary<ThingDef, ThingDef>();

        var result = FilterIngredientsProducingDesiredOutputs(
            [fish],
            ingredientToOutput,
            [bearMeat]
        );

        Assert.ThatCollection(result).Is.Empty();
    }

    // Plain fakes, not real ManagerJob_Production instances: constructing one requires a live
    // Manager/Map, which isn't available to this test suite. WouldCreateCycle is generic over
    // the linked-sources selector for exactly this reason (same trick FindOwningJob uses).
    private sealed class FakeLinkedJob
    {
        public List<FakeLinkedJob> LinkedSources { get; } = [];
    }

    [Test]
    public static void WouldCreateCycleDetectsDirectCycle()
    {
        var consumer = new FakeLinkedJob();
        var producer = new FakeLinkedJob();
        producer.LinkedSources.Add(consumer);

        Assert.That(WouldCreateCycle(consumer, producer, j => j.LinkedSources)).Is.True();
    }

    [Test]
    public static void WouldCreateCycleDetectsTransitiveCycleThroughAChain()
    {
        var consumer = new FakeLinkedJob();
        var middle = new FakeLinkedJob();
        var producer = new FakeLinkedJob();
        middle.LinkedSources.Add(consumer);
        producer.LinkedSources.Add(middle);

        Assert.That(WouldCreateCycle(consumer, producer, j => j.LinkedSources)).Is.True();
    }

    [Test]
    public static void WouldCreateCycleAllowsALegitimateNonCyclicChain()
    {
        var consumer = new FakeLinkedJob();
        var producer = new FakeLinkedJob();
        var unrelatedUpstream = new FakeLinkedJob();
        producer.LinkedSources.Add(unrelatedUpstream);

        Assert.That(WouldCreateCycle(consumer, producer, j => j.LinkedSources)).Is.False();
    }

    [Test]
    public static void WouldCreateCycleRejectsLinkingAJobToItself()
    {
        var job = new FakeLinkedJob();

        Assert.That(WouldCreateCycle(job, job, j => j.LinkedSources)).Is.True();
    }

    // Counts ConfigureLinkableProductFilter calls so tests can assert on memoization instead of
    // just the returned defs - a real resolver like RecipeProductResolver_ButcherAnimals does a
    // full DefDatabase<ThingDef> scan in that method, which is exactly what shouldn't happen on
    // every call once ResolvedOutputDefsFor has cached a recipe's result.
    private sealed class CountingResolver(ThingDef output) : RecipeProductResolver
    {
        public int ConfigureLinkableProductFilterCallCount { get; private set; }
        private readonly ThingDef _output = output;

        public override bool CanResolve(RecipeDef recipe) => true;

        public override void ConfigureFilter(RecipeDef recipe, ThingFilter filter) =>
            filter.SetAllow(_output, true);

        public override void ConfigureLinkableProductFilter(RecipeDef recipe, ThingFilter filter)
        {
            ConfigureLinkableProductFilterCallCount++;
            filter.SetAllow(_output, true);
        }
    }

    [Test]
    public static void ResolvedOutputDefsForReturnsResolverOutput()
    {
        var meat = new ThingDef { defName = "CMR_TestResolvedOutputMeat" };
        var recipe = new RecipeDef { defName = "CMR_TestResolvedOutputRecipe" };
        var resolver = new CountingResolver(meat);

        var result = ResolvedOutputDefsFor(resolver, recipe);

        Assert.ThatCollection(result).Does.Contain(meat);
    }

    [Test]
    public static void ResolvedOutputDefsForWithNoResolverReturnsEmpty()
    {
        var recipe = new RecipeDef { defName = "CMR_TestResolvedOutputNoResolverRecipe" };

        var result = ResolvedOutputDefsFor(null, recipe);

        Assert.ThatCollection(result).Is.Empty();
    }

    [Test]
    public static void ResolvedOutputDefsForOnlyResolvesOnceThenServesFromCache()
    {
        var meat = new ThingDef { defName = "CMR_TestResolvedOutputMemoMeat" };
        var recipe = new RecipeDef { defName = "CMR_TestResolvedOutputMemoRecipe" };
        var resolver = new CountingResolver(meat);

        var first = ResolvedOutputDefsFor(resolver, recipe).ToList();
        var second = ResolvedOutputDefsFor(resolver, recipe).ToList();

        Assert.That(resolver.ConfigureLinkableProductFilterCallCount).Is.EqualTo(1);
        Assert.ThatCollection(first).Does.Contain(meat);
        Assert.ThatCollection(second).Does.Contain(meat);
    }
}
