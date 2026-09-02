// ManagerDefaultSettingsMigrationTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

// Guards against a save written before default-value settings for a job moved out of that job's
// ManagerSettings subclass into a dedicated ManagerDefaultSettings subclass losing those values:
// MigrateFrom must copy every field the old class held, under its new name.
[TestSuite]
internal static class ManagerDefaultSettingsMigrationTests
{
    [Test]
    public static void ForagingMigrateFromCopiesAllFields()
    {
        var old = new ManagerSettings_Foraging
        {
            DefaultSyncFilterAndAllowed = false,
            DefaultForceFullyMature = true,
        };
        var target = new ManagerDefaultSettings_Foraging();

        Assert.That(target.MigrateFrom(old)).Is.True();
        Assert.That(target.DefaultSyncFilterAndAllowed).Is.False();
        Assert.That(target.DefaultForceFullyMature).Is.True();
    }

    [Test]
    public static void ForagingMigrateFromRejectsUnrelatedLegacyType()
    {
        var target = new ManagerDefaultSettings_Foraging();
        Assert.That(target.MigrateFrom(new ManagerSettings_Hunting())).Is.False();
    }

    [Test]
    public static void ForestryMigrateFromCopiesAllFields()
    {
        var old = new ManagerSettings_Forestry
        {
            DefaultSyncFilterAndAllowed = false,
            DefaultForestryJobType = ManagerJob_Forestry.ForestryJobType.ClearArea,
            DefaultAllowSaplings = true,
        };
        var target = new ManagerDefaultSettings_Forestry();

        Assert.That(target.MigrateFrom(old)).Is.True();
        Assert.That(target.DefaultSyncFilterAndAllowed).Is.False();
        Assert
            .That(target.DefaultForestryJobType)
            .Is.EqualTo(ManagerJob_Forestry.ForestryJobType.ClearArea);
        Assert.That(target.DefaultAllowSaplings).Is.True();
    }

    [Test]
    public static void HuntingMigrateFromCopiesAllFields()
    {
        var old = new ManagerSettings_Hunting
        {
            DefaultSyncFilterAndAllowed = false,
            DefaultTargetResource = ManagerJob_Hunting.HuntingTargetResource.Leather,
            DefaultAllowHumanLikeMeat = true,
            DefaultAllowInsectMeat = true,
            DefaultAllowTwistedMeat = true,
            DefaultUnforbidCorpses = false,
            DefaultUnforbidAllCorpses = false,
            DefaultUnforbidHumanCorpses = true,
        };
        var target = new ManagerDefaultSettings_Hunting();

        Assert.That(target.MigrateFrom(old)).Is.True();
        Assert.That(target.DefaultSyncFilterAndAllowed).Is.False();
        Assert
            .That(target.DefaultTargetResource)
            .Is.EqualTo(ManagerJob_Hunting.HuntingTargetResource.Leather);
        Assert.That(target.DefaultAllowHumanLikeMeat).Is.True();
        Assert.That(target.DefaultAllowInsectMeat).Is.True();
        Assert.That(target.DefaultAllowTwistedMeat).Is.True();
        Assert.That(target.DefaultUnforbidCorpses).Is.False();
        Assert.That(target.DefaultUnforbidAllCorpses).Is.False();
        Assert.That(target.DefaultUnforbidHumanCorpses).Is.True();
    }

    [Test]
    public static void MiningMigrateFromCopiesAllFields()
    {
        var old = new ManagerSettings_Mining
        {
            DefaultSyncFilterAndAllowed = false,
            DefaultDeconstructBuildings = true,
            DefaultDeconstructAncientDangerWhenFogged = true,
            DefaultAllowMining = false,
            DefaultTakeOwnershipOfMiningJobs = true,
            DefaultControlDeepDrills = true,
            DefaultHaulMapChunks = false,
            DefaultHaulMinedChunks = false,
            DefaultMineThickRoofs = false,
            DefaultCheckRoofSupport = false,
            DefaultCheckRoofSupportAdvanced = true,
            DefaultCheckRoomDivision = false,
            DefaultTaskPriorityOrder = [ManagerJob_Mining.Task.Mine],
        };
        var target = new ManagerDefaultSettings_Mining();

        Assert.That(target.MigrateFrom(old)).Is.True();
        Assert.That(target.DefaultSyncFilterAndAllowed).Is.False();
        Assert.That(target.DefaultDeconstructBuildings).Is.True();
        Assert.That(target.DefaultDeconstructAncientDangerWhenFogged).Is.True();
        Assert.That(target.DefaultAllowMining).Is.False();
        Assert.That(target.DefaultTakeOwnershipOfMiningJobs).Is.True();
        Assert.That(target.DefaultControlDeepDrills).Is.True();
        Assert.That(target.DefaultHaulMapChunks).Is.False();
        Assert.That(target.DefaultHaulMinedChunks).Is.False();
        Assert.That(target.DefaultMineThickRoofs).Is.False();
        Assert.That(target.DefaultCheckRoofSupport).Is.False();
        Assert.That(target.DefaultCheckRoofSupportAdvanced).Is.True();
        Assert.That(target.DefaultCheckRoomDivision).Is.False();
        // The old priority order only listed one task; migration must still fill in every task
        // that exists now, same as a fresh load does.
        Assert
            .ThatCollection(target.DefaultTaskPriorityOrder)
            .Does.Contain(ManagerJob_Mining.Task.Mine);
        Assert
            .ThatCollection(target.DefaultTaskPriorityOrder)
            .Has.Count(Enum.GetValues(typeof(ManagerJob_Mining.Task)).Length);
    }

    [Test]
    public static void LivestockMigrateFromCopiesDefaultsAndFixesBackref()
    {
        var unrelatedPawnKind = new PawnKindDef { defName = "MigrationTestNoOverridePawnKind" };

        var old = new ManagerSettings_Livestock();
        old.defaults.DefaultTryTameMore = true;
        old.defaults.DefaultCullingStrategy = ManagerJob_Livestock.LivestockCullingStrategy.None;

        var target = new ManagerDefaultSettings_Livestock();

        Assert.That(target.MigrateFrom(old)).Is.True();
        // No override exists for this pawn kind, so this falls back to the migrated defaults.
        var migratedDefaults = target.GetSettingsFor(unrelatedPawnKind);
        Assert.That(migratedDefaults.DefaultTryTameMore).Is.True();
        Assert
            .That(migratedDefaults.DefaultCullingStrategy)
            .Is.EqualTo(ManagerJob_Livestock.LivestockCullingStrategy.None);
    }

    [Test]
    public static void LivestockMigrateFromCopiesOverridesKeyedByPawnKind()
    {
        var overriddenPawnKind = new PawnKindDef { defName = "MigrationTestOverridePawnKind" };
        var unrelatedPawnKind = new PawnKindDef { defName = "MigrationTestNoOverridePawnKind2" };

        var old = new ManagerSettings_Livestock();
        old.defaults.DefaultTamePastTargets = false;
        var legacyOverride = new LegacyPawnKindSettings_Livestock { DefaultTamePastTargets = true };
        old.overrides[overriddenPawnKind] = legacyOverride;

        var target = new ManagerDefaultSettings_Livestock();

        Assert.That(target.MigrateFrom(old)).Is.True();
        Assert.That(target.GetSettingsFor(overriddenPawnKind).DefaultTamePastTargets).Is.True();
        // The override must not have leaked into the defaults used by every other pawn kind.
        Assert.That(target.GetSettingsFor(unrelatedPawnKind).DefaultTamePastTargets).Is.False();
    }

    [Test]
    public static void LivestockMigrateFromRejectsUnrelatedLegacyType()
    {
        var target = new ManagerDefaultSettings_Livestock();
        Assert.That(target.MigrateFrom(new ManagerSettings_Foraging())).Is.False();
    }
}
