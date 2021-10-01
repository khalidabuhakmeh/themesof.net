using System.Text.Json.Serialization;

namespace ThemesOfDotNet.Indexing.Configuration;

public sealed class MilestoneConfiguration
{
    public static MilestoneConfiguration Empty { get; } = new();

    private MilestoneConfiguration()
        : this(null, null, null, null)
    {
    }

    public MilestoneConfiguration(IReadOnlyList<string>? patterns,
                                  IReadOnlyDictionary<string, string>? suffixNameMappings,
                                  IReadOnlyDictionary<string, string>? productNameMappings,
                                  IReadOnlyDictionary<string, string>? productVersionMappings)
    {
        Patterns = patterns ?? Array.Empty<string>();
        SuffixNameMappings = suffixNameMappings ?? new Dictionary<string, string>();
        ProductNameMappings = productNameMappings ?? new Dictionary<string, string>();
        ProductVersionMappings = productVersionMappings ?? new Dictionary<string, string>();
    }

    public IReadOnlyList<string> Patterns { get; }

    [JsonConverter(typeof(CaseInsensitiveDictionaryConverter))]
    public IReadOnlyDictionary<string, string> SuffixNameMappings { get; }

    [JsonConverter(typeof(CaseInsensitiveDictionaryConverter))]
    public IReadOnlyDictionary<string, string> ProductNameMappings { get; }

    [JsonConverter(typeof(CaseInsensitiveDictionaryConverter))]
    public IReadOnlyDictionary<string, string> ProductVersionMappings { get; }
}
