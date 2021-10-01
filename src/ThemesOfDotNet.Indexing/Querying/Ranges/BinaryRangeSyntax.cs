namespace ThemesOfDotNet.Indexing.Querying.Ranges;

public sealed class BinaryRangeSyntax<T> : RangeSyntax<T>
{
    internal BinaryRangeSyntax(T lowerBound, T upperBound)
    {
        LowerBound = lowerBound;
        UpperBound = upperBound;
    }

    public T LowerBound { get; }

    public T UpperBound { get; }

    public override bool Contains(T value)
    {
        var comparer = Comparer<T>.Default;
        var c1 = comparer.Compare(LowerBound, value);
        var c2 = comparer.Compare(value, UpperBound);
        return c1 <= 0 && c2 <= 0;
    }

    public override string ToString()
    {
        return $"{LowerBound}..{UpperBound}";
    }
}
