using System.Text.Json.Serialization;
using ThemesOfDotNet.Indexing.GitHub;

namespace ThemesOfDotNet.Indexing.AzureDevOps;

public sealed class AzureDevOpsWorkItem
{
    public AzureDevOpsWorkItem(string server,
                               int number,
                               string type,
                               string title,
                               string state,
                               string? resolution,
                               long? priority,
                               string? cost,
                               string? milestone,
                               string? target,
                               string? release,
                               DateTime createdAt,
                               string createdBy,
                               string? assignedTo,
                               string? areaPath,
                               string? iterationPath,
                               string url,
                               IReadOnlyList<string> tags,
                               IReadOnlyList<AzureDevOpsFieldChange> changes,
                               IReadOnlyList<int> childNumbers,
                               IReadOnlyList<string>? gitHubIssues)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(createdBy);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(childNumbers);
        //ArgumentNullException.ThrowIfNull(gitHubIssues);

        Server = server;
        Number = number;
        Type = type;
        Title = title;
        State = state;
        Resolution = resolution;
        Priority = priority;
        Cost = cost;
        Milestone = milestone;
        Target = target;
        Release = release;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        AssignedTo = assignedTo;
        AreaPath = areaPath;
        IterationPath = iterationPath;
        Url = url;
        Tags = tags;
        Changes = changes;
        ChildNumbers = childNumbers;
        GitHubIssues = gitHubIssues ?? Array.Empty<string>();
        Queries = Array.Empty<AzureDevOpsQueryId>();
    }

    public string Server { get; }
    public int Number { get; }
    public string Type { get; }
    public string Title { get; }
    public string State { get; }
    public string? Resolution { get; }
    public long? Priority { get; }
    public string? Cost { get; }
    public string? Milestone { get; }
    public string? Target { get; }
    public string? Release { get; }
    public DateTime CreatedAt { get; }
    public string CreatedBy { get; }
    public string? AssignedTo { get; }
    public string? AreaPath { get; }
    public string? IterationPath { get; }
    public string Url { get; }
    public IReadOnlyList<string> Tags { get; }
    public IReadOnlyList<AzureDevOpsFieldChange> Changes { get; }
    public IReadOnlyList<int> ChildNumbers { get; }
    public IReadOnlyList<AzureDevOpsQueryId> Queries { get; set; }
    public IReadOnlyList<string> GitHubIssues { get; }

    [JsonIgnore]
    public AzureDevOpsWorkItemId Id => new(Server, Number);
}
