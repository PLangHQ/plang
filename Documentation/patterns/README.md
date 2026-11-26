# Patterns

The recommended way to solve the problems every app has. Each page is one pattern: the problem, the .goal that solves it, and
why it is done that way and not the obvious other way. Every example on these pages was built and run.

Read this list before you write an app, and follow the pattern when one fits. Where a page says a choice is the developer's,
ask the developer; do not pick for them.

## Structure

| Pattern | Use it for |
|---|---|
| [App folders](./AppFolders.md) | Folders named for what they do (`user/`, `product/`, `admin/`, `job/`), never `api/` or `pages/`; where routes, events and jobs live. |
| [Start and environments](./StartAndEnvironments.md) | Start.goal of a web app: dev and production, seeding before the first visitor, the scheduler, routes split by area. |
| [A database per user or per entity](./PerEntityDatabase.md) | Giving each user, customer or contract its own database, and the shared database for reports. A decision for the developer. |

## Pages

| Pattern | Use it for |
|---|---|
| [Page layout](./PageLayout.md) | The plang js client in the page, one layout set in Start, pages rendered into it. |
| [First page load](./FirstPageLoad.md) | The first GET returns the whole page, also for a new visitor; the parts that depend on who it is are filled in on the poll. |
| [Partial update](./PartialUpdate.md) | Updating one part of a page: the goal renders a template into a target; ids in the route, form data in the body. |

## Who is asking

| Pattern | Use it for |
|---|---|
| [Loading the user](./LoadUser.md) | From `%Identity%` to `%user%`: a row per browser, linked to a person, cached. |
| [Folder authentication](./FolderAuthentication.md) | A gate in the folder it guards (`admin/Authenticate.goal`), permissions that cascade to subfolders, and jobs outside guarded folders. |

## Running it

| Pattern | Use it for |
|---|---|
| [App error handling](./AppErrorHandling.md) | Every server and browser error in one table a bot can read and fix from; what the visitor, an admin and a developer see. |
| [Finding slow goals](./SlowGoals.md) | Recording entry goals slower than a limit, and the steps that took the time while you fix one. |
| [Cache keys](./CacheKeys.md) | Caching an expensive query with a key that holds every input that changes the answer. |
| [Calling an outside api](./ExternalApiCalls.md) | A timeout, recording the failure, and a scheduled retry, so a slow or down service never fails the page. |
| [SQL in files](./SqlInFiles.md) | Long queries in `.sql` files run with `query file`. |

See also: [Recommended app structure](../RecommendedAppStructure.md), [Web server](../Webserver.md), [Pitfalls](../Pitfalls.md).
