// ManagerDefaultSettingsLivestockTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The per-pawn-kind override plumbing of <see cref="ManagerDefaultSettings_Livestock"/>: the
/// <see cref="PawnKindSettings"/> copy constructor, <c>GetSettingsFor</c> and <c>RemoveOverride</c>.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class ManagerDefaultSettingsLivestockTests
{
    private static readonly PawnKindDef Kind = PawnKindDefOf.Muffalo;

    private static PawnKindSettings Copy(PawnKindSettings source) => new(Kind, source);

    [Test]
    public static void CopyConstructorCopiesEveryPublicField()
    {
        var source = new PawnKindSettings();
        Expect.GreaterThan(ManagerSettingsScribeTests.FillWithNonDefaults(source), 0);

        var copy = Copy(source);

        Expect.AreEqual(
            "",
            string.Join("; ", ManagerSettingsScribeTests.Differences(source, copy))
        );
    }

    [Test]
    public static void CopyConstructorCopiesCountTargetsByValue()
    {
        var source = new PawnKindSettings();
        source.DefaultCountTargets = [.. source.DefaultCountTargets.Select((_, i) => i + 10)];

        var copy = Copy(source);
        copy.DefaultCountTargets[0] = 999;

        Expect.AreEqual(10, source.DefaultCountTargets[0]);
        Expect.AreEqual(source.DefaultCountTargets.Length, copy.DefaultCountTargets.Length);
    }

    [Test]
    public static void CopyConstructorCopiesTrainingTargetsByValue()
    {
        var source = new PawnKindSettings();
        _ = source.EnabledTrainingTargets.Add(TrainableDefOf.Obedience);

        var copy = Copy(source);
        Expect.IsTrue(copy.EnabledTrainingTargets.Contains(TrainableDefOf.Obedience));
        _ = copy.EnabledTrainingTargets.Add(TrainableDefOf.Tameness);

        Expect.IsFalse(source.EnabledTrainingTargets.Contains(TrainableDefOf.Tameness));
    }

    [Test]
    public static void CopyConstructorSeedsTheCountTextBuffersFromTheCopiedTargets()
    {
        var source = new PawnKindSettings();
        source.DefaultCountTargets = [.. source.DefaultCountTargets.Select((_, i) => i + 10)];

        var copy = Copy(source);

        Expect.AreEqual(
            string.Join(",", copy.DefaultCountTargets),
            string.Join(",", copy._newCounts)
        );
    }

    [Test]
    public static void CountTargetsAndTextBuffersHaveOneEntryPerAgeSex()
    {
        var settings = new PawnKindSettings();

        Expect.AreEqual(
            Utilities_Livestock.AgeSexArray.Length,
            settings.DefaultCountTargets.Length
        );
        Expect.AreEqual(Utilities_Livestock.AgeSexArray.Length, settings._newCounts.Length);
    }

    [Test]
    public static void GetSettingsForFallsBackToTheSharedDefaultsWithoutAnOverride()
    {
        var settings = new ManagerDefaultSettings_Livestock();

        Expect.ReferencesAreEqual(settings.defaults, settings.GetSettingsFor(Kind));
    }

    [Test]
    public static void GetSettingsForReturnsTheOverrideOnlyForItsPawnKind()
    {
        var settings = new ManagerDefaultSettings_Livestock();
        var @override = Copy(settings.defaults);
        settings.overrides[Kind] = @override;

        Expect.ReferencesAreEqual(@override, settings.GetSettingsFor(Kind));
        Expect.ReferencesAreEqual(
            settings.defaults,
            settings.GetSettingsFor(PawnKindDefOf.Thrumbo)
        );
    }

    [Test]
    public static void GetSettingsForFallsBackAgainAfterRemoveOverride()
    {
        var settings = new ManagerDefaultSettings_Livestock();
        settings.overrides[Kind] = Copy(settings.defaults);

        settings.RemoveOverride(Kind);

        Expect.ReferencesAreEqual(settings.defaults, settings.GetSettingsFor(Kind));
    }

    [Test]
    public static void RemoveOverrideResetsTheSelectedTab()
    {
        var settings = new ManagerDefaultSettings_Livestock();
        var @override = Copy(settings.defaults);
        settings.overrides[Kind] = @override;
        settings._currentLivestockSettingsTab = 0;
        settings.currentOverrideTab = @override;

        settings.RemoveOverride(Kind);

        Expect.AreEqual(-1, settings._currentLivestockSettingsTab);
        Expect.IsNull(settings.currentOverrideTab);
    }

    [Test]
    public static void RemoveOverrideWithoutAnOverrideIsHarmless()
    {
        var settings = new ManagerDefaultSettings_Livestock();

        settings.RemoveOverride(Kind);

        Expect.ReferencesAreEqual(settings.defaults, settings.GetSettingsFor(Kind));
    }
}
