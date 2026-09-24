// ManagerDefaultSettingsMigrationTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

// Guards against a save written before default-value settings for a job moved out of that job's
// ManagerSettings subclass into a dedicated ManagerDefaultSettings subclass losing those values:
// MigrateFrom must copy every field the old class held, under its new name.
[TestFixture(TestType.MainMenu)]
internal sealed class ManagerDefaultSettingsMigrationTests
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

        Expect.IsTrue(target.MigrateFrom(old));
        Expect.IsFalse(target.DefaultSyncFilterAndAllowed);
        Expect.IsTrue(target.DefaultForceFullyMature);
    }

    [Test]
    public static void ForagingMigrateFromRejectsUnrelatedLegacyType()
    {
        var target = new ManagerDefaultSettings_Foraging();
        Expect.IsFalse(target.MigrateFrom(new ManagerSettings_Hunting()));
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

        Expect.IsTrue(target.MigrateFrom(old));
        Expect.IsFalse(target.DefaultSyncFilterAndAllowed);
        Expect.AreEqual(
            ManagerJob_Forestry.ForestryJobType.ClearArea,
            target.DefaultForestryJobType
        );
        Expect.IsTrue(target.DefaultAllowSaplings);
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

        Expect.IsTrue(target.MigrateFrom(old));
        Expect.IsFalse(target.DefaultSyncFilterAndAllowed);
        Expect.AreEqual(
            ManagerJob_Hunting.HuntingTargetResource.Leather,
            target.DefaultTargetResource
        );
        Expect.IsTrue(target.DefaultAllowHumanLikeMeat);
        Expect.IsTrue(target.DefaultAllowInsectMeat);
        Expect.IsTrue(target.DefaultAllowTwistedMeat);
        Expect.IsFalse(target.DefaultUnforbidCorpses);
        Expect.IsFalse(target.DefaultUnforbidAllCorpses);
        Expect.IsTrue(target.DefaultUnforbidHumanCorpses);
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

        Expect.IsTrue(target.MigrateFrom(old));
        Expect.IsFalse(target.DefaultSyncFilterAndAllowed);
        Expect.IsTrue(target.DefaultDeconstructBuildings);
        Expect.IsTrue(target.DefaultDeconstructAncientDangerWhenFogged);
        Expect.IsFalse(target.DefaultAllowMining);
        Expect.IsTrue(target.DefaultTakeOwnershipOfMiningJobs);
        Expect.IsTrue(target.DefaultControlDeepDrills);
        Expect.IsFalse(target.DefaultHaulMapChunks);
        Expect.IsFalse(target.DefaultHaulMinedChunks);
        Expect.IsFalse(target.DefaultMineThickRoofs);
        Expect.IsFalse(target.DefaultCheckRoofSupport);
        Expect.IsTrue(target.DefaultCheckRoofSupportAdvanced);
        Expect.IsFalse(target.DefaultCheckRoomDivision);
        // The old priority order only listed one task; migration must still fill in every task
        // that exists now, same as a fresh load does.
        Expect.IsTrue(target.DefaultTaskPriorityOrder.Contains(ManagerJob_Mining.Task.Mine));
        Expect.AreEqual(
            Enum.GetValues(typeof(ManagerJob_Mining.Task)).Length,
            target.DefaultTaskPriorityOrder.Count
        );
    }

    [Test]
    public static void LivestockMigrateFromCopiesDefaultsAndFixesBackref()
    {
        var unrelatedPawnKind = new PawnKindDef { defName = "MigrationTestNoOverridePawnKind" };

        var old = new ManagerSettings_Livestock();
        old.defaults.DefaultTryTameMore = true;
        old.defaults.DefaultCullingStrategy = ManagerJob_Livestock.LivestockCullingStrategy.None;

        var target = new ManagerDefaultSettings_Livestock();

        Expect.IsTrue(target.MigrateFrom(old));
        // No override exists for this pawn kind, so this falls back to the migrated defaults.
        var migratedDefaults = target.GetSettingsFor(unrelatedPawnKind);
        Expect.IsTrue(migratedDefaults.DefaultTryTameMore);
        Expect.AreEqual(
            ManagerJob_Livestock.LivestockCullingStrategy.None,
            migratedDefaults.DefaultCullingStrategy
        );
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

        Expect.IsTrue(target.MigrateFrom(old));
        Expect.IsTrue(target.GetSettingsFor(overriddenPawnKind).DefaultTamePastTargets);
        // The override must not have leaked into the defaults used by every other pawn kind.
        Expect.IsFalse(target.GetSettingsFor(unrelatedPawnKind).DefaultTamePastTargets);
    }

    [Test]
    public static void LivestockMigrateFromRejectsUnrelatedLegacyType()
    {
        var target = new ManagerDefaultSettings_Livestock();
        Expect.IsFalse(target.MigrateFrom(new ManagerSettings_Foraging()));
    }

    // Migration runs before EnsureManagerDefaultSettingsAreCorrect() has a chance to strip
    // null/invalid entries that Scribe's Deep collection loading can leave behind, so the lookup
    // it uses must not throw on a null entry (see PostLoadInit NullReferenceException regression).
    [Test]
    public static void FindManagerDefaultSettingsForSkipsNullEntries()
    {
        var def = new ManagerDef { defName = "MigrationTestManagerDef" };
        var target = new ManagerDefaultSettings_Foraging { Def = def };
        List<ManagerDefaultSettings> settingsList = [null!, target];

        var found = Settings.FindManagerDefaultSettingsFor(settingsList, def);

        Expect.IsTrue(ReferenceEquals(found, target));
    }

    [Test]
    public static void FindManagerDefaultSettingsForReturnsNullWhenOnlyNullEntriesPresent()
    {
        var def = new ManagerDef { defName = "MigrationTestManagerDefMissing" };
        List<ManagerDefaultSettings> settingsList = [null!];

        var found = Settings.FindManagerDefaultSettingsFor(settingsList, def);

        Expect.IsTrue(found is null);
    }
}
