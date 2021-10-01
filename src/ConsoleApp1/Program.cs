using System.Diagnostics;

using ThemesOfDotNet.Indexing.GitHub;
using ThemesOfDotNet.Indexing.WorkItems;

try
{
    var myDirectory = Path.GetDirectoryName(Environment.ProcessPath);
    var webSitePath = Path.GetFullPath(Path.Join(myDirectory, "..", "..", "..", "..", "themesof.net", "bin", "Debug", "net6.0"));
    var cachePath = Path.Join(webSitePath, "cache");

    var workspace = await Workspace.LoadFromDirectoryAsync(cachePath);

    foreach (var g in workspace.WorkItems.Select(wi => ((wi.Original as GitHubIssue)?.Repo, WorkItem: wi))
                                         .Where(t => t.Repo is not null)
                                         .GroupBy(t => t.Repo!, t => t.WorkItem)
                                         .OrderBy(g => g.Key.Owner)
                                         .ThenBy(g => g.Key.Name))
    {
        var milestones = g.Select(wi => wi.Milestone)
                          .Where(m => m is not null)
                          .Select(m => m!);
        var changeMilestones = g.SelectMany(wi => wi.Changes)
                                .SelectMany(c => new[] { c.PreviousValue, c.Value })
                                .OfType<WorkItemMilestone>();
        var allMilestones = milestones.Concat(changeMilestones)
                                      .Distinct()
                                      .GroupBy(m => m.Product);

        if (allMilestones.Any())
        {
            Console.WriteLine(g.Key.FullName);
            foreach (var pg in allMilestones)
            {
                var min = pg.MinBy(m => m.Version);
                var max = pg.MaxBy(m => m.Version);
                Console.WriteLine($"    {min} - {max}");
            }
        }
    }

    return 0;
}
catch (Exception ex) when (!Debugger.IsAttached)
{
    Console.WriteException(ex);
    return 1;
}

#pragma warning disable CS8321 // Unused local function

static void WriteRoadmap(WorkItemRoadmap roadmap, string directory)
{
    Directory.CreateDirectory(directory);

    using (var writer = File.CreateText(Path.Join(directory, "roadmap.txt")))
        WriteRoadmapEntries(roadmap, writer);

    using (var writer = File.CreateText(Path.Join(directory, "milestones.txt")))
        WriteMilestones(roadmap.Milestones, writer);
}

static void WriteRoadmapEntries(WorkItemRoadmap roadmap, TextWriter writer)
{
    writer.Write("ID");

    writer.Write('\t');
    writer.Write("Work Item");

    writer.Write('\t');
    writer.Write("Before");

    foreach (var milestone in roadmap.Milestones)
    {
        writer.Write('\t');
        writer.Write(milestone.Version);
    }

    writer.Write('\t');
    writer.Write("After");

    writer.WriteLine();

    foreach (var root in roadmap.Workspace.RootWorkItems)
        WriteRoadmapWorkItems(roadmap, root, writer, 0);
}

static void WriteRoadmapWorkItems(WorkItemRoadmap roadmap, WorkItem workItem, TextWriter writer, int level)
{
    var entry = roadmap.GetEntry(workItem);
    if (entry is not null)
    {
        writer.Write(workItem.Id);

        writer.Write('\t');
        writer.Write(new string(' ', level * 2));
        writer.Write(workItem.Title);

        var from = roadmap.Milestones.First();
        var to = roadmap.Milestones.Last();

        entry.GetStates(from, to, out var before, out var after, out var states);

        writer.Write('\t');

        if (before is not null)
            writer.Write($"{before.Value.State} since {before.Value.Milestone.Version}");

        foreach (var state in states)
        {
            writer.Write('\t');
            writer.Write(state);
        }

        writer.Write('\t');

        if (after is not null)
            writer.Write($"{after.Value.State} since {after.Value.Milestone.Version}");

        writer.WriteLine();
    }

    foreach (var child in workItem.Children)
        WriteRoadmapWorkItems(roadmap, child, writer, level + 1);
}

static void WriteMilestones(IEnumerable<WorkItemMilestone> milestones, TextWriter writer)
{
    foreach (var milestone in milestones)
    {
        writer.Write(milestone);
        writer.Write('\t');
        writer.Write(milestone.ReleaseDate);
        writer.WriteLine();
    }
}

static void WriteWorkItemMilestones(Workspace workspace, string directory)
{
    Directory.CreateDirectory(directory);

    using var writer = File.CreateText(Path.Join(directory, "workItemMilestones.txt"));

    writer.Write("ID");
    writer.Write('\t');
    writer.Write("SequenceNumber");
    writer.Write('\t');
    writer.Write("Milestone");
    writer.WriteLine();

    foreach (var workItem in workspace.WorkItems)
    {
        var milestones = new HashSet<WorkItemMilestone>();
        if (workItem.Milestone is not null)
            milestones.Add(workItem.Milestone);

        foreach (var change in workItem.Changes)
        {
            if (change.Value is WorkItemMilestone v)
                milestones.Add(v);
            if (change.PreviousValue is WorkItemMilestone p)
                milestones.Add(p);
        }

        var sequenceNumber = 0;

        foreach (var milestone in milestones.OrderBy(m => m.Product.Name)
                                            .ThenBy(m => m.Version))
        {
            writer.Write(workItem.Id);
            writer.Write('\t');
            writer.Write(sequenceNumber);
            writer.Write('\t');
            writer.Write(milestone);
            writer.WriteLine();

            sequenceNumber++;
        }
    }
}
