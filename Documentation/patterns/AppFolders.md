# App folders

A plang web app is a folder of goal files, and how you split it into folders is more than tidiness: an event binds to a folder
(`before each goal in "/admin/.*"`), so the folders decide who may run what. This page is the recommended layout.

## The layout

```
myapp/
  Start.goal              starts the app: environment, web server, routes, scheduler
  Setup/                  the schema: Setup.goal for the main database, one file per other database
  events/Events.goal      every event binding of the app
  ui/                     the layout and the templates
  user/                   the person's own things: LoadUser, their pages, linking a browser to them
  product/                everything about products: the pages, the json a script asks for
  order/                  everything about orders
  search/                 search
  admin/                  restricted to admins; admin/Authenticate.goal is its gate
  admin/sysadmin/         restricted further; admin/sysadmin/Authenticate.goal
  job/                    the goals the scheduler runs, e.g. job/RefreshRates.goal
  routes/                 when the routes outgrow one AddRoutes goal: routes/Product.goal, routes/Admin.goal
  deploy/                 the goals that install and update the app on a server
  sql/                    .sql files, when a query is too long for a step
  js/plang/dist/          the plang js client, copied from the release
  plang-sw.js             the client's service worker, at the root
  .build/ .db/ .modules/ .services/   the runtime's: see Folder structure
```

## The rule: a folder is named for what it does

- **A folder per thing**: `user/`, `product/`, `order/`, `search/`. The goal a route calls lives in the folder of its thing:
  the product page and the route that answers product json for a script both live in `product/`.
- **No `api/` and no `pages/` folder.** They split one thing in two by how it is answered, and a gate on `api/` then guards
  half of `product/`.
- **`admin/` for what is restricted**, and a folder inside it for what is restricted further. A folder's gate lives in the
  folder, as `Authenticate.goal`, and `events/Events.goal` binds it:

```plang
Events
- before each goal in "/admin/.*", call /admin/Authenticate
- before each goal in "/admin/sysadmin/.*", call /admin/sysadmin/Authenticate
```

  A goal in `admin/sysadmin/` is checked by both gates, the outer one first, so the folders nest the way the permissions do.
  The gates, and what each user sees: [Folder authentication](./FolderAuthentication.md).
- **The url does not protect anything.** A binding matches the goal file's path, not the url of the route. A route
  `/admin/profile` that calls `/user/Profile` runs no admin gate. Keep the url and the folder the same where you can, so it reads
  the way it works.
- **A goal that is shared by many folders** (a helper, a mail sender) goes in the folder of the thing it is about, never into a
  restricted folder: there, every caller passes its gate.

## Nothing that runs at start or on the scheduler goes in a guarded folder

Start, the web server's `on start` goal and scheduled jobs run as the app itself, with no `%user%`, so a folder's gate sends
them away. Two folders follow from that:

- **`job/`** holds the goals the scheduler runs. A job does not call goals in `admin/`; what a job and an admin page both need
  lives in the folder of its thing. [Folder authentication](./FolderAuthentication.md#5-nothing-that-runs-without-a-request-goes-in-a-guarded-folder)
  shows the other ways and what they cost.
- **`routes/`** holds the route files, and is the one folder named for how it works, not for a thing. A small app adds all its
  routes in one `AddRoutes` goal in `Start.goal`. When the list grows, split it by area and call each from `AddRoutes`:

```plang
Start
- start webserver, port: 8080, on start call AddRoutes

AddRoutes
- add route "/", call /Home
- call goal /routes/Admin

/ routes/Admin.goal
Admin
- add route "/admin", call /admin/Dashboard
- add route "/admin/sysadmin", call /admin/sysadmin/Settings
```

  A file for dev only, `routes/Dev.goal`, is called only when the environment is dev. Never put a route file inside a guarded
  folder: `admin/Routes.goal` runs the admin gate when `AddRoutes` calls it, there is no user at start, and the redirect ends
  `Start`. The web server never listens and nothing is logged.

## What goes where

- **`Start.goal`**: what runs once when the app starts, the routes and the schedule.
  See [Recommended app structure](../RecommendedAppStructure.md) for what a production `Start` holds.
- **`Setup/`**: every `.goal` file in it is a setup file. One per database, so each schema reads on its own:
  `Setup.goal`, and for example `SetupUser.goal` for a [database per user](./PerEntityDatabase.md).
- **`events/Events.goal`**: the bindings only. The goals they call live where they belong: a gate in the folder it guards,
  `LoadUser` in `user/`, a goal that records slow pages in `events/`.
- **`ui/`**: `layout.html` and the templates a goal renders.
- **`js/plang/dist/` and `plang-sw.js`**: copied from the plang release, never edited. See [Web server](../Webserver.md).

See also: [Folder structure](../FolderStructure.md) (the folders the runtime itself uses), [Folder authentication](./FolderAuthentication.md),
[Loading the user](./LoadUser.md), [Events](../Events.md).
