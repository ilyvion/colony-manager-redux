// Utilities_Planning.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

internal static class Utilities_Planning
{
    /// <summary>
    /// Walks <paramref name="candidates"/> in order, taking at most <paramref name="limit"/> of
    /// them (all of them when <paramref name="limit"/> is null), and pairs each with the running
    /// count after applying <paramref name="step"/> to <paramref name="startCount"/> once per
    /// taken item. Lazy: candidates beyond the limit are never enumerated, so callers can
    /// throttle between items.
    /// </summary>
    internal static IEnumerable<(T Item, int CountAfter)> WalkRunningCount<T>(
        int startCount,
        int step,
        int? limit,
        IEnumerable<T> candidates
    )
    {
        if (limit <= 0)
        {
            yield break;
        }

        var count = startCount;
        var taken = 0;
        using var enumerator = candidates.GetEnumerator();
        while (taken < (limit ?? int.MaxValue) && enumerator.MoveNext())
        {
            count += step;
            taken++;
            yield return (enumerator.Current, count);
        }
    }

    internal readonly record struct AdditionStep<T>(
        T Item,
        int Yield,
        int CountAfter,
        bool Accepted
    );

    /// <summary>
    /// Walks <paramref name="candidates"/> in order, adding each one's yield to a running count
    /// until <paramref name="targetMet"/> holds for it or <paramref name="canAddMore"/> is false.
    /// Both are checked before each candidate, and <paramref name="canAddMore"/> is read lazily,
    /// so a caller that records accepted steps as it iterates sees its own additions. A
    /// candidate <paramref name="accept"/> rejects is still yielded, with
    /// <see cref="AdditionStep{T}.Accepted"/> false and no change to the count.
    /// </summary>
    internal static IEnumerable<AdditionStep<T>> WalkAdditions<T>(
        int startCount,
        IEnumerable<T> candidates,
        Func<T, int> yieldOf,
        Func<int, bool> targetMet,
        Func<bool> canAddMore,
        Func<T, bool>? accept = null
    )
    {
        var count = startCount;
        foreach (var candidate in candidates)
        {
            if (targetMet(count) || !canAddMore())
            {
                yield break;
            }

            var itemYield = yieldOf(candidate);
            var accepted = accept?.Invoke(candidate) ?? true;
            if (accepted)
            {
                count += itemYield;
            }

            yield return new(candidate, itemYield, count, accepted);
        }
    }
}
