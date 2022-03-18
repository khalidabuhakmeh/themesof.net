using System.Text;
using System.Text.RegularExpressions;

namespace ThemesOfDotNet.Indexing.AzureDevOps;

public readonly struct AzureDevOpsAreaPathExpression
{
    private readonly Regex _regex;

    public AzureDevOpsAreaPathExpression(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        _regex = new Regex(CreateRegularExpression(pattern), RegexOptions.IgnoreCase);
        Pattern = pattern;
    }

    public string Pattern { get; }

    private static string CreateRegularExpression(string pattern)
    {
        var sb = new StringBuilder();
        sb.Append('^');
        var parts = pattern.Split('\\');

        foreach (var part in parts)
        {
            if (sb.Length > 1)
                sb.Append("\\\\");

            if (part == "*")
                sb.Append("[^\\]+");
            else if (part == "**")
                sb.Append(".*");
            else
                sb.Append(Regex.Escape(part));
        }

        sb.Append('$');
        return sb.ToString();
    }

    public bool IsMatch(string areaPath)
    {
        if (!areaPath.EndsWith('\\'))
            areaPath += '\\';

        return _regex.IsMatch(areaPath);
    }

    public override string ToString()
    {
        return Pattern;
    }
}
