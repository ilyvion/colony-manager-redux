// UtilitiesLivestockTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class UtilitiesLivestockTests
{
    // Pins down the AgeAndSex <-> IsAdult/IsMale mapping. A code comment on the enum warns its
    // values are save-critical ("do not change them without the proper care"); this guards
    // against an accidental reorder silently inverting age/sex classification.
    [Test]
    public static void AgeAndSexIsAdultAndIsMaleMappingIsPinned()
    {
        Expect.IsTrue(AgeAndSex.AdultFemale.IsAdult());
        Expect.IsFalse(AgeAndSex.AdultFemale.IsMale());

        Expect.IsTrue(AgeAndSex.AdultMale.IsAdult());
        Expect.IsTrue(AgeAndSex.AdultMale.IsMale());

        Expect.IsFalse(AgeAndSex.JuvenileFemale.IsAdult());
        Expect.IsFalse(AgeAndSex.JuvenileFemale.IsMale());

        Expect.IsFalse(AgeAndSex.JuvenileMale.IsAdult());
        Expect.IsTrue(AgeAndSex.JuvenileMale.IsMale());
    }

    [Test]
    public static void IsOfAgeSexBoundaryAtLifeStageIndexTwo()
    {
        // lifeStageIndex >= 2 is adult, per the deliberate simplification documented at the call
        // site.
        Expect.IsTrue(Utilities_Livestock.IsOfAgeSex(Gender.Male, 2, AgeAndSex.AdultMale));
        Expect.IsFalse(Utilities_Livestock.IsOfAgeSex(Gender.Male, 1, AgeAndSex.AdultMale));
        Expect.IsTrue(Utilities_Livestock.IsOfAgeSex(Gender.Male, 1, AgeAndSex.JuvenileMale));
        Expect.IsTrue(Utilities_Livestock.IsOfAgeSex(Gender.Female, 2, AgeAndSex.AdultFemale));
        Expect.IsTrue(Utilities_Livestock.IsOfAgeSex(Gender.Female, 1, AgeAndSex.JuvenileFemale));
    }

    [Test]
    public static void IsOfAgeSexTreatsNonMaleGendersAsFemale()
    {
        // Deliberate simplification documented at the call site: anything non-male counts as
        // female, including Gender.None.
        Expect.IsTrue(Utilities_Livestock.IsOfAgeSex(Gender.None, 2, AgeAndSex.AdultFemale));
        Expect.IsFalse(Utilities_Livestock.IsOfAgeSex(Gender.None, 2, AgeAndSex.AdultMale));
    }
}
