using System.Text.RegularExpressions;

namespace ThemesOfDotNet.Indexing.AzureDevOps;

public record struct AzureDevOpsQueryId(string ServerUrl, string Id)
    : IComparable<AzureDevOpsQueryId>, IComparable
{
    public string Url => $"{ServerUrl}/_queries/query/{Id}";

    public bool Equals(AzureDevOpsQueryId other)
    {
        return string.Equals(ServerUrl, other.ServerUrl, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        var result = new HashCode();
        result.Add(ServerUrl, StringComparer.OrdinalIgnoreCase);
        result.Add(Id, StringComparer.OrdinalIgnoreCase);
        return result.ToHashCode();
    }

    public int CompareTo(object? obj)
    {
        if (obj is AzureDevOpsQueryId other)
            return CompareTo(other);

        return 1;
    }

    public int CompareTo(AzureDevOpsQueryId other)
    {
        var result = ServerUrl.CompareTo(other.ServerUrl);
        if (result != 0)
            return result;

        return Id.CompareTo(other.Id);
    }

    public static AzureDevOpsQueryId Parse(string text)
    {
        if (TryParse(text, out var result))
            return result;

        throw new FormatException($"'{text}' isn't a valid Azure DevOps query");
    }

    public static bool TryParse(string? text, out AzureDevOpsQueryId result)
    {
        result = default;

        if (string.IsNullOrEmpty(text))
            return false;

        var match = Regex.Match(text, @"(?<ServerUrl>(https?://[a-z0-9_]+.visualstudio.com)|(https?://dev.azure.com/[a-z0-9_]+))/(([a-z0-9._]+)/)*_queries/query(-edit)?/(?<QueryId>[a-zA-Z0-9-]+)", RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase);

        if (!match.Success)
            return false;

        var serverUrl = match.Groups["ServerUrl"].Value;
        var queryId = match.Groups["QueryId"].Value;

        result = new AzureDevOpsQueryId(serverUrl, queryId);
        return true;
    }

    public override string ToString()
    {
        return $"azdo#{Id}";
    }
}
