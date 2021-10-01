using System.Text;
using System.Text.RegularExpressions;

namespace ThemesOfDotNet.Indexing.WorkItems;

public struct WorkItemVersion : IEquatable<WorkItemVersion>, IComparable<WorkItemVersion>, IComparable
{
    public WorkItemVersion(int major, int minor, int build, string? suffix)
    {
        if (major < 0)
            throw new ArgumentOutOfRangeException(nameof(major));

        if (minor < 0)
            throw new ArgumentOutOfRangeException(nameof(major));

        if (build < 0)
            throw new ArgumentOutOfRangeException(nameof(major));

        Major = major;
        Minor = minor;
        Build = build;
        Suffix = CanonicalizeSuffix(suffix);
    }

    private static string? CanonicalizeSuffix(string? suffix)
    {
        if (string.IsNullOrEmpty(suffix))
            return null;

        return suffix;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Build { get; }
    public string? Suffix { get; }

    public override bool Equals(object? obj)
    {
        return obj is WorkItemVersion other && Equals(other);
    }

    public bool Equals(WorkItemVersion other)
    {
        return Major == other.Major &&
               Minor == other.Minor &&
               Build == other.Build &&
               string.Equals(Suffix, other.Suffix, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        var result = new HashCode();
        result.Add(Major);
        result.Add(Minor);
        result.Add(Build);
        result.Add(Suffix, StringComparer.OrdinalIgnoreCase);
        return result.ToHashCode();
    }

    public int CompareTo(WorkItemVersion other)
    {
        var result = Major.CompareTo(other.Major);
        if (result != 0)
            return result;

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
            return result;

        result = Build.CompareTo(other.Build);
        if (result != 0)
            return result;

        if (Suffix is not null && other.Suffix is not null)
        {
            // TODO: Handle dotted numbers
            var matcher = new Regex("^(?<prefix>)[^0-9](?<numbers>[0-9]+)$");
            var left = matcher.Match(Suffix);
            var right = matcher.Match(other.Suffix);

            if (left.Success && right.Success)
            {
                var leftPrefix = left.Groups["prefix"].Value;
                var rightPrefix = left.Groups["prefix"].Value;

                result = string.Compare(leftPrefix, rightPrefix, StringComparison.OrdinalIgnoreCase);
                if (result != 0)
                    return result;

                var leftNumberText = left.Groups["numbers"].Value;
                var rightNumberText = right.Groups["numbers"].Value;

                if (int.TryParse(leftNumberText, out var leftNumber) &&
                    int.TryParse(rightNumberText, out var rightNumber))
                {
                    return leftNumber.CompareTo(rightNumber);
                }
            }
        }

        if (Suffix is null && other.Suffix is not null)
            return 1;

        if (Suffix is not null && other.Suffix is null)
            return -1;

        return string.Compare(Suffix, other.Suffix, StringComparison.OrdinalIgnoreCase);
    }

    int IComparable.CompareTo(object? obj)
    {
        if (obj is WorkItemVersion other)
            return CompareTo(other);

        return 1;
    }

    public static bool TryParse(string? text, out WorkItemVersion result)
    {
        if (text is not null)
        {
            var match = Regex.Match(text, "^(?<major>[0-9]+)(.(?<minor>[0-9]+))?(.(?<band>[0-9]+x*|x+))?((-|\\s)(?<suffix>.*))?$");
            if (match.Success)
            {
                if (int.TryParse(match.Groups["major"].Value, out var major))
                {
                    if (int.TryParse(match.Groups["minor"].Value, out var minor))
                    {
                        var band = match.Groups["band"].Value;
                        if (band.Length == 0)
                            band = "0";

                        if (int.TryParse(band.Replace("x", "0", StringComparison.OrdinalIgnoreCase), out var build))
                        {
                            var suffix = match.Groups["suffix"].Value;
                            result = new WorkItemVersion(major, minor, build, suffix);
                            return true;
                        }
                    }
                }
            }
        }

        result = default;
        return false;
    }


    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(Major);
        sb.Append('.');
        sb.Append(Minor);

        if (Build != 0)
        {
            sb.Append('.');
            sb.Append(Build);
        }

        if (Suffix is not null)
        {
            sb.Append('-');
            sb.Append(Suffix);
        }

        return sb.ToString();
    }

    public static bool operator ==(WorkItemVersion left, WorkItemVersion right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(WorkItemVersion left, WorkItemVersion right)
    {
        return !(left == right);
    }

    public static bool operator <(WorkItemVersion left, WorkItemVersion right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(WorkItemVersion left, WorkItemVersion right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(WorkItemVersion left, WorkItemVersion right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(WorkItemVersion left, WorkItemVersion right)
    {
        return left.CompareTo(right) >= 0;
    }
}
