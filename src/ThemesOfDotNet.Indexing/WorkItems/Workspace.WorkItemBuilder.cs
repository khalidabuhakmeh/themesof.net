using System.Diagnostics;

using ThemesOfDotNet.Indexing.Validation;

namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed partial class Workspace
{
    private sealed class WorkItemBuilder
    {
        private Dictionary<string, SortedSet<string>> _childIdsByParentId = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, SortedSet<string>> _parentIdsByChildId = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, WorkItem> _workItemById = new(StringComparer.OrdinalIgnoreCase);
        private List<Diagnostic> _diagnostics = new();

        public void AddWorkItem(WorkItem workItem)
        {
            ArgumentNullException.ThrowIfNull(workItem);

            _workItemById.Add(workItem.Id, workItem);
        }

        public void AddChild(string parentId, string childId)
        {
            ArgumentNullException.ThrowIfNull(parentId);
            ArgumentNullException.ThrowIfNull(childId);

            // Record child

            if (!_childIdsByParentId.TryGetValue(parentId, out var childIds))
            {
                childIds = new(StringComparer.OrdinalIgnoreCase);
                _childIdsByParentId.Add(parentId, childIds);
            }

            childIds.Add(childId);

            // Record parent

            if (!_parentIdsByChildId.TryGetValue(childId, out var parentIds))
            {
                parentIds = new(StringComparer.OrdinalIgnoreCase);
                _parentIdsByChildId.Add(childId, parentIds);
            }

            parentIds.Add(parentId);
        }

        public void Build(out IReadOnlyList<WorkItem> workItems,
                          out IReadOnlyDictionary<WorkItem, IReadOnlyList<WorkItem>> parentsByChild,
                          out IReadOnlyDictionary<WorkItem, IReadOnlyList<WorkItem>> childrenByParent,
                          out IReadOnlyList<Diagnostic> diagnostics)
        {
            RemoveAndReportInvalidIds();
            DetectAndBreakCycles();
            UnlinkOpenChildrenInClosedParents();

            workItems = _workItemById.Values.OrderBy(wi => wi).ToArray();

            parentsByChild = _parentIdsByChildId.ToDictionary(kv => _workItemById[kv.Key],
                                                              kv => (IReadOnlyList<WorkItem>)kv.Value.Select(id => _workItemById[id]).ToArray());

            childrenByParent = _childIdsByParentId.ToDictionary(kv => _workItemById[kv.Key],
                                                                kv => (IReadOnlyList<WorkItem>)kv.Value.Select(id => _workItemById[id]).ToArray());

            diagnostics = _diagnostics.ToArray();
        }

        private void RemoveAndReportInvalidIds()
        {
            var invalidIds = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            CheckForInvalidIds(invalidIds, _workItemById, _childIdsByParentId, _diagnostics);
            CheckForInvalidIds(invalidIds, _workItemById, _parentIdsByChildId, _diagnostics);

            static void CheckForInvalidIds(SortedSet<string> invalidIds,
                                           Dictionary<string, WorkItem> workItemById,
                                           Dictionary<string, SortedSet<string>> dictionary,
                                           List<Diagnostic> diagnostics)
            {
                foreach (var (fromId, toIds) in dictionary.ToArray())
                {
                    if (!workItemById.TryGetValue(fromId, out var from))
                    {
                        // We let the other side handle the reporting.
                        dictionary.Remove(fromId);
                        continue;
                    }

                    foreach (var toId in toIds.ToArray())
                    {
                        if (!workItemById.ContainsKey(toId))
                        {
                            toIds.Remove(toId);

                            if (invalidIds.Add(toId))
                            {
                                diagnostics.Report("CR01", $"{from.ToMarkdownLink()} links to '{toId}', which doesn't exist.", from);
                            }
                        }
                    }
                }
            }
        }

        private void DetectAndBreakCycles()
        {
            var ancestors = new List<WorkItem>();

            foreach (var workItem in _workItemById.Values.OrderBy(wi => wi.Kind)
                                                         .ThenBy(wi => wi.Id))
            {
                Debug.Assert(ancestors.Count == 0);
                WalkChildren(ancestors, workItem);
            }
        }

        private void WalkChildren(List<WorkItem> ancestors, WorkItem workItem)
        {
            var cycleStart = ancestors.IndexOf(workItem);

            if (cycleStart >= 0)
            {
                var path = ancestors.Skip(cycleStart).Concat(new[] { workItem }).ToArray();
                var pathWithLinks = string.Join(" :arrow_right: ", path.Select(i => i.ToMarkdownLink()));
                var message = $"There is a cycle between issues: {pathWithLinks}";
                _diagnostics.Report("CR02", message, path);

                // Break cycle

                var parentId = ancestors.Last().Id;
                var childId = workItem.Id;

                _parentIdsByChildId[childId].Remove(parentId);
                _childIdsByParentId[parentId].Remove(childId);
            }
            else if (_childIdsByParentId.TryGetValue(workItem.Id, out var childIds))
            {
                foreach (var childId in childIds.ToArray())
                {
                    var child = _workItemById[childId];

                    ancestors.Add(workItem);
                    WalkChildren(ancestors, child);
                    ancestors.Remove(workItem);
                }
            }
        }

        private void UnlinkOpenChildrenInClosedParents()
        {
            // Note: We only want to unlink children if they are contained in another
            //       parent that is still open. Otherwise, we might promote too many
            //       items into the root, which can quickly create a mess.
            //
            //       In order to make things clean, we should have a validation rule
            //       that tells people to create a new parent for items that are under
            //       a closed parent.

            foreach (var child in _workItemById.Values)
            {
                if (!child.IsOpen)
                    continue;

                var childId = child.Id;

                if (_parentIdsByChildId.TryGetValue(childId, out var parentIds))
                {
                    var anyOpenParents = parentIds.Any(p => _workItemById[p].IsOpen);
                    if (anyOpenParents)
                    {
                        foreach (var parentId in parentIds.ToArray())
                        {
                            var parent = _workItemById[parentId];
                            if (!parent.IsOpen)
                            {
                                parentIds.Remove(parentId);
                                _childIdsByParentId[parentId].Remove(childId);
                            }
                        }
                    }
                }
            }
        }

        private static IReadOnlyList<WorkItemUser> GetDiagnosticAssignees(WorkItem workItem)
        {
            if (workItem.Assignees.Any())
                return workItem.Assignees;

            return new[] { workItem.CreatedBy };
        }
    }
}
