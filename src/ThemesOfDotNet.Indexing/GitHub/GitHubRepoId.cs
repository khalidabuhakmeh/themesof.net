namespace ThemesOfDotNet.Indexing.GitHub;

public record struct GitHubRepoId(string Owner, string Name)
    : IComparable<GitHubRepoId>, IComparable
{
    public bool Equals(GitHubRepoId other)
    {
        return string.Equals(Owner, other.Owner, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        var result = new HashCode();
        result.Add(Owner, StringComparer.OrdinalIgnoreCase);
        result.Add(Name, StringComparer.OrdinalIgnoreCase);
        return result.ToHashCode();
    }

    public int CompareTo(object? obj)
    {
        if (obj is GitHubRepoId other)
            return CompareTo(other);

        return 1;
    }

    public int CompareTo(GitHubRepoId other)
    {
        var result = string.Compare(Owner, other.Owner);
        if (result != 0)
            return result;

        return string.Compare(Name, other.Name);
    }

    public static GitHubRepoId Parse(string ownerAndName)
    {
        ArgumentNullException.ThrowIfNull(ownerAndName);

        var parts = ownerAndName.Split('/');
        if (parts.Length != 2)
            throw new FormatException();

        return new GitHubRepoId(parts[0], parts[1]);
    }

    public override string ToString()
    {
        return $"{Owner}/{Name}";
    }
}
