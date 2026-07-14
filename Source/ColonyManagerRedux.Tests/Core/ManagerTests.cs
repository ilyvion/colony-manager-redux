// ManagerTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerTests
{
    [Test]
    public static void FindLowestUnusedIdReturnsZeroWhenNoneUsed() =>
        Assert.That(Manager.FindLowestUnusedId([])).Is.EqualTo(0);

    [Test]
    public static void FindLowestUnusedIdReturnsFirstGapAfterContiguousRun() =>
        Assert.That(Manager.FindLowestUnusedId([0, 1, 2, 3])).Is.EqualTo(4);

    [Test]
    public static void FindLowestUnusedIdReturnsGapInMiddle() =>
        Assert.That(Manager.FindLowestUnusedId([0, 1, 3])).Is.EqualTo(2);

    [Test]
    public static void FindLowestUnusedIdReturnsZeroWhenZeroNotUsed() =>
        Assert.That(Manager.FindLowestUnusedId([1, 2, 3])).Is.EqualTo(0);

    [Test]
    public static void FindLowestUnusedIdHandlesDuplicateAndUnorderedInput() =>
        Assert.That(Manager.FindLowestUnusedId([3, 1, 1, 0, 3, 2])).Is.EqualTo(4);

    // Regression guard for the CHANGELOG [Unreleased] fix: an ID being requested before a
    // manager had finished loading, or the internal ID counter wrapping back to 0 after
    // int.MaxValue job creations, could previously hand out a duplicate ID.
    [Test]
    public static void FindLowestUnusedIdNeverReturnsAnAlreadyUsedId()
    {
        var used = new[] { 0, 1, 2, 4, 5 };
        var result = Manager.FindLowestUnusedId(used);
        Assert.ThatCollection(used).Does.Not.Contain(result);
        Assert.That(result).Is.EqualTo(3);
    }

    [Test]
    public static void ShouldApplyDefaultTemplateFalseWhenAlreadyChecked() =>
        Assert
            .That(
                Manager.ShouldApplyDefaultTemplate(
                    alreadyChecked: true,
                    autoApplyEnabled: true,
                    templateName: "Foo",
                    templateExists: _ => true
                )
            )
            .Is.False();

    [Test]
    public static void ShouldApplyDefaultTemplateFalseWhenAutoApplyDisabled() =>
        Assert
            .That(
                Manager.ShouldApplyDefaultTemplate(
                    alreadyChecked: false,
                    autoApplyEnabled: false,
                    templateName: "Foo",
                    templateExists: _ => true
                )
            )
            .Is.False();

    [Test]
    public static void ShouldApplyDefaultTemplateFalseWhenTemplateNameNullOrEmpty()
    {
        Assert
            .That(
                Manager.ShouldApplyDefaultTemplate(
                    alreadyChecked: false,
                    autoApplyEnabled: true,
                    templateName: null,
                    templateExists: _ => true
                )
            )
            .Is.False();

        Assert
            .That(
                Manager.ShouldApplyDefaultTemplate(
                    alreadyChecked: false,
                    autoApplyEnabled: true,
                    templateName: "",
                    templateExists: _ => true
                )
            )
            .Is.False();
    }

    [Test]
    public static void ShouldApplyDefaultTemplateFalseWhenTemplateDoesNotExist() =>
        Assert
            .That(
                Manager.ShouldApplyDefaultTemplate(
                    alreadyChecked: false,
                    autoApplyEnabled: true,
                    templateName: "Foo",
                    templateExists: _ => false
                )
            )
            .Is.False();

    [Test]
    public static void ShouldApplyDefaultTemplateTrueWhenAllConditionsSatisfied() =>
        Assert
            .That(
                Manager.ShouldApplyDefaultTemplate(
                    alreadyChecked: false,
                    autoApplyEnabled: true,
                    templateName: "Foo",
                    templateExists: _ => true
                )
            )
            .Is.True();

    [Test]
    public static void ShouldSkipDefaultTemplateForModMismatchTrueOnlyWhenBothTrue()
    {
        Assert
            .That(
                Manager.ShouldSkipDefaultTemplateForModMismatch(
                    wouldApply: true,
                    modMismatchDetected: true
                )
            )
            .Is.True();

        Assert
            .That(
                Manager.ShouldSkipDefaultTemplateForModMismatch(
                    wouldApply: true,
                    modMismatchDetected: false
                )
            )
            .Is.False();

        Assert
            .That(
                Manager.ShouldSkipDefaultTemplateForModMismatch(
                    wouldApply: false,
                    modMismatchDetected: true
                )
            )
            .Is.False();

        Assert
            .That(
                Manager.ShouldSkipDefaultTemplateForModMismatch(
                    wouldApply: false,
                    modMismatchDetected: false
                )
            )
            .Is.False();
    }
}
