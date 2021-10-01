using System.CodeDom.Compiler;
using System.Diagnostics;

using ThemesOfDotNet.Indexing.Querying.Syntax;

namespace ThemesOfDotNet.Indexing.Querying.Binding;

public abstract class BoundQuery
{
    public static IReadOnlyList<IReadOnlyList<BoundQuery>> Create(QuerySyntax syntax)
    {
        ArgumentNullException.ThrowIfNull(syntax);

        var result = CreateInternal(syntax);
        var dnf = ToDisjunctiveNormalForm(result);
        return Flatten(dnf);
    }

    private static BoundQuery CreateInternal(QuerySyntax syntax)
    {
        switch (syntax.Kind)
        {
            case QuerySyntaxKind.TextQuery:
                return CreateTextExpression((TextQuerySyntax)syntax);
            case QuerySyntaxKind.KeyValueQuery:
                return CreateKeyValueExpression((KeyValueQuerySyntax)syntax);
            case QuerySyntaxKind.OrQuery:
                return CreateOrExpression((OrQuerySyntax)syntax);
            case QuerySyntaxKind.AndQuery:
                return CreateAndExpression((AndQuerySyntax)syntax);
            case QuerySyntaxKind.NegatedQuery:
                return CreateNegatedExpression((NegatedQuerySyntax)syntax);
            case QuerySyntaxKind.ParenthesizedQuery:
                return CreateParenthesizedExpression((ParenthesizedQuerySyntax)syntax);
            default:
                throw new Exception($"Unexpected node {syntax.Kind}");
        }
    }

    private static BoundQuery CreateTextExpression(TextQuerySyntax node)
    {
        Debug.Assert(node.TextToken.Value is not null);

        return new BoundTextQuery(false, node.TextToken.Value);
    }

    private static BoundQuery CreateKeyValueExpression(KeyValueQuerySyntax node)
    {
        Debug.Assert(node.KeyToken.Value is not null);
        Debug.Assert(node.ValueToken.Value is not null);

        var key = node.KeyToken.Value;
        var value = node.ValueToken.Value;
        return new BoundKevValueQuery(isNegated: false, key, value);
    }

    private static BoundQuery CreateOrExpression(OrQuerySyntax node)
    {
        return new BoundOrQuery(CreateInternal(node.Left), CreateInternal(node.Right));
    }

    private static BoundQuery CreateAndExpression(AndQuerySyntax node)
    {
        return new BoundAndQuery(CreateInternal(node.Left), CreateInternal(node.Right));
    }

    private static BoundQuery CreateNegatedExpression(NegatedQuerySyntax node)
    {
        return new BoundNegatedQuery(CreateInternal(node.Query));
    }

    private static BoundQuery CreateParenthesizedExpression(ParenthesizedQuerySyntax node)
    {
        return CreateInternal(node.Query);
    }

    private static BoundQuery ToDisjunctiveNormalForm(BoundQuery node)
    {
        if (node is BoundNegatedQuery negated)
            return ToDisjunctiveNormalForm(Negate(negated.Query));

        if (node is BoundOrQuery or)
        {
            var left = ToDisjunctiveNormalForm(or.Left);
            var right = ToDisjunctiveNormalForm(or.Right);
            if (ReferenceEquals(left, or.Left) &&
                ReferenceEquals(right, or.Right))
                return node;

            return new BoundOrQuery(left, right);
        }

        if (node is BoundAndQuery and)
        {
            var left = ToDisjunctiveNormalForm(and.Left);
            var right = ToDisjunctiveNormalForm(and.Right);

            // (A OR B) AND C      ->    (A AND C) OR (B AND C)

            if (left is BoundOrQuery leftOr)
            {
                var a = leftOr.Left;
                var b = leftOr.Right;
                var c = right;
                return new BoundOrQuery(
                    ToDisjunctiveNormalForm(new BoundAndQuery(a, c)),
                    ToDisjunctiveNormalForm(new BoundAndQuery(b, c))
                );
            }

            // A AND (B OR C)      ->    (A AND B) OR (A AND C)

            if (right is BoundOrQuery rightOr)
            {
                var a = left;
                var b = rightOr.Left;
                var c = rightOr.Right;
                return new BoundOrQuery(
                    ToDisjunctiveNormalForm(new BoundAndQuery(a, b)),
                    ToDisjunctiveNormalForm(new BoundAndQuery(a, c))
                );
            }

            return new BoundAndQuery(left, right);
        }

        return node;
    }

    private static BoundQuery Negate(BoundQuery node)
    {
        switch (node)
        {
            case BoundKevValueQuery kevValue:
                return NegateKevValueQuery(kevValue);
            case BoundTextQuery text:
                return NegateTextQuery(text);
            case BoundNegatedQuery negated:
                return NegateNegatedQuery(negated);
            case BoundAndQuery and:
                return NegateAndQuery(and);
            case BoundOrQuery or:
                return NegateOrQuery(or);
            default:
                throw new Exception($"Unexpected node {node.GetType()}");
        }
    }

    private static BoundQuery NegateKevValueQuery(BoundKevValueQuery node)
    {
        return new BoundKevValueQuery(!node.IsNegated, node.Key, node.Value);
    }

    private static BoundQuery NegateTextQuery(BoundTextQuery node)
    {
        return new BoundTextQuery(!node.IsNegated, node.Text);
    }

    private static BoundQuery NegateNegatedQuery(BoundNegatedQuery node)
    {
        return node.Query;
    }

    private static BoundQuery NegateAndQuery(BoundAndQuery node)
    {
        return new BoundOrQuery(Negate(node.Left), Negate(node.Right));
    }

    private static BoundQuery NegateOrQuery(BoundOrQuery node)
    {
        return new BoundAndQuery(Negate(node.Left), Negate(node.Right));
    }

    private static IReadOnlyList<IReadOnlyList<BoundQuery>> Flatten(BoundQuery node)
    {
        var disjunctions = new List<IReadOnlyList<BoundQuery>>();
        var conjunctions = new List<BoundQuery>();

        foreach (var or in FlattenOrs(node))
        {
            conjunctions.Clear();

            foreach (var conjunction in FlattenAnds(or))
                conjunctions.Add(conjunction);

            disjunctions.Add(conjunctions.ToArray());
        }

        return disjunctions.ToArray();
    }

    private static IEnumerable<BoundQuery> FlattenAnds(BoundQuery node)
    {
        var stack = new Stack<BoundQuery>();
        var result = new List<BoundQuery>();
        stack.Push(node);

        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (n is not BoundAndQuery and)
            {
                result.Add(n);
            }
            else
            {
                stack.Push(and.Right);
                stack.Push(and.Left);
            }
        }

        return result;
    }

    private static IEnumerable<BoundQuery> FlattenOrs(BoundQuery node)
    {
        var stack = new Stack<BoundQuery>();
        var result = new List<BoundQuery>();
        stack.Push(node);

        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (n is not BoundOrQuery or)
            {
                result.Add(n);
            }
            else
            {
                stack.Push(or.Right);
                stack.Push(or.Left);
            }
        }

        return result;
    }

    public override string ToString()
    {
        using var stringWriter = new StringWriter();
        {
            using var indentedTextWriter = new IndentedTextWriter(stringWriter);
            Walk(indentedTextWriter, this);

            return stringWriter.ToString();
        }

        static void Walk(IndentedTextWriter writer, BoundQuery node)
        {
            switch (node)
            {
                case BoundKevValueQuery kevValue:
                    writer.WriteLine($"{(kevValue.IsNegated ? "-" : "")}{kevValue.Key}:{kevValue.Value}");
                    break;
                case BoundTextQuery text:
                    writer.WriteLine($"{(text.IsNegated ? "-" : "")}{text.Text}");
                    break;
                case BoundNegatedQuery negated:
                    writer.WriteLine("NOT");
                    writer.Indent++;
                    Walk(writer, negated.Query);
                    writer.Indent--;
                    break;
                case BoundAndQuery and:
                    writer.WriteLine("AND");
                    writer.Indent++;
                    Walk(writer, and.Left);
                    Walk(writer, and.Right);
                    writer.Indent--;
                    break;
                case BoundOrQuery or:
                    writer.WriteLine("OR");
                    writer.Indent++;
                    Walk(writer, or.Left);
                    Walk(writer, or.Right);
                    writer.Indent--;
                    break;
                default:
                    throw new Exception($"Unexpected node {node.GetType()}");
            }
        }
    }
}
