// UtilitiesScribeIntsTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Text format used to save the history buffers (<c>Scribe_IntArray</c>/<c>Scribe_IntTupleArray</c>).
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class UtilitiesScribeIntsTests
{
    [Test]
    public static void IntsRoundTrip() =>
        Expect.That(() =>
            Utilities.ParseInts(Utilities.FormatInts([1, 20, 300])).SequenceEqual([1, 20, 300])
        );

    [Test]
    public static void NegativeIntsRoundTrip() =>
        Expect.That(() =>
            Utilities.ParseInts(Utilities.FormatInts([-5, 0, 7])).SequenceEqual([-5, 0, 7])
        );

    [Test]
    public static void EmptyBufferRoundTripsToEmpty()
    {
        Expect.AreEqual("", Utilities.FormatInts([]));
        Expect.IsEmpty(Utilities.ParseInts(""));
        Expect.IsEmpty(Utilities.ParseInts(null));
    }

    [Test]
    public static void EmptyTupleBufferRoundTripsToEmpty()
    {
        Expect.AreEqual("", Utilities.FormatIntTuples([]));
        Expect.IsEmpty(Utilities.ParseIntTuples(""));
        Expect.IsEmpty(Utilities.ParseIntTuples(null));
    }

    [Test]
    public static void TuplesRoundTrip() =>
        Expect.That(() =>
            Utilities
                .ParseIntTuples(Utilities.FormatIntTuples([(1, 2), (-3, 4)]))
                .SequenceEqual([(1, 2), (-3, 4)])
        );

    // Some cultures (e.g. Swedish) write the minus sign as U+2212, which int.Parse only accepts
    // under that culture. Saves must load the same whatever the current culture is.
    [Test]
    public static void ParsingIgnoresCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            Expect.That(() => Utilities.ParseInts("-5:6").SequenceEqual([-5, 6]));
            Expect.That(() => Utilities.ParseIntTuples("-5,6").SequenceEqual([(-5, 6)]));
            Expect.AreEqual("-5:6", Utilities.FormatInts([-5, 6]));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
