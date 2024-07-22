// Enumerable_Extensions.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public static class Enumerable_Extensions
{
    public static IEnumerable<(T, T)> ChunkToTuple<T>(this IEnumerable<T> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        using var enumerator = items.GetEnumerator();

        while (enumerator.MoveNext())
        {
            T first = enumerator.Current;
            bool hasSecond = enumerator.MoveNext();
            if (!hasSecond)
            {
                throw new ArgumentException("Collection must have even number of elements.");
            }
            T second = enumerator.Current;

            yield return (first, second);
        }
    }

    public static (int, int) SumTuple(this IEnumerable<(int, int)> source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        int num1 = 0;
        int num2 = 0;
        foreach (var item in source)
        {
            num1 = checked(num1 + item.Item1);
            num2 = checked(num2 + item.Item2);
        }
        return (num1, num2);
    }
}
