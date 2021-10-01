# themesof.net

This is a re-implementation of themesof.net. The goals:

* Make the site more reliable and reduce latency by using caching and GitHub
  events
* Continue to support public and private repos, including AzDO queries
* Include Microsoft user information so that user names are unified between
  GitHub and AzDO queries
* Make the aggregated view more automatic by leveraging the information that the
  team already provides in the leafs
* Use a validation system that helps us keeping the data consistent

For the documentation see, [Documentation/process.md](Documentation/process.md).

## Open Issues

### Data model

* The GitHub data should be immutable
    - Means we can't use the "preserve references" JSON serialization mode
    - Realistically, that just means the issues need to store the labels as IDs
      and issues can't refer to their containing repos.
* `Team` should be a first-class object
    - A team should have a list of areas
* `Area` should probably be first class object so we can just compare using
  reference equality

### Querying

* Can we support hierarchical querying? Basically instead of a single filter,
  we'd offer two "from" and "to" (maybe a check "include indirect"). This way,
  we could easily build cross team/area dependency analysis.

### Web site UI

* Clean-up CSS
* Consider persisting expanded nodes
* Support export to CSV
