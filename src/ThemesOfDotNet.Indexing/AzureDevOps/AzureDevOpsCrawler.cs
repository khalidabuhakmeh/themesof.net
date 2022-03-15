using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

using ThemesOfDotNet.Indexing.Configuration;

namespace ThemesOfDotNet.Indexing.AzureDevOps;

// TODO: Add support for indexing GitHub links

public sealed class AzureDevOpsCrawler
{
    private readonly string _token;
    private readonly AzureDevOpsCache _cache;

    private readonly HashSet<AzureDevOpsQueryId> _crawledQueries = new();
    private readonly List<AzureDevOpsQueryId> _pendingQueries = new();

    private readonly HashSet<AzureDevOpsWorkItemId> _crawledItems = new();
    private readonly List<AzureDevOpsWorkItemId> _pendingItems = new();

    private readonly Dictionary<AzureDevOpsWorkItemId, AzureDevOpsWorkItem> _workItemById = new();
    private readonly Dictionary<AzureDevOpsWorkItemId, List<AzureDevOpsQueryId>> _queriesByWorkItemId = new();

    private readonly Dictionary<string, WorkItemTrackingHttpClient> _clientByServerUrl = new(StringComparer.OrdinalIgnoreCase);

    public AzureDevOpsCrawler(string token, AzureDevOpsCache cache)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(cache);

        _token = token;
        _cache = cache;
    }

    public bool HasPendingWork => _pendingItems.Count > 0 ||
                                  _pendingQueries.Count > 0;

    public void Enqueue(AzureDevOpsQueryId query)
    {
        if (_crawledQueries.Add(query))
            _pendingQueries.Add(query);
    }

    public void Enqueue(AzureDevOpsWorkItemId item)
    {
        if (_crawledItems.Add(item))
            _pendingItems.Add(item);
    }

    public async Task CrawlPendingAsync()
    {
        while (_pendingQueries.Any() || _pendingItems.Any())
        {
            // Crawl pending queries

            var pendingQueries = _pendingQueries.ToArray();
            _pendingQueries.Clear();

            foreach (var query in pendingQueries)
                await CrawlAsync(query);

            // Crawl pending items

            var pendingItems = _pendingItems.ToArray();
            _pendingItems.Clear();

            if (pendingItems.Length > 0)
                await CrawlAsync(pendingItems);

            // Record which queries a given work item was included in

            foreach (var workItem in _workItemById.Values)
            {
                if (_queriesByWorkItemId.TryGetValue(workItem.Id, out var queries))
                    workItem.Queries = queries.ToArray();
            }
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var workItems = _workItemById.Values.OrderBy(i => i.Id).ToArray();
            Console.WriteLine($"Caching {workItems.Length:N0} work items...");
            await _cache.StoreAsync(workItems);
        }
        catch (Exception ex)
        {
            GitHubActions.Error("Can't save Azure DevOps cache:");
            GitHubActions.Error(ex);
        }
    }

    private async Task CrawlAsync(AzureDevOpsQueryId query)
    {
        var client = GetClient(query.ServerUrl);
        var itemQueryResults = await client.QueryByIdAsync(new Guid(query.Id));
        var rootNumbers = GetRoots(itemQueryResults);

        foreach (var itemNumber in rootNumbers)
        {
            var itemId = new AzureDevOpsWorkItemId(query.ServerUrl, itemNumber);
            
            if (!_queriesByWorkItemId.TryGetValue(itemId, out var queries))
            {
                queries = new List<AzureDevOpsQueryId>();
                _queriesByWorkItemId.Add(itemId, queries);
            }

            queries.Add(query);
        }

        await CrawlAsync(client, rootNumbers);

        static IReadOnlyList<int> GetRoots(WorkItemQueryResult result)
        {
            if (result.WorkItems is not null)
                return result.WorkItems.Select(wr => wr.Id).ToArray();

            if (result.WorkItemRelations is not null)
            {
                return result.WorkItemRelations.Where(r => r.Rel is null &&
                                                           r.Source is null &&
                                                           r.Target is not null)
                                               .Select(r => r.Target.Id)
                                               .ToArray();
            }

            return Array.Empty<int>();
        }
    }

    private async Task CrawlAsync(IEnumerable<AzureDevOpsWorkItemId> items)
    {
        foreach (var itemGroup in items.GroupBy(i => i.ServerUrl))
        {
            var client = GetClient(itemGroup.Key);
            var numbers = itemGroup.Select(i => i.Number);
            await CrawlAsync(client, numbers);
        }
    }

    private async Task CrawlAsync(WorkItemTrackingHttpClient client, IEnumerable<int> itemNumbers)
    {
        var items = await GetWorkItemsAsync(client, itemNumbers.OrderBy(i => i));

        foreach (var item in items)
        {
            var server = client.BaseAddress.ToString();
            if (server.EndsWith('/'))
                server = server.Substring(0, server.Length - 1);

            var number = item.Id!.Value;
            var id = new AzureDevOpsWorkItemId(server, number);
            if (_workItemById.ContainsKey(id))
                continue;

            Console.WriteLine($"Crawling work item {id}...");

            var type = GetFieldAsString(item, "System.WorkItemType")!;
            var title = GetFieldAsString(item, "System.Title")!;
            var state = GetFieldAsString(item, "System.State")!;
            var priority = GetFieldAsInt64(item, "Microsoft.VSTS.Common.Priority");
            var cost = GetFieldAsString(item, "Microsoft.DevDiv.TshirtCosting");
            var release = GetFieldAsString(item, "Microsoft.eTools.Bug.Release");
            var target = GetFieldAsString(item, "Microsoft.DevDiv.Target");
            var milestone = GetFieldAsString(item, "Microsoft.DevDiv.Milestone");
            var assignedTo = GetFieldAsAlias(item, "System.AssignedTo");
            var createdAt = GetFieldAsDateTime(item, "System.CreatedDate")!.Value;
            var createdBy = GetFieldAsAlias(item, "System.CreatedBy")!;
            var itemUrl = GetUrl(item);
            var tags = GetFieldAsTags(item, "System.Tags");
            var changes = await GetFieldChangesAsync(client, number);
            var childIds = GetChildIds(item);

            foreach (var childId in childIds)
                Enqueue(new AzureDevOpsWorkItemId(server, childId));

            var workItem = new AzureDevOpsWorkItem(
                server,
                number,
                type,
                title,
                state,
                priority,
                cost,
                milestone,
                target,
                release,
                createdAt,
                createdBy,
                assignedTo,
                itemUrl,
                tags,
                changes,
                childIds
            );

            _workItemById.Add(workItem.Id, workItem);
        }

        static string? GetFieldAsString(WorkItem item, string fieldName)
        {
            if (item.Fields.TryGetValue<string>(fieldName, out var value))
                return value;
            else
                return null;
        }

        static DateTime? GetFieldAsDateTime(WorkItem item, string fieldName)
        {
            if (item.Fields.TryGetValue<DateTime>(fieldName, out var value))
                return value;
            else
                return null;
        }

        static long? GetFieldAsInt64(WorkItem item, string fieldName)
        {
            if (item.Fields.TryGetValue<long>(fieldName, out var value))
                return value;
            else
                return null;
        }

        static string? GetFieldAsAlias(WorkItem item, string fieldName)
        {
            if (item.Fields.TryGetValue<IdentityRef>(fieldName, out var value))
                return GetAlias(value);
            else
                return null;
        }

        static string[] GetFieldAsTags(WorkItem item, string fieldName)
        {
            if (item.Fields.TryGetValue<string>(fieldName, out var value))
                return ParseTags(value);
            else
                return Array.Empty<string>();
        }

        static string GetUrl(WorkItem item)
        {
            return item.Links.Links.Where(l => l.Key == "html")
                                   .Select(l => l.Value)
                                   .OfType<ReferenceLink>()
                                   .Select(l => l.Href)
                                   .SingleOrDefault() ?? "";
        }

        static int[] GetChildIds(WorkItem item)
        {
            if (item.Relations is null)
                return Array.Empty<int>();

            var result = new List<int>();

            foreach (var link in item.Relations)
            {
                if (link.Rel != "System.LinkTypes.Hierarchy-Forward")
                    continue;

                var url = new Uri(link.Url);
                var number = int.Parse(url.Segments.Last());
                result.Add(number);
            }

            return result.ToArray();
        }
    }

    private WorkItemTrackingHttpClient GetClient(string serverUrl)
    {
        if (!_clientByServerUrl.TryGetValue(serverUrl, out var result))
        {
            var url = new Uri(serverUrl);
            var connection = new VssConnection(url, new VssBasicCredential(string.Empty, _token));
            var client = connection.GetClient<WorkItemTrackingHttpClient>();
            _clientByServerUrl.Add(serverUrl, client);
            result = client;
        }

        return result;
    }

    private static async Task<List<WorkItem>> GetWorkItemsAsync(WorkItemTrackingHttpClient client, IEnumerable<int> ids)
    {
        var result = new List<WorkItem>();
        var batchedIds = Batch(ids, 200);
        foreach (var batch in batchedIds)
        {
            var items = await client.GetWorkItemsAsync(batch, expand: WorkItemExpand.All);
            result.AddRange(items);
        }

        return result;
    }

    private static IEnumerable<T[]> Batch<T>(IEnumerable<T> source, int batchSize)
    {
        var list = new List<T>(batchSize);

        foreach (var item in source)
        {
            if (list.Count == batchSize)
            {
                yield return list.ToArray();
                list.Clear();
            }
            list.Add(item);
        }

        if (list.Count > 0)
            yield return list.ToArray();
    }

    private static async Task<IReadOnlyList<AzureDevOpsFieldChange>> GetFieldChangesAsync(WorkItemTrackingHttpClient client, int id)
    {
        var result = new List<AzureDevOpsFieldChange>();
        var workItemUpdates = await client.GetUpdatesAsync(id);

        foreach (var workItemUpdate in workItemUpdates)
        {
            if (workItemUpdate.Fields is null)
                continue;

            var alias = GetAlias(workItemUpdate.RevisedBy);
            var when = workItemUpdate.RevisedDate;

            // It seems the actual date is usually stored in this field.
            // In fact, RevisedDate is somtimes 12/31/9999.
            if (workItemUpdate.Fields.TryGetValue("System.ChangedDate", out var changedDateUpdate))
                when = (DateTime)changedDateUpdate.NewValue;

            foreach (var (key, fieldUpdate) in workItemUpdate.Fields)
            {
                var field = ParseField(key);
                if (field is not null)
                {
                    var from = ConvertValue(field.Value, fieldUpdate.OldValue);
                    var to = ConvertValue(field.Value, fieldUpdate.NewValue);
                    var change = new AzureDevOpsFieldChange(alias, when, field.Value, from, to);
                    result.Add(change);

                    static object? ConvertValue(AzureDevOpsField field, object value)
                    {
                        if (field == AzureDevOpsField.Tags)
                        {
                            var text = (string?)value;
                            if (text is null)
                                return Array.Empty<string>();

                            return ParseTags(text);
                        }

                        switch (value)
                        {
                            case null:
                            case string:
                            //case int:
                            case long:
                                return value;
                            case IdentityRef identityRef:
                                return GetAlias(identityRef);
                            default:
                                System.Diagnostics.Debugger.Break();
                                return null;
                        }
                    }
                }
            }
        }

        return result.ToArray();
    }

    private static string GetAlias(IdentityRef identityRef)
    {
        if (identityRef.UniqueName is null)
            return identityRef.DisplayName;

        var email = identityRef.UniqueName;
        var indexOfAt = email.IndexOf('@');
        return indexOfAt >= 0
            ? email.Substring(0, indexOfAt)
            : email;
    }

    private static AzureDevOpsField? ParseField(string fieldName)
    {
        switch (fieldName.ToLowerInvariant())
        {
            case "system.workitemtype":
                return AzureDevOpsField.Type;
            case "system.title":
                return AzureDevOpsField.Title;
            case "system.state":
                return AzureDevOpsField.State;
            case "microsoft.vsts.common.priority":
                return AzureDevOpsField.Priority;
            case "microsoft.devdiv.tshirtcosting":
                return AzureDevOpsField.Cost;
            case "microsoft.etools.bug.release":
                return AzureDevOpsField.Release;
            case "microsoft.devdiv.target":
                return AzureDevOpsField.Target;
            case "microsoft.devdiv.milestone":
                return AzureDevOpsField.Milestone;
            case "system.assignedto":
                return AzureDevOpsField.AssignedTo;
            case "system.tags":
                return AzureDevOpsField.Tags;
            default:
                return null;
        }
    }

    private static string[] ParseTags(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<string>();
        else
            return text.Split(';');
    }

    // Incremental updates

    public void LoadFromCache(IReadOnlyList<AzureDevOpsWorkItem> workItems)
    {
        ArgumentNullException.ThrowIfNull(workItems);

        _crawledQueries.Clear();
        _pendingQueries.Clear();

        _crawledItems.Clear();
        _pendingItems.Clear();

        _workItemById.Clear();
        _queriesByWorkItemId.Clear();

        _clientByServerUrl.Clear();

        foreach (var workItem in workItems)
        {
            _crawledItems.Add(workItem.Id);
            _workItemById.Add(workItem.Id, workItem);

            _crawledQueries.UnionWith(workItem.Queries);
            _queriesByWorkItemId.Add(workItem.Id, workItem.Queries.ToList());
        }
    }

    public Task UpdateAsync()
    {
        // Snapshot IDs of all queries and items we have crawled
        //
        // NOTE: This includes any pending work too.

        var queries = _crawledQueries.ToArray();
        var items = _crawledItems.ToArray();

        // Now clear the entire state...

        _crawledQueries.Clear();
        _pendingQueries.Clear();

        _crawledItems.Clear();
        _pendingItems.Clear();

        _workItemById.Clear();
        _queriesByWorkItemId.Clear();

        // ...and re-queue all queries and items and crawl them again.

        foreach (var query in queries)
            Enqueue(query);

        foreach (var item in items)
            Enqueue(item);

        return CrawlPendingAsync();
    }

    public void GetSnapshot(out IReadOnlyList<AzureDevOpsWorkItem> workitems)
    {
        workitems = _workItemById.Values.ToArray();
    }
}
