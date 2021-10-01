using System.Text.Json;

using Humanizer;

namespace ThemesOfDotNet.Indexing.AzureDevOps;

public sealed class AzureDevOpsFieldChange
{
    public AzureDevOpsFieldChange(string actor,
                                  DateTimeOffset when,
                                  AzureDevOpsField field,
                                  object? from,
                                  object? to)
    {
        ArgumentNullException.ThrowIfNull(actor);

        Actor = actor;
        When = when;
        Field = field;
        From = UnpackJsonElement(from);
        To = UnpackJsonElement(to);

        static object? UnpackJsonElement(object? value)
        {
            if (value is null)
                return null;

            if (value is JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Null:
                        return null;
                    case JsonValueKind.String:
                        return element.GetString();
                    case JsonValueKind.Number:
                        return element.GetUInt64();
                    case JsonValueKind.Array:
                        return UnpackStringArray(element);
                }
            }

            return value;
        }

        static string[]? UnpackStringArray(JsonElement element)
        {
            foreach (var arrayElement in element.EnumerateArray())
            {
                if (arrayElement.ValueKind != JsonValueKind.String)
                    return null;
            }

            return element.EnumerateArray()
                          .Select(e => e.GetString()!)
                          .ToArray();
        }

    }

    public string Actor { get; }
    public DateTimeOffset When { get; }
    public AzureDevOpsField Field { get; }
    public object? From { get; }
    public object? To { get; }

    public override string ToString()
    {
        return $"{When.Humanize()} {Actor} changed {Field} from '{From}' to '{To}'";
    }
}
