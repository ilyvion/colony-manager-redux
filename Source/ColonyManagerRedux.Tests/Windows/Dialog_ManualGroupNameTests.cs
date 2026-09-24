// Dialog_ManualGroupNameTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;
using static ColonyManagerRedux.Managers.Dialog_ManualGroupName;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class Dialog_ManualGroupNameTests
{
    [Test]
    public static void IsDuplicateGroupNameIsTrueForCaseInsensitiveMatchAgainstOtherGroups()
    {
        var existingGroups = new List<string> { "Alpha", "Beta" };

        Expect.IsTrue(IsDuplicateGroupName("alpha", null, existingGroups));
        Expect.IsFalse(IsDuplicateGroupName("Gamma", null, existingGroups));
    }

    [Test]
    public static void IsDuplicateGroupNameIsFalseWhenNameMatchesItsOwnOriginalName()
    {
        // Renaming a group to the name it already has must not be flagged as a duplicate.
        var existingGroups = new List<string> { "Alpha", "Beta" };

        Expect.IsFalse(IsDuplicateGroupName("Alpha", "Alpha", existingGroups));
        Expect.IsFalse(IsDuplicateGroupName("alpha", "Alpha", existingGroups));
        Expect.IsTrue(IsDuplicateGroupName("Beta", "Alpha", existingGroups));
    }
}
