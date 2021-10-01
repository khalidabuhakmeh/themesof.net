namespace ThemesOfDotNet.Indexing.Querying.Ranges;

public sealed class UnaryRangeSyntax<T> : RangeSyntax<T>
{
    internal UnaryRangeSyntax(UnaryRangeOperator op, T operand)
    {
        Op = op;
        Operand = operand;
    }

    public UnaryRangeOperator Op { get; }

    public T Operand { get; }

    public override bool Contains(T value)
    {
        var comparer = Comparer<T>.Default;
        var c = comparer.Compare(value, Operand);
        return Op switch
        {
            UnaryRangeOperator.EqualTo => c == 0,
            UnaryRangeOperator.LessThan => c < 0,
            UnaryRangeOperator.LessThanOrEqual => c <= 0,
            UnaryRangeOperator.GreaterThan => c > 0,
            UnaryRangeOperator.GreaterThanOrEqual => c >= 0,
            _ => throw new Exception($"Unexpected operator {Op}")
        };
    }

    public override string ToString()
    {
        return Op switch
        {
            UnaryRangeOperator.EqualTo => $"{Operand}",
            UnaryRangeOperator.LessThan => $"<{Operand}",
            UnaryRangeOperator.LessThanOrEqual => $"<={Operand}",
            UnaryRangeOperator.GreaterThan => $">{Operand}",
            UnaryRangeOperator.GreaterThanOrEqual => $">={Operand}",
            _ => throw new Exception($"Unexpected operator {Op}")
        };
    }
}
