using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

using ThemesOfDotNet.Indexing.Configuration;

namespace ThemesOfDotNet.Indexing.AzureDevOps;

public sealed class AzureDevOpsQueryCrawler
{
    private readonly string _token;
    private readonly AzureDevOpsCache _cache;

    public AzureDevOpsQueryCrawler(string token, AzureDevOpsCache cache)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(cache);

        _token = token;
        _cache = cache;
    }

    public async Task CrawlAsync(IReadOnlyCollection<AzureDevOpsQueryConfiguration> queries)
    {
        ArgumentNullException.ThrowIfNull(queries);

        try
        {
            var workItems = new List<AzureDevOpsWorkItem>();

            foreach (var query in queries)
            {
                Console.WriteLine($"Fetching query {query.QueryId} from {query.Url}...");

                var queryItems = await QueryAsync(query);
                workItems.AddRange(queryItems);
            }

            Console.WriteLine($"Caching {workItems.Count:N0} work items...");
            await _cache.StoreAsync(workItems);
        }
        catch (Exception ex)
        {
            GitHubActions.Error("Can't crawl Azure DevOps:");
            GitHubActions.Error(ex);
        }
    }

    private async Task<IReadOnlyList<AzureDevOpsWorkItem>> QueryAsync(AzureDevOpsQueryConfiguration query)
    {
        var url = new Uri(query.Url);
        var connection = new VssConnection(url, new VssBasicCredential(string.Empty, _token));
        var client = connection.GetClient<WorkItemTrackingHttpClient>();
        var itemQueryResults = await client.QueryByIdAsync(new Guid(query.QueryId));

        var workItemRelations = itemQueryResults.WorkItemRelations ?? Enumerable.Empty<WorkItemLink>();

        var itemIds = workItemRelations.Select(rel => rel.Target)
              .Concat(workItemRelations.Select(rel => rel.Source))
              .Where(r => r != null)
              .Select(r => r.Id)
              .ToHashSet();

        var childIdsByParentId = new Dictionary<int, List<int>>();

        foreach (var link in workItemRelations)
        {
            if (link.Source == null || link.Target == null)
                continue;

            if (link.Rel != "System.LinkTypes.Hierarchy-Forward")
                continue;

            var parentId = link.Source.Id;
            var childId = link.Target.Id;

            if (!childIdsByParentId.TryGetValue(parentId, out var childIds))
            {
                childIds = new List<int>();
                childIdsByParentId.Add(parentId, childIds);
            }

            childIds.Add(childId);
        }

        var items = await GetWorkItemsAsync(client, itemIds);

        var workItems = new List<AzureDevOpsWorkItem>();

        foreach (var item in items)
        {
            var queryId = query.QueryId;
            var id = item.Id!.Value;
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
            var changes = await GetFieldChangesAsync(client, id);
            var childIds = GetChildIds(item, childIdsByParentId);

            var workItem = new AzureDevOpsWorkItem(
                queryId,
                id,
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

            workItems.Add(workItem);
        }

        return workItems.ToArray();

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

        static int[] GetChildIds(WorkItem item, Dictionary<int, List<int>> childIdsByParentId)
        {
            return childIdsByParentId.TryGetValue(item.Id!.Value, out var childIdList)
                     ? childIdList.ToArray()
                     : Array.Empty<int>();
        }
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
        Console.WriteLine($"Fetching fields updates for {id}...");

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
}
