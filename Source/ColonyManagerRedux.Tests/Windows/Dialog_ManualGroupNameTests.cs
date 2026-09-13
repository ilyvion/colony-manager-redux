// Dialog_ManualGroupNameTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;
using static ColonyManagerRedux.Managers.Dialog_ManualGroupName;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class Dialog_ManualGroupNameTests
{
    [Test]
    public static void IsDuplicateGroupNameIsTrueForCaseInsensitiveMatchAgainstOtherGroups()
    {
        var existingGroups = new List<string> { "Alpha", "Beta" };

        Assert.That(IsDuplicateGroupName("alpha", null, existingGroups)).Is.True();
        Assert.That(IsDuplicateGroupName("Gamma", null, existingGroups)).Is.False();
    }

    [Test]
    public static void IsDuplicateGroupNameIsFalseWhenNameMatchesItsOwnOriginalName()
    {
        // Renaming a group to the name it already has must not be flagged as a duplicate.
        var existingGroups = new List<string> { "Alpha", "Beta" };

        Assert.That(IsDuplicateGroupName("Alpha", "Alpha", existingGroups)).Is.False();
        Assert.That(IsDuplicateGroupName("alpha", "Alpha", existingGroups)).Is.False();
        Assert.That(IsDuplicateGroupName("Beta", "Alpha", existingGroups)).Is.True();
    }
}
