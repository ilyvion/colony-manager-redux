// AAAAManagerJobCompTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.AAAA.Core;
using RimTestRedux;

namespace ColonyManagerRedux.AAAA.Tests;

[TestSuite]
internal static class AAAAManagerJobCompTests
{
    /// <summary>
    /// An <see cref="ICollection{T}"/> that throws part way through <see cref="Add"/> once it has
    /// accepted <paramref name="failAfter"/> items, to simulate the kind of mid-swap failure
    /// <see cref="AAAAManagerJobCompField.TrySwapCollectionContents{T}"/> needs to recover from.
    /// </summary>
    private sealed class FlakyCollection<T>(List<T> items, int failAfter) : ICollection<T>
    {
        public int Count => items.Count;
        public bool IsReadOnly => false;

        public void Add(T item)
        {
            if (items.Count >= failAfter)
            {
                throw new InvalidOperationException("Simulated failure");
            }
            items.Add(item);
        }

        public void Clear() => items.Clear();

        public bool Contains(T item) => items.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);

        public bool Remove(T item) => items.Remove(item);

        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Test]
    public static void SuccessfulSwapReplacesContentsAndReportsNoFailures()
    {
        var backing = new List<string> { "old1", "old2" };
        ICollection<string> collection = backing;
        var swapFailed = false;
        var restoreFailed = false;

        AAAAManagerJobCompField.TrySwapCollectionContents(
            collection,
            ["new1", "new2", "new3"],
            _ => swapFailed = true,
            _ => restoreFailed = true
        );

        Assert.That(swapFailed).Is.False();
        Assert.That(restoreFailed).Is.False();
        Assert.ThatCollection(collection).Has.Count(3);
        Assert.ThatCollection(collection).Does.Contain("new1");
        Assert.ThatCollection(collection).Does.Contain("new3");
    }

    [Test]
    public static void FailedSwapRestoresOriginalContents()
    {
        // Regression guard: a swap that fails part way through must not leave the collection
        // in a partially cleared/filled, corrupted state.
        var backing = new List<string> { "old1", "old2" };
        var collection = new FlakyCollection<string>(backing, failAfter: 2);
        var swapFailed = false;
        var restoreFailed = false;

        AAAAManagerJobCompField.TrySwapCollectionContents(
            collection,
            ["new1", "new2", "new3"],
            _ => swapFailed = true,
            _ => restoreFailed = true
        );

        Assert.That(swapFailed).Is.True();
        Assert.That(restoreFailed).Is.False();
        Assert.ThatCollection(backing).Has.Count(2);
        Assert.ThatCollection(backing).Does.Contain("old1");
        Assert.ThatCollection(backing).Does.Contain("old2");
    }

    [Test]
    public static void FailedSwapAndFailedRestoreBothReportFailure()
    {
        var backing = new List<string> { "old1", "old2" };
        // failAfter: 0 means even re-adding the original contents during restore will fail.
        var collection = new FlakyCollection<string>(backing, failAfter: 0);
        var swapFailed = false;
        var restoreFailed = false;

        AAAAManagerJobCompField.TrySwapCollectionContents(
            collection,
            ["new1"],
            _ => swapFailed = true,
            _ => restoreFailed = true
        );

        Assert.That(swapFailed).Is.True();
        Assert.That(restoreFailed).Is.True();
    }

    [Test]
    public static void TryGetAreaToRestoreReturnsFirstAreaWhenPreviousAreasIsNonEmpty()
    {
        var previousAreas = new List<string?> { "old1", "old2" };

        var found = AAAAManagerJobCompField.TryGetAreaToRestore(previousAreas, out var area);

        Assert.That(found).Is.True();
        Assert.That(area).Is.EqualTo("old1");
    }

    [Test]
    public static void TryGetAreaToRestoreFailsWhenPreviousAreasIsEmpty()
    {
        // Regression guard: normal mode firing without a matching prior danger-mode
        // call (e.g. a comp added mid-danger-mode) must not throw an IndexOutOfRangeException
        // from indexing an empty previous-areas list.
        var previousAreas = new List<string?>();

        var found = AAAAManagerJobCompField.TryGetAreaToRestore(previousAreas, out var area);

        Assert.That(found).Is.False();
        Assert.That(area is null).Is.True();
    }

    private const string Suffix = " \\(Safe\\)";

    [Test]
    public static void MatchesSafeAreaLabelMatchesPlainLabelWithSuffix() =>
        Assert
            .That(AAAAManagerJobCompField.MatchesSafeAreaLabel("Home (Safe)", "Home", Suffix))
            .Is.True();

    [Test]
    public static void MatchesSafeAreaLabelIsCaseInsensitive() =>
        Assert
            .That(AAAAManagerJobCompField.MatchesSafeAreaLabel("HOME (SAFE)", "home", Suffix))
            .Is.True();

    [Test]
    public static void MatchesSafeAreaLabelEscapesRegexMetacharactersInAreaLabel()
    {
        // Regression guard for commit 3466824: an area label containing regex metacharacters
        // must be escaped, not interpreted as a pattern, or this either throws or silently
        // matches the wrong areas.
        Assert
            .That(AAAAManagerJobCompField.MatchesSafeAreaLabel("a.b*c (Safe)", "a.b*c", Suffix))
            .Is.True();
        Assert
            .That(AAAAManagerJobCompField.MatchesSafeAreaLabel("aXbXXXc (Safe)", "a.b*c", Suffix))
            .Is.False();
    }

    [Test]
    public static void MatchesSafeAreaLabelHandlesParenthesesInAreaLabel() =>
        // e.g. an area literally named "Home (1)".
        Assert
            .That(
                AAAAManagerJobCompField.MatchesSafeAreaLabel("Home (1) (Safe)", "Home (1)", Suffix)
            )
            .Is.True();

    [Test]
    public static void MatchesSafeAreaLabelRejectsLabelWithoutSuffix() =>
        Assert
            .That(AAAAManagerJobCompField.MatchesSafeAreaLabel("Home", "Home", Suffix))
            .Is.False();

    [Test]
    public static void BuildSafeAreaPatternEscapesAreaLabel() =>
        Assert
            .That(AAAAManagerJobCompField.BuildSafeAreaPattern("a.b*c", Suffix))
            .Is.EqualTo("a\\.b\\*c \\(Safe\\)");
}
