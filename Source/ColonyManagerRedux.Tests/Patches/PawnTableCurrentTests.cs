// PawnTableCurrentTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Which <see cref="PawnTable"/> <see cref="PawnTableCurrentExtensions.IsCurrentTable"/> reports
/// as current while a table's <c>Columns</c> getter runs.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class PawnTableCurrentTests
{
    private static PawnTable NewTable() => new(new PawnTableDef(), () => [], 100, 100);

    [Test]
    public static void OnlyTheTableBeingReadIsCurrent()
    {
        var outer = NewTable();
        var other = NewTable();

        RimWorld_PawnTable_Columns.Prefix(outer, out var state);
        try
        {
            Expect.IsTrue(outer.IsCurrentTable(), "table being read");
            Expect.IsFalse(other.IsCurrentTable(), "other table");
        }
        finally
        {
            RimWorld_PawnTable_Columns.Postfix(state);
        }
    }

    [Test]
    public static void NoTableIsCurrentOnceTheReadFinishes()
    {
        var table = NewTable();

        RimWorld_PawnTable_Columns.Prefix(table, out var state);
        RimWorld_PawnTable_Columns.Postfix(state);

        Expect.IsNull(RimWorld_PawnTable_Columns.CurrentPawnTable);
        Expect.IsFalse(table.IsCurrentTable());
    }

    // A column worker's VisibleCurrently can read another table's Columns while the outer
    // table's read is still filtering its columns; the outer table must stay current after.
    [Test]
    public static void NestedReadRestoresTheOuterTable()
    {
        var outer = NewTable();
        var inner = NewTable();

        RimWorld_PawnTable_Columns.Prefix(outer, out var outerState);
        try
        {
            RimWorld_PawnTable_Columns.Prefix(inner, out var innerState);
            Expect.IsTrue(inner.IsCurrentTable(), "inner during its read");
            RimWorld_PawnTable_Columns.Postfix(innerState);

            Expect.IsTrue(outer.IsCurrentTable(), "outer after the nested read");
            Expect.IsFalse(inner.IsCurrentTable(), "inner after its read");
        }
        finally
        {
            RimWorld_PawnTable_Columns.Postfix(outerState);
        }
        Expect.IsNull(RimWorld_PawnTable_Columns.CurrentPawnTable);
    }
}
