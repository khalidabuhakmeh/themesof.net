using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using ThemesOfDotNet.Indexing.Configuration;
using ThemesOfDotNet.Indexing.Releases;

namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed partial class Workspace
{
    private sealed class WorkItemMilestoneBuilder
    {
        private readonly Workspace _workspace;
        private readonly SubscriptionConfiguration _configuration;
        private readonly Dictionary<string, WorkItemProduct> _products = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(WorkItemProduct, WorkItemVersion), WorkItemMilestone> _milestones = new();
        private readonly List<ProductVersionMapping> _versionMappings = new();
        private readonly HashSet<string> _legalProductNames = new(StringComparer.OrdinalIgnoreCase);

        public WorkItemMilestoneBuilder(Workspace workspace, IReadOnlyList<ReleaseInfo> releases, SubscriptionConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ArgumentNullException.ThrowIfNull(configuration);

            _workspace = workspace;
            _configuration = configuration;

            foreach (var (range, productName) in _configuration.Milestones.ProductVersionMappings)
            {
                var versionMapping = ProductVersionMapping.Parse(range, productName);
                _versionMappings.Add(versionMapping);
            }

            foreach (var release in releases)
            {
                if (!WorkItemVersion.TryParse(release.Version, out var workItemVersion))
                    continue;

                var product = GetOrCreateProduct(release.ProductName);
                GetOrCreateMilestone(product, workItemVersion, release.Date);
            }

            // Record known product names

            foreach (var release in releases)
                _legalProductNames.Add(release.ProductName);

            foreach (var productName in _configuration.Milestones.ProductNameMappings.Values)
                _legalProductNames.Add(productName);

            foreach (var productName in _configuration.Milestones.ProductVersionMappings.Values)
                _legalProductNames.Add(productName);

            foreach (var org in configuration.GitHubOrgs)
            {
                if (org.DefaultProduct is not null)
                    _legalProductNames.Add(org.DefaultProduct);

                _legalProductNames.UnionWith(org.MappedProducts);

                foreach (var (_, repo) in org.Repos)
                {
                    if (repo.DefaultProduct is not null)
                        _legalProductNames.Add(repo.DefaultProduct);

                    _legalProductNames.UnionWith(repo.MappedProducts);
                }
            }
        }

        private IReadOnlyList<string> MapProductNames(string org, string repo)
        {
            if (_configuration.GitHubOrgByName.TryGetValue(org, out var orgConfiguration))
            {
                if (orgConfiguration.Repos.TryGetValue(repo, out var repoConfiguration) && repoConfiguration.MappedProducts.Any())
                    return repoConfiguration.MappedProducts;

                return orgConfiguration.MappedProducts;
            }

            return Array.Empty<string>();
        }

        public WorkItemMilestone? MapGitHubMilestone(string org, string repo, string? milestone)
        {
            var productNames = MapProductNames(org, repo);
            return MapMilestone(milestone, productNames);
        }

        public WorkItemMilestone? MapAzureDevOpsMilestone(string? milestone)
        {
            return MapMilestone(milestone, new[] { "VS", ".NET" });
        }

        private WorkItemMilestone? MapMilestone(string? milestone, IReadOnlyList<string> productNames)
        {
            if (milestone is not null)
            {
                foreach (var pattern in _configuration.Milestones.Patterns)
                {
                    var parsedMilestone = ParseMilestone(milestone, productNames, pattern);
                    if (parsedMilestone is not null)
                        return parsedMilestone;
                }
            }

            return null;
        }

        private WorkItemMilestone? ParseMilestone(string milestone, IReadOnlyList<string> productNames, string pattern)
        {
            var match = Regex.Match(milestone, pattern);
            if (!match.Success)
                return null;

            var productText = MapProductName(match.Groups["product"].Value.Trim());
            var versionText = match.Groups["version"].Value.Trim();
            var bandText = match.Groups["band"].Value.Trim();
            var suffixName = MapSuffixName(match.Groups["suffixName"].Value.Trim());
            var suffixNumber = match.Groups["suffixNumber"].Value.Trim();
            var suffixText = suffixName + suffixNumber;

            if (!TryParseVersion(versionText, out var version))
                return null;

            if (version.Build >= 0 && bandText.Length > 0)
                return null;

            if (version.Revision >= 0)
                return null;

            var build = version.Build;

            if (bandText.Length > 0)
            {
                if (!int.TryParse(bandText.Replace("x", "0", StringComparison.OrdinalIgnoreCase), out build))
                    return null;
            }

            if (build < 0)
                build = 0;

            if (productText.Length > 0 && !_legalProductNames.Contains(productText))
                return null;

            var productName = productText.Length > 0
                                ? productText
                                : MapVersionToProduct(version, productNames);

            if (productName is null)
                return null;

            var product = GetOrCreateProduct(productName);
            var major = version.Major;
            var minor = version.Minor;
            var workItemVersion = new WorkItemVersion(major, minor, build, suffixText);

            return GetOrCreateMilestone(product, workItemVersion, null);

            static bool TryParseVersion(string text, [NotNullWhen(true)] out Version? version)
            {
                if (int.TryParse(text, out var singleNumber))
                {
                    version = new Version(singleNumber, 0);
                    return true;
                }

                return Version.TryParse(text, out version);
            }
        }

        private string MapProductName(string name)
        {
            if (_configuration.Milestones.ProductNameMappings.TryGetValue(name, out var mappedName))
                return mappedName;

            return name;
        }

        private string? MapVersionToProduct(Version version, IReadOnlyList<string> productNames)
        {
            if (productNames.Count == 1)
                return productNames.Single();

            foreach (var mapping in _versionMappings)
            {
                if (productNames.Contains(mapping.ProductName, StringComparer.OrdinalIgnoreCase) &&
                    mapping.Contains(version))
                    return mapping.ProductName;
            }

            return null;
        }

        private string MapSuffixName(string name)
        {
            if (_configuration.Milestones.SuffixNameMappings.TryGetValue(name, out var mappedName))
                return mappedName;

            return name;
        }

        private WorkItemProduct GetOrCreateProduct(string name)
        {
            if (!_products.TryGetValue(name, out var result))
            {
                result = new WorkItemProduct(_workspace, name);
                _products.Add(name, result);
            }

            return result;
        }

        private WorkItemMilestone GetOrCreateMilestone(WorkItemProduct product, WorkItemVersion version, DateTimeOffset? releaseDate)
        {
            var key = (product, version);

            if (!_milestones.TryGetValue(key, out var result))
            {
                result = new WorkItemMilestone(product, version, releaseDate);
                _milestones.Add(key, result);
            }

            return result;
        }

        public IReadOnlyList<WorkItemProduct> GetProducts()
        {
            return _products.Values.OrderBy(p => p.Name)
                                   .ToArray();
        }

        public IReadOnlyList<WorkItemMilestone> GetMilestones()
        {
            return _milestones.Values.OrderBy(m => m.Product.Name)
                                     .ThenBy(m => m.Version)
                                     .ToArray();
        }

        private sealed class ProductVersionMapping
        {
            private ProductVersionMapping(Version? lower, Version? upper, string productName)
            {
                if (upper is null && lower is null)
                    throw new ArgumentNullException(nameof(upper), "upper can't be null if lower is already null");

                ArgumentNullException.ThrowIfNull(productName);

                Lower = lower;
                Upper = upper;
                ProductName = productName;
            }

            public Version? Lower { get; }

            public Version? Upper { get; }

            public string ProductName { get; }

            public bool Contains(Version version)
            {
                if (Lower is not null && version < Lower)
                    return false;

                if (Upper is not null && Upper < version)
                    return false;

                return true;
            }

            public static ProductVersionMapping Parse(string versionRange, string productName)
            {
                ArgumentNullException.ThrowIfNull(versionRange);
                ArgumentNullException.ThrowIfNull(productName);

                var parts = versionRange.Split("-");
                if (parts.Length != 2)
                    throw new FormatException();

                var lowerText = parts[0].Trim();
                var upperText = parts[1].Trim();

                var lower = lowerText.Length == 0 ? null : Version.Parse(lowerText);
                var upper = upperText.Length == 0 ? null : Version.Parse(upperText);

                return new ProductVersionMapping(lower, upper, productName);
            }
        }
    }
}
