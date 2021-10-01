using System.Reflection;

using ThemesOfDotNet.Indexing.Querying.Binding;
using ThemesOfDotNet.Indexing.Querying.Ranges;
using ThemesOfDotNet.Indexing.Querying.Syntax;
using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Indexing.Querying;

public abstract class Query<T> : Query
{
    private static readonly Predicate<T> _alwaysTrue = _ => true;
    private readonly Predicate<T> _predicate;

    protected Query(QueryContext context, string text)
        : base(context, text)
    {
        _predicate = CreatePredicate();
    }

    public bool Evaluate(T value)
    {
        return _predicate(value);
    }

    public override IEnumerable<string> GetSyntaxHelp()
    {
        var handlers = GetHandlers();

        foreach (var ((key, value), _) in handlers.Predicates.OrderBy(p => p.Key)
                                                             .ThenBy(p => p.Value))
        {
            if (value is not null)
                yield return $"{key}:{value}";
            else
                yield return $"{key}:<value>";
        }
    }

    protected abstract QueryHandlers GetHandlers();

    protected abstract bool ContainsText(T value, string text);

    private Predicate<T> CreatePredicate()
    {
        if (Text.Length == 0)
            return _alwaysTrue;

        var syntax = QuerySyntax.Parse(Text);
        var boundQuery = BoundQuery.Create(syntax);

        Predicate<T>? predicate = null;

        foreach (var disjunction in boundQuery)
        {
            Predicate<T>? disjunctionPredicate = null;

            foreach (var conjunction in disjunction)
            {
                var next = CreatePredicate(conjunction);
                var current = disjunctionPredicate;
                disjunctionPredicate = current is null
                    ? next
                    : new Predicate<T>(v => current(v) && next(v));
            }

            if (disjunctionPredicate is not null)
            {
                var next = disjunctionPredicate;
                var current = predicate;
                predicate = current is null
                    ? next
                    : new Predicate<T>(v => current(v) || next(v));
            }
        }

        return predicate ?? _alwaysTrue;
    }

    private Predicate<T> CreatePredicate(BoundQuery node)
    {
        return node switch
        {
            BoundTextQuery textQuery => CreatePredicate(textQuery),
            BoundKevValueQuery keyValueQuery => CreatePredicate(keyValueQuery),
            _ => throw new Exception($"Unexpected node: {node.GetType()}")
        };
    }

    private Predicate<T> CreatePredicate(BoundTextQuery node)
    {
        return node.IsNegated
                ? wi => !ContainsText(wi, node.Text)
                : wi => ContainsText(wi, node.Text);
    }

    private Predicate<T> CreatePredicate(BoundKevValueQuery node)
    {
        var key = node.Key.ToLowerInvariant();
        var value = node.Value.ToLowerInvariant();

        var handlers = GetHandlers();

        if (handlers.Predicates.TryGetValue((key, value), out var predicateHandler) ||
            handlers.Predicates.TryGetValue((key, null), out predicateHandler))
        {
            return node.IsNegated
                    ? v => !predicateHandler(this, v, node)
                    : v => predicateHandler(this, v, node);
        }
        else if (handlers.Modifiers.TryGetValue((key, value), out var modifierHandler) ||
                 handlers.Modifiers.TryGetValue((key, null), out modifierHandler))
        {
            modifierHandler(this, node);
            return _alwaysTrue;
        }

        return CreatePredicate(new BoundTextQuery(node.IsNegated, $"{key}:{value}"));
    }

    private string ExpandVariables(string value)
    {
        if (string.Equals(value, "@me", StringComparison.OrdinalIgnoreCase) && Context.UserName is not null)
            return Context.UserName;

        return value;
    }

    protected static QueryHandlers CreateHandlers(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var predicates = new Dictionary<(string Key, string? Value), Func<Query<T>, T, BoundKevValueQuery, bool>>();
        var modifiers = new Dictionary<(string Key, string? Value), Action<Query<T>, BoundKevValueQuery>>();
        var methods = type.GetMethods(BindingFlags.Instance |
                                      BindingFlags.Static |
                                      BindingFlags.NonPublic);

        foreach (var method in methods)
        {
            var attribute = method.GetCustomAttributesData()
                                  .SingleOrDefault(ca => ca.AttributeType == typeof(QueryHandlerAttribute));
            if (attribute is null)
                continue;

            if (attribute.ConstructorArguments.Count != 1)
                throw new Exception($"Wrong number of arguments for [{nameof(QueryHandlerAttribute)}] on {method}");

            if (attribute.ConstructorArguments[0].ArgumentType != typeof(string[]))
                throw new Exception($"Wrong type of arguments for [{nameof(QueryHandlerAttribute)}] on {method}");

            var args = (ICollection<CustomAttributeTypedArgument>?)attribute.ConstructorArguments[0].Value;

            if (args is null || args.Count == 0)
                throw new Exception($"Wrong number of arguments for [{nameof(QueryHandlerAttribute)}] on {method}");

            var strings = args.Select(a => (string)a.Value!);
            var pairs = GetKeyValues(strings).ToArray();
            var parameters = method.GetParameters();

            Action<Query<T>, BoundKevValueQuery>? modifierHandler = null;
            Func<Query<T>, T, BoundKevValueQuery, bool>? predicateHandler = null;

            if (parameters.Length == 0)
            {
                modifierHandler = (instance, query) =>
                {
                    method.Invoke(instance, Array.Empty<object>());
                };
            }
            else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
            {
                modifierHandler = (instance, query) =>
                {
                    method.Invoke(instance, new object?[] { query.Value });
                };
            }
            else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(T))
            {
                predicateHandler = (instance, value, query) =>
                {
                    return (bool)method.Invoke(instance, new object?[] { value })!;
                };
            }
            else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(T) &&
                                               parameters[1].ParameterType == typeof(string))
            {
                predicateHandler = (instance, value, query) =>
                {
                    var text = instance.ExpandVariables(query.Value);
                    return (bool)method.Invoke(instance, new object?[] { value, text })!;
                };
            }
            else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(T) &&
                                               parameters[1].ParameterType == typeof(RangeSyntax<int>))
            {
                predicateHandler = (instance, value, query) =>
                {
                    if (RangeSyntax.ParseInt32(query.Value) is RangeSyntax<int> range)
                        return (bool)method.Invoke(instance, new object?[] { value, range })!;
                    else
                        return false;
                };
            }
            else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(T) &&
                                               parameters[1].ParameterType == typeof(RangeSyntax<DateTimeOffset>))
            {
                predicateHandler = (instance, value, query) =>
                {
                    if (RangeSyntax.ParseDateTimeOffset(query.Value) is RangeSyntax<DateTimeOffset> range)
                        return (bool)method.Invoke(instance, new object?[] { value, range })!;
                    else
                        return false;
                };
            }
            else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(T) &&
                                               parameters[1].ParameterType == typeof(RangeSyntax<WorkItemCost>))
            {
                predicateHandler = (instance, value, query) =>
                {
                    if (RangeSyntax.ParseCost(query.Value) is RangeSyntax<WorkItemCost> range)
                        return (bool)method.Invoke(instance, new object?[] { value, range })!;
                    else
                        return false;
                };
            }
            else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(T) &&
                                               parameters[1].ParameterType == typeof(RangeSyntax<WorkItemVersion>))
            {
                predicateHandler = (instance, value, query) =>
                {
                    if (RangeSyntax.ParseVersion(query.Value) is RangeSyntax<WorkItemVersion> range)
                        return (bool)method.Invoke(instance, new object?[] { value, range })!;
                    else
                        return false;
                };
            }
            else
            {
                throw new Exception($"Unexpected signature for {method}");
            }

            if (modifierHandler is not null)
            {
                foreach (var kv in pairs)
                    modifiers.Add(kv!, modifierHandler);
            }

            if (predicateHandler is not null)
            {
                foreach (var kv in pairs)
                    predicates.Add(kv!, predicateHandler);
            }
        }

        return new QueryHandlers(predicates, modifiers);
    }

    private static IEnumerable<(string Key, string? Value)> GetKeyValues(IEnumerable<string> pairs)
    {
        foreach (var pair in pairs)
        {
            var kv = pair.Split(":");
            if (kv.Length == 1)
                yield return (kv[0], null);
            else if (kv.Length == 2)
                yield return (kv[0], kv[1]);
            else
                throw new ArgumentException($"Invalid syntax: '{pair}'", nameof(pairs));
        }
    }

    protected sealed class QueryHandlers
    {
        public QueryHandlers(
            IReadOnlyDictionary<(string Key, string? Value), Func<Query<T>, T, BoundKevValueQuery, bool>> predicates,
            IReadOnlyDictionary<(string Key, string? Value), Action<Query<T>, BoundKevValueQuery>> modifiers)
        {
            Predicates = predicates;
            Modifiers = modifiers;
        }

        public IReadOnlyDictionary<(string Key, string? Value), Func<Query<T>, T, BoundKevValueQuery, bool>> Predicates { get; }
        public IReadOnlyDictionary<(string Key, string? Value), Action<Query<T>, BoundKevValueQuery>> Modifiers { get; }
    }
}
