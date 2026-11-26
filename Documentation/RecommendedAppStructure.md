# Recommended app structure

What an app needs depends on what it does. Start with the smallest level that fits and add the next one when the app needs it.
Each level only adds to the one before; nothing here has to be built in advance.

| Your app | Level | You need |
|---|---|---|
| Runs a task and ends (console, script, tool) | 1 | `Start.goal`, `Setup/` if it has a database |
| A web app you run yourself, small, one machine | 2 | level 1 + `start webserver` with a port |
| A web app with real users on a server | 3 | level 2 + `live`, environment, seeding before the first visitor, scheduler, certificate |
| A web app you update over real data | 4 | level 3 + the procedure in [Deploying a new version](./Deploy.md) |

## Level 1: console app

```plang
Start
- set default value %name% = "world"
- write out "hello %name%"
```

Run with `plang` or `plang name=Anna`. Arguments of the form `key=value` become variables; `set default value` keeps the one given and
fills in the one that is missing. A goal other than Start runs with `plang exec path/to/Goal key=value`, and a goal that ends in
`throw error` makes plang exit with 1, so a script can be used from the shell.

If the app has a database, the schema goes in `Setup/` and is append only: [Setup and migrations](./SetupAndMigrations.md).
There is no `live`, no port and no mock at this level.

## Level 2: small web app

```plang
Start
- set default value %port% = 8080
- start webserver, port: %port%
```

The port is an argument with a default, so `plang port=8091` starts it elsewhere without touching the file. Always `set default value`,
never a plain `set %port% = 8080`, which would overwrite the argument.
Start the web server in `Start`. It can also be started from a goal that runs in a request or on the scheduler (a second web server
started by a deploy, for example).
Routes, pages and the request gate: [Running on a server](./RunningOnAServer.md), [Identity in the browser](./IdentityInTheBrowser.md),
[Events](./Events.md).

Record slow goals from the first day: an `after each goal` event that writes every entry goal taking more than 300 ms to a table.
A page should answer in about 100 ms, and the table tells you which page does not and where the time goes, before a user does.
The pattern, with the step level detail for when you fix one: [Finding slow goals](./patterns/SlowGoals.md).

## Level 3: web app in production

Add what production needs and development does not: `live=1` on the command line to pick the production settings, seeding that
finishes before the first visitor (`BeforeStart`), the scheduler, routes split by area with dev routes only in dev, and the
certificate. One `Start.goal` serves both. The full Start.goal, built and run both ways: [Start and environments](./patterns/StartAndEnvironments.md).

A web app with real users also records its errors in a table from the first day: [App error handling](./patterns/AppErrorHandling.md).

## Level 4: updating a web app that has real data

Deploying a new version of an app that has real users and real data, whether it is a new plang runtime, changed goals or both, is a
procedure of its own: the app has to be set up for it, the test databases have to be ready, and there are several steps in a fixed
order. It is only worth the work once a deploy can hurt real users, and a small app does not need it.

The full best practice is on its own page: [Deploying a new version](./Deploy.md). It builds on [Mocking a provider](./TalkingToAnApi.md#mocking-a-provider-for-a-test-instance) and
[Settings](./Settings.md).

## Other topics

| Topic | Page |
|---|---|
| Folders, and which folder is restricted to whom | [App folders](./patterns/AppFolders.md) |
| Schema and migrations | [Setup and migrations](./SetupAndMigrations.md) |
| Authorization: who may see which page | [Identity in the browser](./IdentityInTheBrowser.md) |
| Identity | [Identity](./Identity.md) |
| Events | [Events](./Events.md) |
| Slow pages and goals | [Finding slow goals](./patterns/SlowGoals.md) |
| Errors | [Error handling](./ErrorHandler.md) |
| Running on a server | [Running on a server](./RunningOnAServer.md) |
| Startup parameters | [Startup parameters](./StartupParameters.md) |
| External APIs and their mocks | [Talking to an API](./TalkingToAnApi.md) |
