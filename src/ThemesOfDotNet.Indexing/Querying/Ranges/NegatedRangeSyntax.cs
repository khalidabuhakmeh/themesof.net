namespace ThemesOfDotNet.Indexing.Querying.Ranges;

public sealed class NegatedRangeSyntax<T> : RangeSyntax<T>
{
    internal NegatedRangeSyntax(RangeSyntax<T> operand)
    {
        Operand = operand;
    }

    public RangeSyntax<T> Operand { get; }

    public override bool Contains(T value)
    {
        return !Operand.Contains(value);
    }

    public override string ToString()
    {
        return $"not {Operand}";
    }
}
