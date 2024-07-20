// Triplet.cs
// Copyright Karel Kroeze, 2017-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public readonly struct Triplet<T1, T2, T3>(T1 first, T2 second, T3 third) : IEquatable<Triplet<T1, T2, T3>>
{
    public T1 First { get; } = first;

    public T2 Second { get; } = second;

    public T3 Third { get; } = third;

    public bool Equals(Triplet<T1, T2, T3> other)
    {
        return EqualityComparer<T1>.Default.Equals(First, other.First) &&
               EqualityComparer<T2>.Default.Equals(Second, other.Second) &&
               EqualityComparer<T3>.Default.Equals(Third, other.Third);
    }

    public override bool Equals(object obj)
    {
        if (obj is null)
        {
            return false;
        }

        return obj is Triplet<T1, T2, T3> triplet && Equals(triplet);
    }

    public override int GetHashCode() => HashCode.Combine(First, Second, Third);

    public static bool operator ==(Triplet<T1, T2, T3> left, Triplet<T1, T2, T3> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Triplet<T1, T2, T3> left, Triplet<T1, T2, T3> right)
    {
        return !(left == right);
    }
}
