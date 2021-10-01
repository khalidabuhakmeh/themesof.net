namespace ThemesOfDotNet.Indexing.GitHub;

public readonly struct GitHubAreaPath : IEquatable<GitHubAreaPath>, IEnumerable<GitHubAreaPath>
{
    private readonly IReadOnlyList<string>? _segments;
    private readonly string? _fullName;

    public GitHubAreaPath(IEnumerable<string> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        _segments = segments.ToArray();
        _fullName = string.Join("/", _segments);
    }

    public IReadOnlyList<string> Segments => _segments ?? Array.Empty<string>();

    public string FullName => _fullName ?? string.Empty;

    public string Name => Segments.LastOrDefault() ?? string.Empty;

    public GitHubAreaPath GetPath(int segmentIndex)
    {
        if (segmentIndex < 0 || segmentIndex >= Segments.Count - 1)
            throw new ArgumentOutOfRangeException(nameof(segmentIndex));

        return new GitHubAreaPath(Segments.Take(segmentIndex));
    }

    public static bool TryParse(string? text, out GitHubAreaPath result)
    {
        if (text != null)
        {
            var separators = new[] { '-', '.', ':' };
            var options = StringSplitOptions.RemoveEmptyEntries |
                          StringSplitOptions.TrimEntries;
            var parts = text.Split(separators, options);
            var isAreaLabel = parts.Length > 0 &&
                              string.Equals(parts[0], "area", StringComparison.OrdinalIgnoreCase);
            if (isAreaLabel)
            {
                var segments = parts.Skip(1);
                result = new GitHubAreaPath(segments);
                return true;
            }
        }

        result = default;
        return false;
    }

    public override bool Equals(object? obj)
    {
        return obj is GitHubAreaPath other && Equals(other);
    }

    public bool Equals(GitHubAreaPath other)
    {
        return string.Equals(FullName, other.FullName, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        var result = new HashCode();
        result.Add(FullName, StringComparer.OrdinalIgnoreCase);
        return result.ToHashCode();
    }

    public IEnumerator<GitHubAreaPath> GetEnumerator()
    {
        for (var i = 0; i < Segments.Count; i++)
            yield return GetPath(i);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public static bool operator ==(GitHubAreaPath left, GitHubAreaPath right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(GitHubAreaPath left, GitHubAreaPath right)
    {
        return !(left == right);
    }
}
