using System.Text.RegularExpressions;

namespace ThemesOfDotNet.Indexing.AzureDevOps;

public record struct AzureDevOpsWorkItemId(string ServerUrl, int Number)
    : IComparable<AzureDevOpsWorkItemId>, IComparable
{
    public string Url => $"{ServerUrl}/_workitems/edit/{Number}";

    public bool Equals(AzureDevOpsWorkItemId other)
    {
        return string.Equals(ServerUrl, other.ServerUrl, StringComparison.OrdinalIgnoreCase) &&
               Number == other.Number;
    }

    public override int GetHashCode()
    {
        var result = new HashCode();
        result.Add(ServerUrl, StringComparer.OrdinalIgnoreCase);
        result.Add(Number);
        return result.ToHashCode();
    }

    public int CompareTo(object? obj)
    {
        if (obj is AzureDevOpsWorkItemId other)
            return CompareTo(other);

        return 1;
    }

    public int CompareTo(AzureDevOpsWorkItemId other)
    {
        var result = ServerUrl.CompareTo(other.ServerUrl);
        if (result != 0)
            return result;

        return Number.CompareTo(other.Number);
    }

    public static AzureDevOpsWorkItemId Parse(string text)
    {
        if (TryParse(text, out var result))
            return result;

        throw new FormatException($"'{text}' isn't a valid Azure DevOps work item");
    }

    public static bool TryParse(string? text, out AzureDevOpsWorkItemId result)
    {
        result = default;

        if (string.IsNullOrEmpty(text))
            return false;

        var match = Regex.Match(text, @"(?<ServerUrl>https?://[a-zA-Z0-9._]+)/((?<Project>[a-zA-Z0-9._]+)/)?_workitems/edit/(?<Number>[0-9]+)", RegexOptions.IgnorePatternWhitespace);

        if (!match.Success)
            return false;

        var serverUrl = match.Groups["ServerUrl"].Value;
        var numberText = match.Groups["Number"].Value;

        if (!int.TryParse(numberText, out var number))
            return false;

        result = new AzureDevOpsWorkItemId(serverUrl, number);
        return true;
    }

    public override string ToString()
    {
        return $"azdo#{Number}";
    }
}
