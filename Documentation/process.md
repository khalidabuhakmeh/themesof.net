# Intended Process

This document captures how the tool expects to find the state on GitHub.

## Issue kinds

The site indexes the tree structure across all our orgs, but it only starts with
issues that are labelled as `Theme`, `Epic`, or `User Story`. However, any issue
that is referenced from those will also show up, even it's not a theme, epic or
user story. Those issues are referred to as `Tasks` as those are usually tasks
for the engineering team.

## Linking

Normally, work items are linked by referencing them inside a task list of their
parent, like so:

```Markdown
- [ ] #12
- [ ] dotnet/runtime#123
- [ ] https://github.com/dotnet/runtime#123
```

However, you can also link the parent, like so:

```Markdown
[Parent](https://github.com/dotnet/runtime#123)
```

This is normally only done for cases where the child is in private repo while
the parent is in public repo.

**Noted** Avoid including the title in the Markdown as GitHub now automatically
renders it as part of the link.

## Priorities

A work item can have a priority. Themes generally don't have a priority as they
are top-down demands for areas of investments. However, the epics and user
stories within those usually have priorities.

Priority values range from 0 to 3, with lower values being considered more
important. This is expressed via the labels `priority:0`, `priority:1`, and so
on.

**Note**: The leadership view only shows work items that have either no priority
or if their value is `0` or `1`. The intent is to focus on big rocks.

## Costs

Work items use t-shirt size costing, expressed as those labels:

* `Cost:S`
* `Cost:M`
* `Cost:L`
* `Cost:XL`

## States

An issue is either open or closed. That's not particular useful for use because
we need to know a bit more:

* `Proposed`. An issue that is open and has no status label applied is
  considered proposed. These are work items that haven't necessarily been
  reviewed yet and might not be planned yet.

* `Committed`. This is expressed via the label `Status:Committed` and indicates
  that this work item was reviewed by the product team and is planned for an
  upcoming release.

* `In Progress`. This is expressed via the label `Status:InProgress` and
  indicates that the engineering team has started working on it.

* `Completed`. This is expressed via the label `Status:Completed` and indicates
  that the engineering team has completed the work.

* `Cut`. This is expressed via the label `Status:Cut` and indicates that the
  product team has decided to cut the work item from the current release.

**Note**: This is a deviation from what we did in .NET 6 where the state was
expressed via the associated .NET 6 project board.

## Milestones

For the release tracking we'll be using milestones instead of project boards.
Since there are multiple release vehicles, we need to indicate the product as
part of the milestone, like `.NET 7.0` and `VS 17.2`. For .NET and VS releases
this is less important as the version number ranges are disjoint, but we have
other products that overlap, such as YARP and NuGet.

## Guidelines

* A given issue should only have a single kind, that is, it should only have one
  of the labels `Theme`, `Epic`, or `User Story`.

* When an item is marked as committed, it should be assigned a milestone.
  Otherwise it's not clear which release it is committed for.

* When an item is closed, it should be marked as either completed or cut.
  Completed items should have a milestone to indicate which release they were
  completed for.

* During a release cycle, it's OK to move items freely between previews.
  However, when an epic was only partially completed in .NET 6, it should be
  closed and the remaining work should be booked for a new epic. This ensures
  that we can look back at the .NET 6 release and understand what was completed.
  This is important as the public will generally look at our roadmap right after
  we've shipped and the engineering team has started to work on the next
  version. We want to be clear what work is completed and what work is planned.
